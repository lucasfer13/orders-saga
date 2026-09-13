# 0005. Reservar stock con una actualización condicional en lugar de un bloqueo en memoria

## Estado

Aceptada

## Contexto

`StockStore.Reserve` es hoy correcto por una razón que desaparece en T11: un
`Lock` en memoria. Dentro de ese bloqueo, el método comprueba todas las líneas
del pedido y sólo entonces descuenta, lo que le permite garantizar dos cosas:

1. **No hay sobreventa**: nunca se reserva más stock del disponible.
2. **Todo o nada**: una reserva parcial dejaría la saga compensando contra un
   estado que el pedido nunca alcanzó. El comentario del propio fichero lo
   dice, y es la invariante más importante del servicio.

Al pasar a PostgreSQL, el `Lock` deja de proteger nada: hay varias peticiones
concurrentes, varios consumidores del mismo mensaje y, potencialmente, varios
procesos. La traducción ingenua —leer la fila, comprobar en C#, restar y
guardar— es un *read-modify-write* clásico: dos transacciones que leen
`disponible = 10` y restan 6 cada una dejan la fila en −2, y ambas responden
que la reserva fue bien.

Esto no es un detalle de implementación que se pueda dejar al criterio de quien
escriba el código: es la diferencia entre que el servicio sea correcto o no, y
condiciona el esquema de la tabla y la forma de los tests. La reserva de stock
es, además, el primer paso de la saga: si sobrevende, todo lo que viene
después está compensando el error equivocado.

Hay que decidirlo ahora, y hay que decidirlo sin apoyarse en las piezas que
llegan más tarde: la idempotencia de los consumidores (T23) y la política de
reintentos (T26) resuelven problemas distintos y no cubren éste.

## Decisión

**La reserva se hace en una transacción explícita, con una actualización
condicional por línea y comprobación del número de filas afectadas.**

Los elementos de la decisión:

- El descuento se expresa como una sola sentencia por línea, del tipo
  "descuenta `n` unidades del producto `p` **siempre que** el disponible sea
  al menos `n`", ejecutada en la base de datos (`ExecuteUpdate`), no como una
  lectura seguida de una escritura. PostgreSQL evalúa la condición bajo el
  bloqueo de fila que toma el propio `UPDATE`, así que la comprobación y el
  descuento son la misma operación atómica.
- **El número de filas afectadas es el resultado**: 1 significa reservado, 0
  significa que no había suficiente. No se vuelve a leer para decidir.
- Si alguna línea devuelve 0, **se aborta la transacción entera**: el "todo o
  nada" lo garantiza el `ROLLBACK`, no el orden de las comprobaciones. Esto es
  un cambio respecto al `Lock`, que comprobaba todo antes de tocar nada; con
  transacción no hace falta, y pretender conservar el patrón de dos pasadas
  reintroduce la ventana entre comprobar y escribir.
- **Las líneas se procesan en un orden determinista** (por identificador de
  producto) para que dos reservas que compitan por los mismos productos no
  puedan quedar en interbloqueo esperándose mutuamente.
- El nivel de aislamiento es el de PostgreSQL por defecto (`READ COMMITTED`).
  No hace falta más: la condición se revalida dentro del `UPDATE`.
- La tabla lleva además una restricción `CHECK` que impide que el disponible
  sea negativo. Es una red declarativa: si algún día alguien escribe el
  descuento sin la condición, la base de datos lo rechaza en lugar de
  permitir una sobreventa silenciosa.

La liberación de stock (la compensación) es la operación inversa y no tiene
condición que comprobar, pero sí depende de que la reserva quede registrada
fila a fila para saber cuánto devolver. Su comportamiento ante mensajes
repetidos **no** se decide aquí: eso es idempotencia (T23) y es material
marcado como manual en `CLAUDE.md`.

## Consecuencias

**Más fácil**

- La invariante se sostiene con varios procesos, que es la situación real en
  cuanto haya un consumidor de mensajes además del endpoint HTTP.
- No hace falta token de concurrencia, ni política de reintentos, ni
  aislamiento elevado para esta operación. Una cosa menos que explicar y una
  cosa menos que pueda fallar de forma no determinista en CI.
- Da material medible y honesto para T36: reservas concurrentes por segundo
  contra una base real.

**Más difícil / lo que se asume**

