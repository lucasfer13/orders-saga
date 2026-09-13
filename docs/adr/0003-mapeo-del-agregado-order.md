# 0003. Mapear el agregado `Order` desde `Orders.Api`, sin llevar EF Core al dominio

## Estado

Reemplazada por [0006](./0006-constructor-privado-en-orderline.md) en la
parte de materialización de `OrderLine` (el punto de riesgo técnico descrito
más abajo, en "Consecuencias"). El resto de reglas de mapeo de este ADR
—IDs, `Order.Total`, `Status`, `DomainEvents`, clave de `order_lines`— sigue
vigente y no se repite en el 0006.

## Contexto

`Orders.Domain` es la excepción justificada del repo: mientras `Inventory`,
`Payments` y `Shipping` son slices finos, `Orders` tiene un agregado con
invariantes reales y dependencias hacia adentro. Su `.csproj` no tiene hoy
ninguna `PackageReference`, y T31 va a escribir un test que falle si aparece
una: el dominio no referencia infraestructura, EF Core ni MassTransit.

El agregado, tal y como está escrito, no es trivial de persistir:

- `OrderId`, `CustomerId` y `ProductId` son `readonly record struct` que
  envuelven un `Guid`. EF Core no sabe leerlos sin ayuda.
- `Money` es un `readonly record struct` con `decimal Amount` y
  `string Currency`. Al ser un tipo por valor, **no puede ser un tipo
  propiedad** (*owned*): EF Core exige que los tipos propiedad sean tipos por
  referencia.
- `Lines` se expone como `IReadOnlyCollection<OrderLine>` calculado
  (`_lines.AsReadOnly()`) sobre un `List<OrderLine>` privado. No hay setter ni
  colección mutable pública que EF pueda rellenar.
- `OrderLine` es un `sealed record` con propiedades de sólo lectura y un único
  constructor con parámetros, que además valida.
- `DomainEvents` es otra colección de sólo lectura, pero no es estado
  persistente: es una lista de eventos acumulados para publicarlos.
- Hay un constructor privado sin parámetros puesto ahí explícitamente para la
  materialización de EF Core.
- `Status` es un `enum` con seis valores y transiciones válidas definidas en
  el agregado.

Hay que decidir dónde vive la configuración de persistencia y cómo se traduce
cada una de esas piezas, antes de que se escriba la primera migración: una
migración inicial equivocada se arrastra en todas las siguientes.

## Decisión

**Toda la configuración de EF Core del agregado vive en `Orders.Api`, en una
implementación de `IEntityTypeConfiguration<Order>`; `Orders.Domain` no cambia
ni una línea y no gana ninguna referencia.** El dominio ya expone lo necesario
para que EF trabaje —constructor privado, setters privados, campos de
respaldo— sin saber que EF existe.

Las reglas de traducción, que son la parte sustantiva de la decisión:

- **IDs fuertemente tipados → conversores de valor registrados una sola vez**,
  en `ConfigureConventions` del `DbContext` (configuración previa a las
  convenciones), no propiedad a propiedad. Cada ID se almacena como `uuid` y
  se marca como generado nunca por la base: el identificador lo crea el
  dominio (`OrderId.New()`), no una secuencia. Registrarlos por convención y
  no a mano es lo que hace que `Where(o => o.Id == id)` se traduzca a SQL en
  lugar de evaluarse en cliente.
- **`Money` → propiedad compleja, con dos columnas**: importe
  (`numeric(18,2)`) y divisa (`char(3)`). Deliberadamente **no** se usa un
  conversor a una sola columna de texto: un importe tiene que poder filtrarse,
  sumarse y compararse en SQL, y una columna `"12.50 EUR"` lo impide. La
  precisión se fija explícitamente porque el `decimal` de .NET sin precisión
  declarada produce un `numeric` sin restricción en PostgreSQL, y la precisión
  del dinero no se deja al azar.
- **`Lines` → colección propiedad (`OwnsMany`) en su propia tabla**
  (`order_lines`), mapeada **por el campo de respaldo** `_lines`, con modo de
  acceso por campo. La propiedad pública devuelve un envoltorio de sólo
  lectura al que EF no puede añadir elementos; el campo sí. `OrderLine` se
  materializa por su constructor con parámetros (EF enlaza parámetros por
  nombre), lo que mantiene intacta su validación.
- **La clave de `order_lines` es la clave shadow por defecto de EF Core**
  (pedido + índice de la línea), no una clave de negocio. Usar
  `(order_id, product_id)` sería más bonito en el esquema, pero impondría una
  invariante que el agregado no tiene hoy —"un producto no puede aparecer en
  dos líneas del mismo pedido"—, y las invariantes de `Order` se declaran en
  `Order`, no en un `ALTER TABLE`.
- **`DomainEvents` → ignorado.** No es estado. Qué se hace con esos eventos
  (recogerlos al guardar y escribirlos en la tabla outbox) es T15, y es una
  decisión distinta que tendrá su propio ADR.
