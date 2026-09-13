# 0006. Materializar `OrderLine` con un constructor privado sin parámetros, igual que `Order`

## Estado

Aceptada · Reemplaza a [0003](./0003-mapeo-del-agregado-order.md) ·
Reemplazada por [0007](./0007-money-como-clase-para-ownsone.md) en el
mecanismo de mapeo de `Money` dentro de `OrderLine` (pasa de propiedad
compleja a `OwnsOne` sobre `Money` como clase). El constructor privado sin
parámetros de `OrderLine` decidido aquí sigue vigente sin cambios: el 0007
lo da por hecho y no lo sustituye.

## Contexto

El ADR-0003 dejó explícitamente abierto un riesgo técnico: si EF Core 10.0.12
no soportaba `Money` como propiedad compleja anidada dentro de la colección
propiedad `Lines`, la decisión de mapeo se reabriría, con dos salidas
candidatas ya escritas en ese ADR —serializar `Lines` a `jsonb` con
`ToJson()`, o subir la divisa a la cabecera del pedido y dejar en la línea
sólo el importe—, ninguna de las dos elegible sobre la marcha.

El Paso 4.1 de `docs/plan.md` ha ejecutado esa verificación y el riesgo se ha
confirmado, con una causa más profunda de lo que el ADR-0003 anticipaba:

- `OwnedNavigationBuilder` (el tipo que configura `OwnsMany(o => o.Lines)`)
  **no expone `ComplexProperty`**. La salida "propiedad compleja" ni siquiera
  es expresable dentro de una colección propiedad; no es que EF la rechace,
  es que la API no la ofrece ahí.
- Donde `ComplexProperty` sí es expresable —`ComplexCollection(o => o.Lines)`,
  novedad de EF Core 10, con `OrderLine` mapeado como entidad normal en su
  propia tabla— el modelo falla igual al arrancar, con el mismo error de
  fondo: *"No suitable constructor was found… Cannot bind 'unitPrice'… only
  mapped properties can be bound to constructor parameters"*.
- La salida candidata número uno del propio ADR-0003, `OwnsMany(...).ToJson()`,
  fue probada y falla con **exactamente el mismo error**. La serialización a
  `jsonb` no evita el problema: EF sigue necesitando materializar
  `OrderLine` para serializarlo, y tropieza en el mismo sitio.
