# 0007. Convertir `Money` de `record struct` a `record class` para poder mapear `OrderLine.UnitPrice` con `OwnsOne`

## Estado

Aceptada · Reemplaza a [0006](./0006-constructor-privado-en-orderline.md) en
el mecanismo de mapeo de `Money` dentro de `OrderLine`. El constructor
privado sin parámetros que introdujo el 0006 sigue vigente y sigue haciendo
falta: esta decisión no lo sustituye, resuelve el problema que quedaba
después de él.

## Contexto

Tras el ADR-0006, `OrderLine` ya se materializa (constructor privado +
acceso por campo), pero `Lines` sigue sin mapearse:
`src/Orders.Api/Persistence/Configurations/OrderConfiguration.cs` tiene
`builder.Ignore(order => order.Lines)`, con un comentario que documenta lo
probado:

- `OwnedNavigationBuilder` (el builder de `OwnsMany`) **no expone
  `ComplexProperty`**: no hay forma de declarar `Money` como tipo complejo
  anidado desde la API fluida ahí dentro.
- Llegar a los metadatos por debajo (`IMutableTypeBase.AddComplexProperty`)
  construye el modelo y genera SQL correcto, pero el generador de snapshots
  de EF emite después `b1.ComplexProperty(...)` sobre `OwnedNavigationBuilder`
  en el `Designer.cs`/`ModelSnapshot.cs` checked-in, una llamada que no
  existe ahí tampoco — el código generado no compila. Callejón sin salida
  verificado, no teórico.
- `OwnsMany(...).ToJson()` compila y migra limpio con el constructor privado
  en su sitio, pero serializa las líneas a una columna `jsonb`, perdiendo la
  consultabilidad SQL que el ADR-0003 exige explícitamente para los
  importes.

La hipótesis a verificar: en la primera ronda, `OwnsOne(l => l.UnitPrice)`
"ni compila (CS0452)". Ese error de C# significa "el argumento de tipo debe
ser un tipo por referencia". Las firmas reales de EF Core 10 son:

```csharp
OwnedNavigationBuilder<TEntity, TDependentEntity> OwnsOne<TNewRelatedEntity>(...)
    where TNewRelatedEntity : class
EntityTypeBuilder<TEntity> OwnsOne<TRelatedEntity>(...)
    where TRelatedEntity : class
```

**Se confirma la hipótesis.** `OwnsOne`/`OwnsMany` restringen su parámetro de
tipo a `class` porque un tipo *owned* se registra como su propia
`IMutableEntityType`, con la maquinaria de *change tracking* que eso implica,
y esa maquinaria exige tipo por referencia. `ComplexProperty` (EF Core 8+) es
precisamente el mecanismo que se introdujo para no pagar ese precio con
tipos valor — y es por lo que `Order.Total`, mapeado con `ComplexProperty`
directamente sobre `EntityTypeBuilder<Order>` (que sí lo expone), funciona
hoy sin problema con `Money` como struct.

El hilo se cierra así: el bloqueo no ha sido nunca "EF no soporta anidar
`Money`" en abstracto — ha sido que la única vía disponible dentro de una
colección *owned* (`OwnsOne`) exige un tipo por referencia, y se ha estado
intentando esquivarlo por la vía que sí acepta structs (`ComplexProperty`),
que resulta no estar expuesta ahí. Dos rondas atacando cómo declarar el tipo
complejo dentro de `OwnsMany`, cuando el problema real era el tipo de
`Money`.

## Decisión

**`Money` pasa de `readonly record struct` a `sealed record class`.
`OrderLine.UnitPrice` se mapea con `OwnsOne`, anidado dentro de
`OwnsMany(order => order.Lines)`, ahora posible porque `Money` cumple la
restricción `where TNewRelatedEntity : class`. `Order.Total` no cambia de
mecanismo: sigue mapeado con `ComplexProperty` sobre `EntityTypeBuilder<Order>`,
que ya funcionaba y no depende de esta restricción.**

Detalles que forman parte de la decisión:

- El constructor público de `Money` (`Money(decimal amount, string
  currency = "EUR")`) no necesita cambiar. Sus dos parámetros son escalares
  simples —no tipos anidados—, así que EF los enlaza por constructor sin el
  problema que sí tuvo `OrderLine` en el ADR-0006: allí el parámetro
  bloqueado era él mismo un tipo complejo (`Money unitPrice`), no un
  escalar. `Money` no necesita, y no gana, un constructor privado sin
  parámetros.
- El esquema resultante no cambia respecto a lo que el ADR-0003 pedía:
  `order_lines` sigue con `unit_price_amount numeric(18,2)` y
  `unit_price_currency char(3)`, consultables y sumables en SQL. Lo que
  cambia es el mecanismo fluido usado para llegar ahí (`OwnsOne` en vez de
  `ComplexProperty`), no el resultado.
- `Order.Total` se deja explícitamente sin tocar: sigue en
  `builder.ComplexProperty(order => order.Total, ...)`, que ya está
  verificado en verde. `ComplexProperty` acepta tipos por referencia igual
  que por valor, así que el cambio de `Money` a clase no lo rompe; sólo dos
  cosas a confirmar en la migración (no son una decisión nueva, son un
  detalle de implementación): que la propiedad siga marcada como requerida
  explícitamente en la configuración, y que la migración generada no cambie
  el tipo de columna ya aplicado.

Por qué esta salida y no las otras tres vivas en este momento (`ToJson`,
dejar `Lines` sin mapear, descomponer `Money` en dos primitivas sólo dentro
de `OrderLine`):

- **Es la única que no renuncia a nada de lo que el ADR-0003 pidió.** Ni
  columna JSON, ni líneas sin persistir, ni un `Money` que sólo existe entero
  en la cabecera y roto en la línea.
- **Es la más barata para el dominio de las que sí resuelven el problema de
  verdad.** Un cambio de palabra clave (`struct` → `class`) sobre un tipo
  que ya es inmutable por diseño (sin setters, sólo `{ get; }`), no una
  reestructuración de `OrderLine` ni de `Order`.
- **Es simétrica con el precedente que ya sentó el ADR-0006**: igual que
  `Order` ya cedía un constructor privado a la infraestructura sin dejar de
  ser el mismo agregado, `Money` cede su naturaleza de struct sin dejar de
  ser el mismo value object —mismos operadores, misma validación, misma
  igualdad por valor—.

## Consecuencias

**Más fácil**

- `Lines` queda mapeada de verdad, con importes de línea consultables en
  SQL: "importe comprometido del producto X" sigue siendo una consulta SQL
  normal sobre `order_lines`, no sobre JSON.
- Un solo tipo `Money` para toda la cabecera y todas las líneas, sin
  duplicar el concepto en un tipo de persistencia paralelo ni en primitivas
  sueltas.
- `Order.Total`, ya verificado, no se retoca: el cambio es aditivo sobre la
  parte que faltaba, no una reapertura de lo que ya funcionaba.

**Más difícil / lo que se asume**

- **`Money` deja de ser un tipo que el compilador garantiza no-nulo por
  construcción.** Un `readonly record struct` no admite `null` salvo
  envuelto en `Money?`; un `record class` sí lo admite salvo que las
  anotaciones de tipos de referencia nulables lo marquen y algo lo haga
  cumplir. Con `Nullable` activado en todo el repo y
  `TreatWarningsAsErrors` (`Directory.Build.props`), una asignación nula
  directa a `Money` sigue fallando en compilación como advertencia
  ascendida a error — no es una garantía tan fuerte como la del struct, pero
  tampoco queda sin red. Donde sí hace falta ser explícito es en el mapeo:
  tanto `OrderLine.UnitPrice` como `Order.Total` deben marcarse `IsRequired()`
  a mano en la configuración, en vez de depender de que EF infiera lo mismo
  que ya infería gratis con un struct. Es una línea de configuración por
  propiedad, no una redefinición de reglas.