- **`Status` → texto, no entero.** Un `enum` mapeado por su valor numérico
  convierte cualquier reordenación futura del `enum` en una corrupción
  silenciosa de datos históricos, y hace ilegible cualquier consulta manual
  durante una demo. El coste son unos bytes por fila y una comparación de
  cadenas en los índices; para el volumen de este sistema es irrelevante.

## Consecuencias

**Más fácil**

- El dominio sigue siendo testeable sin infraestructura y el test de frontera
  de T31 pasa sin excepciones ni listas blancas.
- El esquema resultante es legible: columnas con nombre propio, importes
  consultables, estados en texto. Cuenta bien en una captura del README.
- Cambiar de proveedor de persistencia afecta a un fichero de configuración y
  a una implementación de repositorio (ADR-0004), no al agregado.

**Más difícil / lo que se asume**

- La configuración es verbosa y hay que escribirla entera a mano. Es el precio
  de no ensuciar el dominio con atributos, y conviene decirlo en el README en
  lugar de fingir que sale gratis.
- **El punto de riesgo técnico de T11 es `Money` dentro de `OrderLine`**: una
  propiedad compleja anidada dentro de una colección propiedad. El mapeo del
  `Money` de la cabecera (`Order.Total`) es directo; el de la línea depende de
  que EF Core 10.0.12 soporte esa combinación. **Es lo primero que hay que
  verificar con un test**, antes de escribir la migración inicial (ver
  `docs/plan.md`). Si no estuviera soportado, la decisión de mapeo se
  reabre —las dos salidas candidatas son serializar la colección de líneas a
  una columna `jsonb`, o subir la divisa a la cabecera del pedido y dejar en
  la línea sólo el importe— y ninguna de las dos se toma sin volver aquí: la
  primera cambia cómo se consultan las líneas, la segunda cambia el modelo de
  dominio.
- Persistir un agregado con su colección implica cargarla explícitamente al
  leer. Con colecciones propiedad EF lo hace solo, pero la carga diferida
  sigue desactivada a propósito: una consulta que dispara SQL sin que se vea
  en el código es exactamente el tipo de cosa que rompe una medición de T36.

**Cuándo revisar esta decisión**

Si el agregado crece hasta el punto de que la configuración a mano sea la
parte más frágil del servicio, o si aparece una consulta de lectura que el
modelo de escritura no sepa responder con un coste razonable (ahí la respuesta
sería un modelo de lectura aparte, no deformar el agregado).

## Alternativas descartadas

- **Exponer `Guid` en el dominio y olvidarse de los conversores** — elimina
  todo el problema de mapeo de un plumazo, y a cambio elimina también la razón
  por la que los IDs fuertemente tipados existen: que `Reserve(orderId,
  productId)` no compile si alguien intercambia los argumentos. El agregado ya
  está escrito así y funciona; rebajarlo por comodidad de persistencia es
  dejar que la infraestructura decida el modelo.
- **Atributos de EF Core (`[Owned]`, `[Table]`, `[Column]`) sobre las clases
  del dominio** — la opción más corta, y la que hace que `Orders.Domain`
  referencie EF Core. Contradice la frontera que el repo afirma y que T31
  verifica. Se descarta por definición, no por gusto.
- **Un modelo de persistencia separado (`OrderRecord`) con un mapeador hacia y
  desde el agregado** — es la alternativa seria: desacopla del todo el esquema
  del modelo y es lo que haría falta si el esquema fuera heredado. Aquí
  significaría duplicar cinco tipos y mantener un mapeador bidireccional a
  mano, incluida la reconstrucción del estado privado del agregado, para un
  esquema que nosotros mismos definimos y que no tiene por qué diferir. Coste
  permanente, beneficio hipotético.
- **Serializar las líneas a una columna `jsonb` con `ToJson()`** — encaja
  conceptualmente (las líneas no tienen identidad fuera del pedido) y evita el
  problema de la clave de la tabla hija. Se descarta como opción por defecto
  porque deja las líneas fuera del alcance de SQL: "cuántas unidades del
  producto X hay comprometidas" pasa a ser una consulta sobre JSON, y el
  esquema deja de contar la historia por sí solo al mirarlo. Queda como
  candidata explícita si el mapeo anidado no fuera viable.
- **`OrderLine` como entidad independiente con su propia clave y su propio
  `DbSet`** — permitiría consultar y modificar líneas sin pasar por el
  agregado, que es justo lo que un agregado existe para impedir. Además
  obligaría a inventar una identidad (`OrderLineId`) que el dominio no
  necesita.
- **Un conversor de `Money` a una sola columna** (texto tipo `"12.50 EUR"` o
  `decimal` asumiendo divisa fija) — la primera forma hace imposible sumar en
  SQL; la segunda pierde la divisa, y los conversores no tienen estado con el
  que reconstruirla al leer.
- **Dejar que EF genere el esquema con sus convenciones por defecto** — ahorra
  un rato y produce un `decimal` sin precisión declarada, un `Status` numérico
  y nombres de columna que dependen de cómo se llamen hoy las propiedades de
  C#. Las tres cosas se pagan después, en una migración correctiva.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T08, T11, T31 · Ver también: ADR-0004_