- `OwnsOne(l => l.UnitPrice)` dentro de la configuración de la línea no llega
  a compilar (CS0452, "no se puede convertir tipo por valor a tipo por
  referencia"), tal y como el ADR-0003 ya preveía para cualquier `OwnsOne`
  sobre un `readonly record struct`.

La causa exacta: `OrderLine` es un `sealed record` sin constructor sin
parámetros, con propiedades de sólo lectura (`{ get; }`, sin `set`) asignadas
únicamente en el constructor con parámetros. Sin una vía alternativa para
materializarlo, EF **tiene que** enlazar por constructor, y una propiedad
compleja —dos columnas que se combinan en un solo valor— no es enlazable
como un único parámetro de constructor. Esto no es una limitación de
`OwnsMany` en concreto: es una limitación de cómo se puede construir
`OrderLine`, y por eso ninguna de las tres formas de anidar `Lines` la
esquiva.

**Dato verificado experimentalmente**, y el que reabre la decisión con una
salida nueva que el ADR-0003 no contemplaba: un tipo espejo, idéntico a
`OrderLine` salvo por tener además un **constructor privado sin parámetros**,
mapea sin problema y produce exactamente el esquema que el ADR-0003 quería:
`unit_price_amount numeric(18,2)` + `unit_price_currency char(3)`. Es la
misma vía que ya usa `Order.Total` —un `Money` en la cabecera, fuera de la
colección, que mapea bien porque `Order` ya tiene ese constructor privado— y
la misma vía que el propio `Order` ya usa para materializarse a sí mismo.

Ese último punto es el dato que pesa en la decisión: `Order` **ya tiene** un
constructor privado sin parámetros, puesto ahí explícitamente para EF Core,
comentado, commiteado y aceptado. El ADR-0003 afirma que "el dominio se queda
intacto" y a la vez documenta esa cesión como parte del estado ya aceptado
del agregado. Hoy `OrderLine` es la única pieza del agregado que no sigue ese
patrón — no porque el patrón esté mal, sino porque nadie lo había necesitado
todavía en la línea.

## Decisión

**`OrderLine` gana un constructor privado sin parámetros, exactamente con el
mismo propósito y la misma justificación que el de `Order`, y EF Core lo usa
para materializarla accediendo a sus propiedades por el campo de respaldo.
`Money` sigue siendo una propiedad compleja de dos columnas tanto en
`Order.Total` como en `OrderLine.UnitPrice` — el ADR-0003 no se corrige en
esa parte, se completa.**

Con esto, `Lines` se sigue mapeando como colección propiedad (`OwnsMany`) en
su propia tabla `order_lines`, mapeada por el campo `_lines` con clave shadow
por defecto, tal y como decidió el ADR-0003; lo único que cambia respecto a
ese ADR es cómo se materializa `OrderLine` dentro de esa colección.

Por qué esta salida y no las otras dos que el propio ADR-0003 había dejado
como candidatas, ni la que introduce esta reapertura (`OrderLine` como
clase) ni el conversor de una sola columna:

- **Es la que menos le cuesta al dominio.** Es una línea de código —un
  constructor privado sin cuerpo, comentado igual que el de `Order`—, no un
  cambio de forma del tipo. `OrderLine` sigue siendo un `record` inmutable
  desde fuera, sigue sin exponer setters públicos, sigue validando
  `quantity > 0` en su único constructor público, y `Orders.Domain.csproj`
  sigue sin ganar ninguna `PackageReference`: nada de esto usa un atributo ni
  un tipo de EF Core, así que el test de frontera de T31 no se entera del
  cambio.
- **Es la que menos le cuesta al esquema y a las consultas futuras.** No
  toca la decisión de dos columnas consultables del ADR-0003. `order_lines`
  sigue teniendo `unit_price_amount numeric(18,2)` y `unit_price_currency
  char(3)` como columnas propias, sumables y filtrables en SQL —"cuánto
  importe hay comprometido en líneas del producto X" sigue siendo una
  consulta SQL normal, no una consulta sobre JSON ni una que asuma una
  divisa fija.
- **No es una excepción nueva, es la generalización de una que ya existe.**
  `Order` ya cede un constructor privado a la infraestructura, ya aceptado
  por el usuario y ya commiteado. Extender el mismo gesto a `OrderLine`
  dentro del mismo agregado no abre una puerta nueva: cierra la única
  grieta por la que ese agregado no seguía su propio patrón. Es más
  defendible en una revisión que introducir un mecanismo distinto
  (conversor, cambio de tipo) para resolver el mismo problema que `Order`
  ya resolvió de otra forma dentro del mismo fichero.
- **Está verificado, no es una apuesta.** El tipo espejo con el constructor
  privado ya se probó y produce el esquema exacto que se quería. Las otras
  rutas (`ComplexCollection`, `ToJson()`, `OwnsOne`) se probaron primero y
  fallaron con evidencia concreta, no por descarte teórico.

## Consecuencias

**Más fácil**

- El agregado `Order` queda con una regla de materialización consistente en
  sus dos niveles (cabecera y línea), en vez de dos mecanismos distintos
  para el mismo problema (`Money` embebido).
- El esquema de `order_lines` sale exactamente como el ADR-0003 lo diseñó:
  columnas con nombre propio, importe y divisa consultables por separado,
  sin tocar ninguna de las demás reglas de mapeo de ese ADR (IDs, `Status`,
  `DomainEvents`, clave shadow de `order_lines`).
- No hace falta reabrir ni reescribir la configuración de `Order.Total`, que
  ya funcionaba: la solución es simétrica y usa el mismo mecanismo, no uno
  nuevo en paralelo.

**Más difícil / lo que se asume**

- La materialización de `OrderLine` va a depender de que EF Core acceda a
  sus propiedades por el campo de respaldo generado por el compilador para
  cada auto-propiedad de sólo lectura (`<ProductId>k__BackingField`, etc.),
  no por un setter —`OrderLine` no tiene ninguno—. El tipo espejo probado
  funcionó por convención, sin configuración explícita adicional, pero es
  una convención menos visible que un `private set`: al `implementer` le
  toca decidir en el Paso 4.2 si confía en el descubrimiento automático de
  EF o si fija el acceso por campo explícitamente en la configuración
  (`UsePropertyAccessMode(PropertyAccessMode.Field)` o `HasField(...)` por
  propiedad) para que quede documentado en el código y no dependa de un
  comportamiento implícito. Esta es una decisión de implementación, no de
  diseño, y no se cierra aquí.
- Igual que ya ocurre con `Order` desde el ADR-0003 original: la validación
  de `OrderLine` (`quantity` mayor que cero) vive en el constructor público
  y **no se re-ejecuta** cuando EF materializa una línea leída de la base de
  datos. Esto no es una regresión nueva de este ADR —ya era cierto para
  `Order.Place` y sus invariantes de cabecera—, pero conviene decirlo
  explícitamente: la garantía de validez de una línea depende de que nunca
  se escriba una fila inválida en `order_lines`, no de que el dominio la
  revalide en cada lectura.
- El constructor privado sin parámetros dejará a `OrderLine` sin ninguna
  validación posible si alguna vez se usa reflexión o serialización fuera
  del control de EF Core para instanciarla. El riesgo es el mismo que ya
  asume `Order` hoy, así que no es una novedad, pero se hereda.

**Cuándo revisar esta decisión**

Si una versión futura de EF Core añade `ComplexProperty` a
`OwnedNavigationBuilder` o a `ComplexCollectionBuilder` de forma que
`OrderLine` pueda mapearse sin ceder un constructor —lo que convertiría este
ADR en innecesario—, o si el acceso por campo demuestra ser frágil en la
práctica (por ejemplo, si una renombración de propiedad rompe el
descubrimiento por convención sin que ningún test lo detecte), momento en el
que la alternativa a valorar sería fijar el acceso por campo de forma
explícita en vez de depender de la convención.

## Alternativas descartadas

- **Esperar a una versión de EF Core que soporte `ComplexProperty` dentro de
  una colección propiedad** — es la salida que no le cuesta nada al dominio
  ni al esquema, y por eso es la más tentadora. Se descarta por calendario:
  este es un portfolio con una demo que tiene que funcionar con la versión
  ya fijada (EF Core 10.0.12), no con una hipotética futura. Si esa versión
  llega, este ADR se revisa entonces (ver "Cuándo revisar esta decisión"),
  pero no se bloquea T11 a la espera de ella.
- **Convertir `OrderLine` de `record` a clase con setters privados** —
  resolvería el mismo problema de materialización (EF podría usar el
  constructor sin parámetros más los setters, en vez de campo), pero paga
  un precio que la opción elegida no paga: pierde la igualdad estructural
  que un `record` da gratis. Aunque hoy ningún test se apoya en esa
  igualdad para comparar líneas directamente (`OrderLine` sólo se usa hoy
  como valor de fábrica en los tests unitarios de `Order`), es una garantía
  del tipo que desaparece sin necesidad: la opción elegida obtiene el mismo
  resultado de persistencia sin renunciar a que dos líneas con los mismos
  valores sean iguales por definición. Cambiar la naturaleza del tipo para
  resolver un problema de infraestructura es exactamente lo que el
  ADR-0003 ya rechazó para otras piezas del agregado (ver su alternativa
  "exponer `Guid` en el dominio"): la comodidad de persistencia no debería
  decidir la forma del dominio cuando hay una opción que no lo exige.
- **Un conversor de `Money` a una sola columna dentro de `OrderLine`** — es
  la alternativa que el propio ADR-0003 ya había descartado para
  `Order.Total`, con el mismo razonamiento aplicado ahora a la línea: una
  columna de texto (`"12.50 EUR"`) hace imposible sumar importes en SQL, y
  una columna `decimal` que asuma una divisa fija pierde la divisa sin
  ningún sitio donde reconstruirla al leer. Dentro de `order_lines` el
  coste es si acaso mayor que en la cabecera: es la tabla donde con más
  probabilidad aparece una consulta futura del tipo "importe total
  comprometido del producto X", que dejaría de ser una consulta SQL directa.
  Se descarta por la misma razón que ya valía para `Order.Total`, no por una
  nueva.
- **Subir la divisa a la cabecera del pedido y dejar en la línea sólo el
  importe** — era la segunda salida candidata que el propio ADR-0003 había
  dejado escrita para este escenario. Se descarta ahora que existe una
  alternativa que no la necesita: cambiaría el modelo de dominio (una línea
  dejaría de tener su propio `Money` completo, y el agregado tendría que
  imponer "todas las líneas comparten la divisa del pedido" como invariante
  nueva) para resolver un problema que el constructor privado resuelve sin
  tocar la forma de `OrderLine` en absoluto. Pagar ese cambio de modelo ya
  no está justificado una vez que la opción elegida está verificada.
- **`OwnsMany(...).ToJson()`** — ya estaba descartada como opción por
  defecto en el ADR-0003 (deja las líneas fuera del alcance de SQL) y ahora
  además está descartada como opción de emergencia: se probó y falla con el
  mismo error de enlace de constructor que las demás rutas anidadas. No es
  una alternativa viable hoy, ni siquiera como salida de último recurso.
- **`OrderLine` como entidad independiente con su propia clave y su propio
  `DbSet`** — sigue teniendo el mismo defecto que el ADR-0003 ya le
  encontró: permitiría consultar y modificar líneas sin pasar por el
  agregado, y obligaría a inventar una identidad (`OrderLineId`) que el
  dominio no necesita. Nada de lo averiguado en esta reapertura cambia esa
  valoración.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T11, Paso 4.1 de `docs/plan.md` · Reemplaza a: ADR-0003_
