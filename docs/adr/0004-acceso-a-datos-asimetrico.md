# 0004. Usar repositorio en `Orders` y `DbContext` directo en los slices finos

## Estado

Aceptada

## Contexto

`ESTANDAR-CALIDAD.md` (sección 8) y el agente `architecture-guard` listan,
entre las reglas a verificar en cada build, una escrita sin matices: **"los
handlers no usan `DbContext` directamente"**.

`CLAUDE.md` afirma, con la misma rotundidad, que `Inventory`, `Payments` y
`Shipping` son slices finos, "casi transaction script": sus endpoints y
consumidores llaman directamente a su handler, sin mediador, sin behaviors y
sin ceremonia.

Las dos afirmaciones no pueden cumplirse a la vez tal y como están escritas.
Un transaction script al que se le prohíbe tocar el `DbContext` necesita un
repositorio; un repositorio de un solo método que sólo reenvía la llamada al
`DbContext` es la ceremonia que el transaction script existía para evitar.

La decisión no se puede posponer: T11 es la tarea que introduce el
`DbContext`, y T31 es la que escribe el test que convierte esa regla en algo
verificado. Si la frontera no está definida antes, el test se escribirá con la
versión global de la regla y fallará en tres servicios, o se escribirá
relajado y no verificará nada.

## Decisión

**La regla se aplica sólo a `Orders`. En `Inventory`, `Payments` y `Shipping`
los handlers usan su `DbContext` directamente, y eso es lo correcto.**

En `Orders`:

- `Orders.Domain` declara el puerto `IOrderRepository`, con firmas que sólo
  mencionan tipos del propio dominio (`Order`, `OrderId`) y
  `CancellationToken`. Es una dependencia hacia adentro: el dominio define lo
  que necesita, la infraestructura lo cumple.
- `Orders.Api` implementa ese puerto sobre `OrdersDbContext`. Esa
  implementación es **el único tipo de la lógica de aplicación del servicio que
  ve el `DbContext`**. Lo ven además, y no hay forma de evitarlo, el propio
  contexto, la fábrica de diseño que usa `dotnet ef`, el migrador de arranque,
  las migraciones que genera EF y el registro del healthcheck: son fontanería
  de EF y del host, no código que manipule el agregado.
- Ningún handler de `Orders` inyecta `OrdersDbContext`, ni un `DbSet`, ni
  ninguna interfaz que los envuelva.
- El repositorio **no** expone `SaveChangesAsync` ni abre transacciones: el
  commit es trabajo del `TransactionBehavior` del pipeline (T12), según la
  sección 3 del estándar. En T11 todavía no hay behaviors ni camino de
  escritura en `Orders`, así que el único punto que guarda explícitamente es
  el test de integración que verifica el mapeo.

En `Inventory`, `Payments` y `Shipping`:

- El handler recibe su `DbContext` por constructor y trabaja con él.
- No hay repositorios, ni interfaces por servicio, ni `IUnitOfWork`.
- El handler sí guarda sus cambios: no hay pipeline que lo haga por él, y no
  se va a introducir uno sólo para respetar una regla pensada para otro
  contexto.

La razón de fondo es que la regla no es un fin en sí misma. Existe para
proteger un agregado: que nadie pueda modificar un `Order` esquivando sus
invariantes con un `UPDATE` desde un handler. En un servicio cuyo modelo son
tres columnas y ninguna invariante de dominio, esa protección no protege nada
y sólo añade indirección.

`architecture-guard` escribe, por tanto, **dos** reglas y no una:

1. Ningún tipo de `Orders.Api` depende de `OrdersDbContext` salvo la
   implementación del repositorio y la fontanería enumerada arriba, que el test
   lista explícitamente para que añadir un nombre a esa lista sea una decisión
   visible en el diff y no un descuido.
2. Ningún tipo de `Inventory.Api`, `Payments.Api` o `Shipping.Api` depende del
   `DbContext` de otro servicio.

La segunda es la que de verdad importa en los slices finos, y es la que hoy no
está en la lista del estándar.