- Se pierde la semántica de "tipo por valor" que comunicaba `Money` como
  concepto — que dos copias no comparten identidad y no hay una referencia
  que pueda ser `null` por descuido. La igualdad estructural del `record` se
  conserva en ambos casos (clase o struct), así que ningún test existente
  de `Money` ni de los eventos de dominio que lo incluyen (`OrderPlaced`,
  `OrderPaymentCharged`) se ve afectado — se revisó
  `tests/unit/OrdersSaga.UnitTests/Domain/ValueObjectsTests.cs` y
  `OrderTests.cs`, y ninguno depende de que `Money` sea un struct.
- Es una asunción más de infraestructura sobre la forma del dominio, en la
  misma línea que el ADR-0006: el dominio cede una propiedad de su tipo
  (aquí, ser struct) para que la persistencia sea expresable con la API
  estable de EF Core, en vez de con metadatos internos o JSON. No cambia
  ninguna regla de negocio ni ninguna validación de `Money`.

**Cuándo revisar esta decisión**

Si una versión futura de EF Core expone `ComplexProperty` en
`OwnedNavigationBuilder` (lo que dejaría de exigir que `Money` sea una
clase para mapear `OrderLine.UnitPrice`), valdría la pena reconsiderar
volver a `readonly record struct`, con el coste de otra migración y otro
ADR — no antes de que esa vía exista de verdad.

## Alternativas descartadas

- **Mantener `OwnsMany(...).ToJson()`** — es la única alternativa que hoy
  compila y migra sin cambiar `Money`. Se descarta porque el ADR-0003 ya
  rechazó explícitamente serializar `Lines` a `jsonb` por la misma razón que
  sigue aplicando: deja los importes de línea fuera del alcance de SQL.
  Adoptarla ahora que existe una vía que no paga ese precio sería elegir la
  opción peor a propósito.
- **Dejar `Lines` sin mapear indefinidamente** — es lo que hay hoy en
  `OrderConfiguration.cs`, y no es una alternativa real: rompe el criterio
  de terminado de T11 (test de ida y vuelta del agregado completo,
  incluidas las líneas) y deja `Orders` sin poder persistir un pedido de
  verdad, que es el propósito del paso.
- **Descomponer `Money` en dos propiedades primitivas
  (`UnitPriceAmount`, `UnitPriceCurrency`) sólo dentro de `OrderLine`,
  dejando `Money` como struct en el resto del agregado** — evita tocar el
  tipo `Money`, pero fragmenta el value object exactamente en el sitio
  donde más se usa (las líneas, no la cabecera), y deja el mismo concepto
  representado de dos formas distintas dentro del mismo agregado: `Money`
  entero en `Order.Total`, dos primitivas sueltas en `OrderLine`. Habría
  que reconstruir un `Money` a mano en `LineTotal` y en cualquier código
  que hoy trata `UnitPrice` como un valor único. Es la misma inconsistencia
  de mecanismo que el ADR-0006 ya evitó al dar a `OrderLine` el mismo
  patrón de materialización que `Order`, aquí aplicada al revés y con más
  coste: cambia la forma pública de `OrderLine`, no sólo su
  materialización interna.
- **Volver a los metadatos internos (`IMutableTypeBase.AddComplexProperty`)
  a pesar del fallo del generador de snapshots** — ya verificado como
  callejón sin salida: el modelo construye, pero el código generado del
  snapshot no compila. No es una alternativa viable, es la misma vía que
  ya falló.
- **Un modelo de persistencia paralelo para `Money`** (un tipo de
  infraestructura con las mismas dos propiedades, mapeador manual hacia y
  desde el `Money` del dominio) — es la misma alternativa que el ADR-0003
  ya descartó en general ("un modelo de persistencia separado con un
  mapeador"): duplica el tipo y exige mantener un mapeador bidireccional a
  mano para un esquema que el propio repo define, a cambio de no cambiar
  una palabra clave en un tipo que ya es inmutable. Coste permanente,
  beneficio hipotético — la misma razón que ya valía en 0003.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T11, Paso 4.2 de `docs/plan.md` · Reemplaza a: ADR-0006 (mecanismo de mapeo de `Money` en `OrderLine`) · Ver también: ADR-0003_