- `ExecuteUpdate` no pasa por el rastreador de cambios de EF Core: no dispara
  eventos, no aplica concurrencia optimista y no se puede combinar
  descuidadamente con entidades ya cargadas en el mismo contexto. En
  `Inventory` da igual —no hay agregado ni eventos de dominio—, pero es
  exactamente por eso que esta técnica **no** se generaliza a `Orders`.
- **La prueba de la invariante deja de ser unitaria.** Hoy hay un test
  unitario (`Reserves_nothing_when_one_line_cannot_be_satisfied`) que la
  verifica contra el `Lock`. Una condición de carrera contra PostgreSQL no se
  demuestra con un mock: pasa a ser un test de integración con N tareas
  concurrentes reservando el mismo producto y una aserción sobre el total
  final. Es más lento y es el único que prueba algo.
- La lógica queda repartida entre el código y el esquema (la restricción
  `CHECK`). Es una repetición deliberada, y conviene que el comentario de la
  migración diga que es una red de seguridad y no la comprobación principal.
- Si en el futuro la reserva necesitara reglas más ricas (reservas con
  caducidad, prioridades entre pedidos), una sentencia condicional se queda
  corta y habría que volver aquí.

**Cuándo revisar esta decisión**

Si `Inventory` deja de ser un transaction script y las reservas se convierten
en un modelo con estados propios (ver ADR-0004), la concurrencia pasa a ser un
problema del agregado y probablemente de concurrencia optimista.

## Alternativas descartadas

- **Leer, comprobar en C# y guardar con el rastreador de cambios** — es la
  traducción literal del código actual y es incorrecta en cuanto hay dos
  peticiones a la vez: la comprobación y la escritura ocurren en momentos
  distintos y nada impide que otra transacción se cuele entre ambas. Lo grave
  es que en un test secuencial pasa siempre, así que el error no aparece hasta
  que hay carga.
- **Concurrencia optimista con `xmin` como token y reintentos** — es la opción
  idiomática de EF Core con Npgsql y funciona. Se descarta porque convierte
  cada colisión en una excepción y un reintento: hay que escribir una política
  de reintentos aquí, en T11, en el mismo repo donde T26/T27 van a definir
  *otra* política de reintentos para mensajes envenenados. Dos políticas
  distintas con el mismo nombre en un repo que se lee para evaluar criterio es
  un mal negocio, y el `UPDATE` condicional resuelve el caso sin ninguna.
- **Bloqueo pesimista con `SELECT ... FOR UPDATE` sobre las filas de producto**
  — correcto y fácil de razonar: se bloquean las filas, se comprueba y se
  escribe. Cuesta una consulta adicional en SQL crudo por cada reserva y
  mantiene las filas bloqueadas durante toda la transacción, serializando a
  todos los pedidos que compartan un producto popular. La actualización
  condicional consigue la misma garantía sosteniendo el bloqueo sólo durante
  la sentencia.
- **Aislamiento `SERIALIZABLE`** — deja el código ingenuo escrito tal cual y
  delega en el motor. A cambio, cualquier transacción del servicio puede
  fallar con un error de serialización (`40001`) que hay que capturar y
  reintentar **en todas partes**, para resolver un problema que sólo tiene una
  operación. Es pagar en todo el servicio el coste de un caso.
- **Una única sentencia que reserve todas las líneas de golpe** (un `UPDATE`
  con varias filas y comprobación posterior del total de filas afectadas) —
  evita el bucle y el problema de orden, pero hace imposible saber *qué*
  producto faltó, y el mensaje de fallo que la saga propaga ("no hay stock
  suficiente para el producto X") es información que la demo y la compensación
  usan.
- **Mover la reserva a un procedimiento almacenado** — sería atómico y rápido,
  y sacaría la regla más importante del servicio fuera del código, fuera del
  control de versiones útil y fuera del alcance de los tests. Además rompería
  la premisa de que la única fuente de verdad del esquema son las migraciones
  de EF Core (ADR-0002).
- **Un semáforo o bloqueo distribuido (Redis, advisory lock por producto)** —
  añade una dependencia nueva al repo para emular en la red el `Lock` que
  estamos quitando, cuando la base de datos ya ofrece la atomicidad necesaria.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T09, T11, T21 · Ver también: ADR-0004_