## Consecuencias

**Más fácil**

- La asimetría de arquitectura del repo deja de ser una frase del README y
  pasa a tener una consecuencia concreta y verificable en el código.
- Los slices finos siguen siendo finos: un fichero por operación, sin capas
  intermedias que no aportan.
- El agregado `Order` tiene un único punto de entrada a la persistencia, que
  es donde se controla cómo se carga con sus líneas y, más adelante, dónde se
  recogen sus eventos de dominio (T15).

**Más difícil / lo que se asume**

- Hay que explicar la excepción en dos sitios: en el test de arquitectura (con
  un nombre que diga por qué está delimitado a `Orders`) y en el README. Una
  excepción no escrita donde se mira parece un descuido.
- Quien recorra la lista de la sección 8 del estándar literalmente verá una
  regla "incumplida" en tres de los cuatro servicios. Este ADR es la respuesta
  a esa objeción, y por eso existe.
- Un handler de un slice fino puede escribir una consulta mala sin que nada lo
  intercepte. Se acepta: la alternativa —una capa que las intercepte— cuesta
  más de lo que evita a esta escala.
- La justificación arquitectónica completa de por qué `Orders` y los otros
  tres servicios no se parecen es materia del ADR de T37 ("por qué
  arquitecturas distintas en el mismo repo"). Este ADR decide sólo el punto
  concreto del acceso a datos, que hace falta ya; el otro contará la historia
  entera y enlazará aquí.

**Cuándo revisar esta decisión**

Si alguno de los tres slices finos desarrolla invariantes de verdad —y el
candidato natural es `Inventory`, si las reservas se vuelven un modelo con
estados— deja de ser un transaction script y entra en el régimen de `Orders`.

## Alternativas descartadas

- **Repositorio en los cuatro servicios, regla global sin excepciones** — es
  coherente sobre el papel y la regla se escribe en una línea. Produce tres
  interfaces de uno o dos métodos cuya única implementación reenvía al
  `DbContext`, y contradice de frente la decisión, ya tomada, de que esos
  servicios sean transaction scripts. Aplicar un patrón donde no resuelve
  ningún problema es precisamente lo que el repo dice no hacer: sería aplicar
  una checklist.
- **`DbContext` directo también en `Orders`, regla eliminada** — la variante
  simétrica por el otro extremo. Elimina una capa, y con ella el único sitio
  donde se controla que un `Order` se cargue completo y se modifique por sus
  métodos. El dominio se queda sin puerto, la dependencia deja de apuntar
  hacia adentro y el test de frontera de `Orders` deja de tener objeto: si
  cualquier handler puede hacer `context.Orders.ExecuteUpdate(...)`, las
  transiciones de estado del agregado son una sugerencia.
- **Un `IRepository<T>` genérico compartido por los cuatro servicios** —
  ahorra código y crea un tipo común del que dependen los cuatro, violando
  otra regla de la misma lista ("un slice no importa tipos de otro slice") y
  acoplando cuatro servicios que deben poder desplegarse por separado. Además
  un repositorio genérico expone `IQueryable` o `Expression`, con lo que el
  `DbContext` se filtra igualmente, sólo que disfrazado.
- **Exponer el contexto tras una interfaz (`IOrdersDbContext` con sus
  `DbSet`)** — es la solución más frecuente en repos que quieren "poder
  mockear el contexto". No cambia nada real: el handler sigue recibiendo tipos
  de EF Core y sigue pudiendo hacer cualquier cosa con ellos. Y mockear el
  contexto está prohibido en este repo por la sección 7 del estándar, así que
  ni siquiera compra lo que promete.
- **Mediador y behaviors también en los slices finos, para que el commit lo
  haga un `TransactionBehavior`** — resolvería el "el handler no abre
  transacciones" de forma uniforme, a cambio de reabrir una decisión ya
  cerrada en `CLAUDE.md` y de añadir a tres servicios un pipeline que no
  necesitan.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T11, T12, T31, T37 · Ver también: ADR-0003, ADR-0005_
