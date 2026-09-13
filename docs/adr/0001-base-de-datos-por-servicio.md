# 0001. Dar a cada servicio su propia base de datos en una única instancia de PostgreSQL

## Estado

Aceptada

## Contexto

Los cuatro servicios (`Orders`, `Inventory`, `Payments`, `Shipping`) tienen
estado propio y hoy lo guardan en memoria. T11 los pasa a PostgreSQL con EF
Core 10.

La tesis del repo es que la consistencia entre servicios se consigue con una
saga y compensaciones, no con una transacción que abarque a todos. Esa tesis
sólo es demostrable si el almacenamiento impide físicamente la alternativa
fácil: si `Orders` pudiera leer la tabla de stock con un `JOIN`, la saga
sobraría y el repo estaría demostrando lo contrario de lo que afirma.

Restricciones que ya vienen dadas:

- `docker-compose.yml` levanta **una** instancia de PostgreSQL, sin script de
  init, y esa decisión está cerrada en `CLAUDE.md`: cada servicio crea y migra
  su propia base.
- La regla de "clonar y `docker compose up`" no admite pasos manuales ni
  consumo de recursos desproporcionado en una máquina de desarrollo.
- Los tests de integración levantan sus propias dependencias con
  Testcontainers, así que la topología elegida se paga también en cada
  ejecución de la suite.

## Decisión

**Cuatro bases de datos lógicas —`orders`, `inventory`, `payments`,
`shipping`— dentro de una única instancia de PostgreSQL.** Cada servicio
conoce exclusivamente su cadena de conexión, tiene un único `DbContext` y su
propio juego de migraciones. Ninguna clave foránea, vista, función ni consulta
cruza el límite entre dos bases: un servicio que necesite un dato de otro lo
obtiene por mensaje, nunca por SQL.

El razonamiento:

- La frontera deja de depender de la disciplina. No es que no hagamos el
  `JOIN`: es que el motor no puede resolverlo, porque las tablas están en
  bases distintas y PostgreSQL no hace consultas entre bases sin una extensión
  (`dblink` o `postgres_fdw`) que nadie va a instalar por accidente.
- Una sola instancia mantiene el arranque en un comando y el consumo de
  memoria en un valor razonable para una máquina de desarrollo. El aislamiento
  que se pierde (CPU, memoria, versión del motor por servicio) es aislamiento
  operativo, no arquitectónico, y no es lo que este repo está demostrando.
- Un único contenedor de PostgreSQL en los tests de integración significa un
  arranque de contenedor por ejecución de la suite, no cuatro.

## Consecuencias

**Más fácil**

- La afirmación "cada servicio es dueño de sus datos" pasa a ser verificable:
  basta con comprobar que cada `DbContext` sólo declara sus propias entidades
  y que ningún proyecto referencia el contexto de otro (T31).
- Cada servicio evoluciona su esquema sin coordinarse con los demás.
- El diagrama C4 de nivel 2 (T41) refleja la realidad sin notas al pie.

**Más difícil / lo que se asume**

- No hay ninguna consulta que responda "estado global del pedido" de un tirón.
  Componer esa vista es trabajo de la API de `Orders` y de los eventos de la
  saga. Es el coste esperado, no un efecto secundario.
- Cuatro juegos de migraciones que mantener y cuatro `DbContext` que
  configurar, con código de arranque muy parecido entre servicios. Ese
  parecido **no** se factoriza a un proyecto común: un paquete compartido de
  infraestructura volvería a acoplar los cuatro servicios por la puerta de
  atrás. Se acepta la repetición.
- El usuario de conexión necesita permiso para crear bases de datos
  (`CREATEDB`), porque la base no existe la primera vez que arranca el
  servicio. En `docker-compose.yml` el usuario `orders_saga` es el
  superusuario del contenedor, así que se cumple; en cualquier entorno real
  habría que concederlo explícitamente, y conviene dejarlo dicho.
- La instancia única es un punto de fallo compartido: si PostgreSQL cae, caen
  los cuatro `/health/ready` a la vez. Es correcto para una demo y sería
  inaceptable en producción; queda anotado para el apartado "qué haría
  distinto" del README (T41).

**Cuándo revisar esta decisión**

Si algún servicio necesitara un motor o una versión distinta, o si el objetivo
pasara a ser demostrar aislamiento operativo en lugar de aislamiento de datos,
habría que pasar a una instancia por servicio.

## Alternativas descartadas

- **Un esquema de PostgreSQL por servicio dentro de una base compartida** —
  es la opción más barata en recursos y la más habitual en la práctica, pero
  deja la frontera al nivel de convención: `SELECT ... FROM inventory.stock`
  desde la conexión de `Orders` funciona perfectamente. Todo el valor de esta
  tarea está en que esa consulta sea imposible, no en que esté mal vista.
  Además, un `search_path` mal configurado convierte el aislamiento en un
  problema de configuración silencioso.
- **Una base compartida con prefijos de tabla** (`orders_`, `inventory_`) —
  la misma objeción, agravada: ni siquiera hay un mecanismo de permisos con el
  que apuntalar la separación, y las migraciones de los cuatro servicios
  compiten por la misma tabla de historial de EF Core.
- **Una instancia de PostgreSQL por servicio en el compose** — es la
  separación más fuerte y la más fiel a un despliegue real, pero cuadruplica
  los contenedores, la memoria y el tiempo de arranque, y en los tests de
  integración obliga a levantar cuatro contenedores por ejecución. A cambio
  sólo añade aislamiento operativo: la frontera de datos ya la garantiza la
  base lógica separada. El coste es constante y el beneficio, para lo que este
  repo demuestra, nulo.
- **Un script `init.sql` que cree las cuatro bases al levantar el contenedor**
  — descartado ya en `CLAUDE.md`, y por una razón que conviene dejar escrita:
  duplicaría la fuente de verdad del esquema. Habría un sitio que crea las
  bases (SQL) y otro que crea las tablas (migraciones de EF Core), que además
  no se aplicaría en los tests de integración, donde no hay script de init.
  El servicio tiene que saber crear su base desde cero en cualquier entorno.
- **Un único `DbContext` compartido por los cuatro servicios** — acoplaría los
  cuatro ensamblados al mismo modelo y haría imposible desplegarlos por
  separado, que es la premisa del proyecto entero.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T11 · Ver también: ADR-0002, ADR-0004_
