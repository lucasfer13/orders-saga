# Plan de implementación

Este documento es el contrato del agente `implementer`. Cada tarea del
`docs/BACKLOG.md` que necesite diseño previo entra aquí desglosada en pasos,
con su criterio de terminado y el test que lo demuestra.

**Regla de uso:** si un paso no cubre algo, el `implementer` para y pregunta.
No improvisa y no toma decisiones de diseño por su cuenta; si hace falta una
decisión nueva, vuelve al `architect` y sale un ADR antes de escribir código.

Al final de cada tarea hay una sección con las **decisiones cerradas** (con su
porqué, para no relitigarlas) y las que **siguen abiertas**. Ninguna de estas
últimas se cierra en el código: se cierran hablando y, si procede,
actualizando el ADR correspondiente.

---

# T11 — EF Core 10 + PostgreSQL por servicio, con migraciones iniciales

## Qué entra y qué no

**Entra**

- Un `DbContext` por servicio, con su base de datos independiente.
- El mapeo del agregado `Order` y de las entidades de los tres slices finos.
- La migración inicial de cada servicio y su aplicación al arrancar.
- Las cadenas de conexión en local y en `docker-compose.yml`.
- Los checks concretos que llenan los tags `ready` y `startup`, hoy vacíos.
- La sustitución de los tres stores en memoria (`StockStore`, `PaymentStore`,
  `ShipmentStore`), que desaparecen del repo.
- La adaptación de la suite de tests existente, que hoy no espera una base de
  datos.

**No entra** (y no se adelanta: cada cosa tiene su tarea)

- Mediador, behaviors y `TransactionBehavior` → **T12**. En T11 ningún handler
  de `Orders` necesita commit porque `Orders` todavía no tiene endpoints de
  escritura.
- Bus, mensajería y el check de RabbitMQ en `ready` → **T14**. El tag `ready`
  queda preparado para que T14 sólo tenga que añadir su check.
- Tabla outbox y despacho de los eventos de dominio acumulados en el agregado
  → **T15**. En T11 los eventos se ignoran en el mapeo, no se persisten.
- Tabla de mensajes procesados e idempotencia → **T23** (marcada `[Manual]`).
  Ningún paso de T11 la toca ni la anticipa.
- Concurrencia optimista en `Orders`, modelos de lectura, índices de
  rendimiento → cuando haya números que los justifiquen (**T36**), no por
  costumbre.

## Decisiones de referencia

| Decisión | Dónde |
|---|---|
| Una base por servicio en una sola instancia de PostgreSQL | [ADR-0001](adr/0001-base-de-datos-por-servicio.md) |
| Migraciones al arrancar, ligadas a `/health/startup` | [ADR-0002](adr/0002-migraciones-al-arrancar.md) |
| Mapeo del agregado `Order` sin llevar EF Core al dominio | [ADR-0003](adr/0003-mapeo-del-agregado-order.md) |
| Repositorio en `Orders`, `DbContext` directo en los slices finos | [ADR-0004](adr/0004-acceso-a-datos-asimetrico.md) |
| Reserva de stock con actualización condicional | [ADR-0005](adr/0005-concurrencia-en-la-reserva-de-stock.md) |

Los cinco siguen en estado **Propuesta**: las decisiones abiertas de T11 ya
están casi todas cerradas (ver más abajo), pero los ADRs no han sido aceptados
formalmente todavía. El `implementer` no empieza hasta que lo estén.

> Nota de numeración: `docs/BACKLOG.md` llama "ADR 1…ADR 5" a los cinco ADRs
> obligatorios de `PLAN.md` (outbox, coreografía, idempotencia, reintentos,
> arquitecturas distintas). Esos números son etiquetas, no ficheros: con los
> cinco de T11 ya escritos, el de T16 será `0006` y así sucesivamente. T38
> ("revisión final de los 5 ADRs") pasa a revisar diez.

---

## Convenciones comunes a los cuatro servicios

Se repiten en los cuatro **a propósito**: no se factorizan a un proyecto
compartido de infraestructura, porque eso volvería a acoplar los servicios
(ADR-0001).

**Ubicación y nombres**

- Carpeta `Persistence/` en la raíz del proyecto de cada servicio, no dentro
  de una carpeta de feature: el contexto lo comparten todas las features del
  servicio, y meterlo dentro de una obligaría a las demás a importar de un
  slice hermano — justo lo que T31 va a prohibir con un test.
- `<Servicio>DbContext`, `internal sealed`, coherente con el resto del
  servicio. Configuraciones en `Persistence/Configurations/`, aplicadas por
  escaneo del ensamblado. Migraciones en `Persistence/Migrations/`, la
  primera llamada `InitialCreate`.
- Nombres de tabla y columna en `snake_case`, vía `EFCore.NamingConventions`
  (`UseSnakeCaseNamingConvention()` en cada contexto), no escritos a mano
  columna a columna (**D3**).
- Las entidades de los tres slices finos sustituyen a los `record` que hoy
  viven en `Storage/`. La carpeta `Storage/` desaparece en los tres.

**Registro y configuración**

- Clave de configuración **idéntica en los cuatro servicios**:
  `ConnectionStrings:Database`. La base concreta va dentro de la propia
  cadena (`Database=orders`), de modo que el código de arranque sea el mismo
  en los cuatro y sólo cambie la configuración.
- **El valor por defecto de esa clave vive en el `appsettings.Development.json`
  de cada servicio, versionado** (**D2**). No es un secreto guardado en el
  repo: por la cadena de precedencia de configuración (`appsettings.json` →
  `appsettings.{Environment}.json` → user-secrets → variables de entorno →
  línea de comandos) es un **valor por defecto sobreescribible**, y la
  variable de entorno del compose gana siempre. Son además las mismas
  credenciales ya publicadas en `docker-compose.yml` y en la tabla del README.
  Cada `appsettings.Development.json` lleva un comentario que dice por qué ese
  valor está ahí, para que no se lea como un descuido. **No** se usan
  user-secrets y `launchSettings.json` **sigue ignorado**: no se versiona.
- `AddDbContext` con ciclo de vida *scoped*. **No** se usa
  `AddDbContextPool`: es una optimización, y en este repo las optimizaciones
  se justifican con números (T36), no por costumbre.
- **Sin `EnableRetryOnFailure`** en T11. La estrategia de ejecución con
  reintentos obliga a envolver toda transacción iniciada por el usuario, y las
  transacciones aparecen en T12 (behavior) y T15 (outbox). Activarla ahora
  significaría rehacerla entonces; se decide allí, con el código delante.
- Sin carga diferida (*lazy loading*): una consulta que dispara SQL sin verse
  en el código arruina cualquier medición de T36.
- En la cadena de conexión, un `Timeout` de conexión corto (3 s). Con el
  valor por defecto (15 s) un probe de `ready` se quedaría colgado muy por
  encima del `timeout` de su propio health check.

**Migrador y arranque** (ADR-0002)

En cada servicio, tres piezas:

1. Un tipo que publica el estado de la migración (pendiente / en curso /
   aplicada / fallida, con el mensaje de error), registrado como singleton.
2. Un `IHostedService` que, una vez el host ya está escuchando, aplica
   `Database.Migrate()` —que crea la base si no existe— y actualiza ese
   estado. Reintenta un número acotado de veces con espera entre intentos
   para absorber la ventana entre `pg_isready` y el primer `accept` real de
   PostgreSQL. Si agota los reintentos, deja el estado en "fallida" con el
   error y **no** tumba el proceso.
3. Un health check que lee ese estado.

**Qué se registra en cada tag** (esto es lo que hoy está vacío)

| Check | Tags | Qué comprueba | Timeout |
|---|---|---|---|
| `self` | `live` | Nada. Ya existe, no se toca. | 1 s |
| `postgres` | `ready` | Que la base es alcanzable, con `AddDbContextCheck` (hace `CanConnect`). | 2 s |
| `migrations` | `ready`, `startup` | El estado publicado por el migrador. | 1 s |

`AddDbContextCheck` viene de
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (**D1**),
y se eligió precisamente porque comprueba a través del mismo `DbContext` que
usa el servicio: si el check pasa, la aplicación puede trabajar de verdad, no
sólo abrir un socket contra PostgreSQL.

Tres precisiones que son parte de la decisión, no detalles:

- `migrations` va **también** en `ready` porque un servicio con la base
  alcanzable pero sin esquema no puede atender tráfico, y el `healthcheck` del
  compose apunta a `/health/ready`.
- Mientras la migración no ha terminado, el check devuelve **`Unhealthy`**, no
  `Degraded`: `Degraded` se traduce a HTTP 200 por defecto y el compose lo
  leería como "listo".
- Los checks se ejecutan en paralelo, así que `/health/ready` tarda como
  mucho lo que el más lento (2 s). El `healthcheck` del compose tiene
  `timeout: 3s`; con 2 s queda margen. Si se sube algún timeout de check, hay
  que subir también el del compose.

**Herramientas de migración**

- `Microsoft.EntityFrameworkCore.Design` como `PackageReference` con
  `PrivateAssets="all"` en los cuatro proyectos de API (la versión ya está
  fijada en `Directory.Packages.props`; no se toca).
- Manifiesto de herramientas local (`.config/dotnet-tools.json`) con `dotnet-ef`
  fijado a la misma versión de EF Core. Sin él, el comando para generar una
  migración depende de lo que cada máquina tenga instalado globalmente, que es
  exactamente el tipo de paso manual que este repo trata como bug.
- Una `IDesignTimeDbContextFactory` por servicio, para que `dotnet ef` pueda
  construir el contexto sin base de datos viva ni variables de entorno.
  Apunta a la cadena de desarrollo local.

---

## Paso 0 — Prerrequisitos (bloqueante)

1. Confirmar que los cinco ADRs pasan de *Propuesta* a *Aceptada*. Hasta que
   eso ocurra, el `implementer` no empieza.
2. Añadir a `Directory.Packages.props` las dos entradas nuevas. **No se
   modifica ninguna versión ya fijada**: sólo se añaden estas dos, y se anotan
   en el README junto al resto de comprobaciones de T03.

   | Paquete | Versión | Nota |
   |---|---|---|
   | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | **10.0.12** | Verificada en nuget.org (8 sep 2026, `net10.0`). Alineada con EF Core. |
   | `EFCore.NamingConventions` | **10.0.1** | Verificada en nuget.org (22 ene 2026, Apache-2.0). Exige `Microsoft.EntityFrameworkCore >= 10.0.1 && < 11.0.0`: encaja con el pin de 10.0.12. |

   Versiones que **ya estaban fijadas** y que esta tarea usa tal cual:
   `Microsoft.EntityFrameworkCore` **10.0.12**,
   `Microsoft.EntityFrameworkCore.Design` **10.0.12**,
   `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.3**,
   `Testcontainers.PostgreSql` **4.15.0**, `Respawn` **7.0.0**.
3. Crear el manifiesto de herramientas (`.config/dotnet-tools.json`) y fijar
   `dotnet-ef` en **10.0.12**, la misma versión que EF Core.

**Terminado cuando:** `dotnet tool restore` y `dotnet build OrdersSaga.slnx`
pasan en limpio y `dotnet ef --version` responde 10.0.12.

---

## Paso 1 — `Shipping`: el slice plantilla

Se empieza por el servicio más simple —una tabla, sin invariantes— para dejar
resuelto de una vez todo lo transversal (cadena de conexión, migrador, health
checks, fixture de Testcontainers) contra el caso con menos ruido. Los pasos 2
y 3 replican esta forma; el paso 4 es el único distinto.

- Entidad `Shipment` en `Persistence/`: `order_id` (uuid, clave primaria),
  `tracking_number` (texto, no nulo), `cancelled` (bool, no nulo). Deja de ser
  un `record` posicional inmutable: es una entidad con estado que el handler
  modifica.
- `ShippingDbContext` + configuración + migración `InitialCreate`.
- `ShipOrderHandler` y `CancelShipmentHandler` pasan a inyectar el contexto
  (ADR-0004) y a ser asíncronos. El endpoint `GetShipments` también.
- **Semántica que hay que conservar:** hoy `Ship` sobre un pedido ya expedido
  sobrescribe el envío. Con una clave primaria por pedido, un segundo `Ship`
  reventaría. En T11 se conserva el comportamiento actual (si existe, se
  actualiza) y **no** se escribe nada parecido a una comprobación de mensaje
  ya procesado: eso es T23 y está marcado como manual.
- `Storage/ShipmentStore.cs` se borra.
- Cadena de conexión: `appsettings.Development.json` versionado para ejecución
  local (**D2**) y variable de entorno `ConnectionStrings__Database` en el
  bloque `shipping` de
  `docker-compose.yml`. Las variables de entorno tienen más precedencia que
  `appsettings.Development.json`, así que en compose gana la del compose
  aunque el servicio corra como `Development`.
- Migrador + los dos health checks nuevos.

**Tests (rojo primero, commits separados)**

- Fixture de integración con Testcontainers: **un** contenedor de PostgreSQL
  para todo el ensamblado de tests, con las cuatro bases lógicas dentro
  (misma topología que el compose, ADR-0001). Una `WebApplicationFactory` por
  servicio que sobreescribe `ConnectionStrings:Database` para apuntar al
  contenedor.
- Respawn para limpiar entre tests, **ignorando la tabla de historial de
  migraciones** (si se vacía, el estado deja de coincidir con el esquema).
- Test de ida y vuelta: expedir un pedido, leerlo de vuelta en otro *scope*,
  cancelarlo, comprobar el estado.
- `HealthEndpointTests` (ya existente) deja de arrancar sin base de datos:
  pasa a usar la nueva fixture. Se **mantiene tal cual** la aserción de que
  `/health/live` sólo tiene el check `self` —es la que demuestra la regla— y
  se añaden dos: `ready` contiene `postgres` y `migrations`; `startup`
  contiene `migrations`.

**Terminado cuando:** `docker compose up -d` deja `shipping` en `healthy`
partiendo de un volumen de PostgreSQL vacío, la base `shipping` existe con su
esquema, y la suite de integración pasa.

---

## Paso 2 — `Payments`

Misma forma que el paso 1.

- Entidad `Charge`: `id` (uuid, clave primaria), `order_id` (uuid, **índice
  único** — el store actual guarda un cargo por pedido y esa propiedad se
  conserva), `amount` (`numeric(18,2)`), `currency` (`char(3)`), `refunded`
  (bool).
- La precisión del importe se declara explícitamente: un `decimal` sin
  precisión produce un `numeric` sin restricción en PostgreSQL.
- `ChargePaymentHandler` conserva intacta la regla de `DeclineAboveAmount`
  (`PaymentsOptions`): es lo que hace reproducible la demo del GIF y no tiene
  nada que ver con la persistencia. Se comprueba **antes** de escribir nada.
- `RefundPaymentHandler` y el endpoint `GetPayments`, a asíncronos.
- `Storage/PaymentStore.cs` se borra.

**Tests:** cargo y reembolso con ida y vuelta a la base; cargo por encima del
umbral rechazado **sin dejar fila** en `charges`; reembolso de un cargo ya
reembolsado devuelve falso.

---

## Paso 3 — `Inventory`

Es el único slice fino con una invariante real, y por eso va después de tener
la plantilla asentada. Sigue [ADR-0005](adr/0005-concurrencia-en-la-reserva-de-stock.md).

- Dos entidades:
  - `stock_items`: `product_id` (uuid, clave primaria), `available` (int, no
    nulo) con una restricción `CHECK (available >= 0)` como red de seguridad
    declarativa. La migración lleva un comentario explicando que es una red y
    no la comprobación principal.
  - `stock_reservations`: `order_id` + `product_id` como clave primaria,
    `quantity` (int). Hace falta fila a fila: la compensación necesita saber
    cuánto devolver por producto.
- `ReserveStockHandler`: transacción explícita; una actualización condicional
  por línea, en orden determinista por `product_id`; el número de filas
  afectadas decide; si alguna devuelve 0, `ROLLBACK` y fallo con el producto
  concreto en el mensaje (la saga propaga ese texto). Nada de leer, comprobar
  en C# y guardar.
- `ReleaseStockHandler`: en una transacción, lee las reservas del pedido,
  devuelve las cantidades, borra las filas. Si no había reservas, devuelve
  falso, como hoy.
- `Storage/StockStore.cs` se borra.
- **Datos iniciales:** sin stock no hay demo posible. Sembrador idempotente
  que corre después de las migraciones y sólo cuando una clave de
  configuración lo activa (**D4**). Apagado por defecto; encendido en
  `docker-compose.yml` y en la fixture de los tests de integración. Los datos
  viven en código, no en una migración, para que ajustar cantidades no
  obligue a generar una migración nueva. **Qué productos y qué cantidades
  sigue abierto (D4-bis)**: hasta que se decida, el sembrador se escribe con
  su lista en un solo sitio, fácil de sustituir.

**Tests**

- Los cuatro tests unitarios actuales de `ReserveStockHandler` y los de
  `ReleaseStockHandler` **se mueven a `tests/integration/`**: ya no pueden
  construir un `StockStore`, y ni los mocks de base de datos ni el proveedor
  en memoria están permitidos (`ESTANDAR-CALIDAD.md`, sección 7). Lo hace
  `test-engineer`.
- Test nuevo, que es el que justifica el ADR-0005: N tareas reservando a la
  vez el mismo producto con existencias para sólo algunas; se comprueba que
  el número de reservas concedidas es exactamente el que cabía y que
  `available` nunca queda negativo. Este test es la única prueba real de la
  invariante y no debe poder pasar con una implementación de leer-comprobar-
  guardar.
- Test de "todo o nada": un pedido de dos líneas donde la segunda no cabe no
  deja ni rastro de la primera.

---

## Paso 4 — `Orders`

El de mayor riesgo técnico. Sigue [ADR-0003](adr/0003-mapeo-del-agregado-order.md)
y [ADR-0004](adr/0004-acceso-a-datos-asimetrico.md).

**4.1 — Verificación del mapeo (primero, y se puede adelantar).** Escribe el
test de integración que guarda un `Order` completo —dos líneas, importes,
estado— y lo recupera en otro *scope* comprobando que el agregado vuelve
idéntico, incluidas las líneas y las divisas. No depende de los pasos 1 a 3,
así que si se prefiere atacar el riesgo pronto, se puede hacer antes que
nada.

**El punto exacto a verificar es `Money` dentro de `OrderLine`**: una
propiedad compleja anidada dentro de una colección propiedad. Si EF Core
10.0.12 no lo soporta, **el `implementer` para y avisa**: las dos salidas
candidatas están escritas en el ADR-0003 y ambas cambian la decisión, así que
no se eligen sobre la marcha.

**4.2 — Mapeo y migración.**

- Conversores de los IDs fuertemente tipados registrados una sola vez, en la
  configuración previa a las convenciones del contexto; columnas `uuid`;
  identificador generado nunca por la base.
- `Order.Total` como propiedad compleja, dos columnas con precisión
  declarada.
- `Lines` mapeada por el campo de respaldo `_lines`, con acceso por campo, a
  la tabla `order_lines`, con la clave shadow por defecto de EF (no una clave
  de negocio: ver ADR-0003).
- `DomainEvents` ignorado.
- `Status` como texto.
- `Orders.Domain` **no cambia**: ni una referencia, ni un atributo, ni una
  propiedad nueva. Si el mapeo pareciera exigirlo, es señal de que el mapeo
  está mal planteado, no el dominio.
- Migración `InitialCreate`.

**4.3 — Repositorio.**

- `IOrderRepository` en `Orders.Domain`, con firmas que sólo mencionan `Order`,
  `OrderId` y `CancellationToken`.
- Implementación en `Orders.Api/Persistence/`, único tipo del servicio que ve
  `OrdersDbContext`.
- **No** expone `SaveChangesAsync` ni abre transacciones: el commit es del
  `TransactionBehavior` de T12. En T11 el único que guarda explícitamente es
  el test de integración.

**4.4 — Migrador y health checks**, igual que en los otros tres.

`Orders.Api` sigue sin endpoints propios y sin documento OpenAPI: T11 no añade
ninguno.

---

## Paso 5 — Compose, README y fronteras

- `docker-compose.yml`: cuatro variables `ConnectionStrings__Database`, una por
  servicio, apuntando a `Host=postgres` y a su base. Ningún contenedor nuevo,
  ningún script de init, ninguna dependencia nueva.
- Comprobación explícita de arranque en frío: `docker compose down -v` seguido
  de `docker compose up -d` deja los cuatro servicios en `healthy` sin un solo
  paso manual. Si no, es un bug bloqueante del hito (`docs/BACKLOG.md`, notas).
- `docs-writer` actualiza el README: la tabla de probes deja de decir que
  `ready` y `startup` responden con la lista vacía, y se enlazan los cinco
  ADRs nuevos en el apartado de decisiones.
- `architecture-guard` añade las reglas que esta tarea hace verificables:
  - `Orders.Domain` no depende de EF Core (ya era cierto; ahora hay una
    referencia a EF en el repo que podría colarse, así que el test empieza a
    valer).
  - Ningún tipo de `Orders.Api` fuera de la implementación del repositorio
    depende de `OrdersDbContext`, con un nombre de test que diga por qué la
    regla está delimitada a `Orders` (ADR-0004).
  - Ningún servicio depende del `DbContext` de otro.

---

## Criterio de terminado de T11

- [ ] `docker compose down -v && docker compose up -d` deja los seis
      contenedores en `healthy`, con las cuatro bases creadas y migradas.
- [ ] Los cuatro servicios responden a los tres probes, y cada uno dice algo
      distinto: `live` sólo `self`; `ready` con `postgres` y `migrations`;
      `startup` con `migrations`.
- [ ] Las tres clases `*Store` y la carpeta `Storage/` ya no existen.
- [ ] `Orders.Domain` sigue sin ninguna `PackageReference`.
- [ ] Una migración `InitialCreate` por servicio, revisada a ojo: precisión de
      los importes, `CHECK` del stock, nombres de columna.
- [ ] Test de ida y vuelta del agregado `Order` completo.
- [ ] Test de concurrencia de la reserva de stock, que falla con una
      implementación de leer-comprobar-guardar.
- [ ] `dotnet test --solution OrdersSaga.slnx` en verde, incluidos los tests de
      arquitectura.
- [ ] El historial muestra el ciclo rojo → verde en commits separados.

---

## Decisiones cerradas

**D1 — El check de base de datos es `AddDbContextCheck`**, de
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
**10.0.12** (verificada en nuget.org, 8 sep 2026). Comprueba a través del
mismo `DbContext` que usa el servicio: si el check pasa, la aplicación puede
trabajar de verdad, no sólo abrir un socket. Se descartó
`AspNetCore.HealthChecks.NpgSql`, que verifica la conexión sin pasar por EF y
además es de terceros.

**D2 — La cadena de conexión de desarrollo va en
`appsettings.Development.json`, versionada, y no se trata como secreto.**
Por la cadena de precedencia de configuración (`appsettings.json` →
`appsettings.{Environment}.json` → user-secrets → variables de entorno →
línea de comandos) no es un secreto almacenado sino un **valor por defecto
sobreescribible**, y la variable de entorno del compose gana siempre. Son
además las mismas credenciales ya publicadas en `docker-compose.yml` y en la
tabla del README. Consecuencias registradas: **no** se usan user-secrets;
`launchSettings.json` **se queda ignorado** por `.gitignore` y no se versiona
—era el instinto inicial, pero dejaría a quien clone sin poder hacer
`dotnet run` ni F5 sin recrear a mano un fichero no documentado—; y cada
`appsettings.Development.json` lleva un comentario explicando por qué ese
valor está ahí, para que no se lea como un descuido.

**D3 — `snake_case` mediante `EFCore.NamingConventions` 10.0.1** (verificada
en nuget.org, 22 ene 2026, Apache-2.0; exige
`Microsoft.EntityFrameworkCore >= 10.0.1 && < 11.0.0`, compatible con el pin
de 10.0.12). Una línea por contexto en lugar de nombrar a mano cada tabla y
cada columna. Se descartaron las convenciones por defecto de EF
(identificadores en `PascalCase`, que en PostgreSQL obligan a comillas en
cualquier consulta manual).

**D4 — Sembrado de stock con un sembrador activado por configuración**,
apagado por defecto, encendido en `docker-compose.yml` y en la fixture de
tests. Los datos viven en código y no en una migración, para que ajustar
cantidades no obligue a generar una migración nueva ni a excluir filas de
Respawn.

## Decisiones que siguen abiertas

**D4-bis — qué productos y qué cantidades siembra.** No se cierra en T11: las
cantidades y los precios tienen que encajar con `Payments:DeclineAboveAmount`
para que la demo de T40 —un pedido que falla en el pago y dispara las
compensaciones— sea reproducible. El criterio a cumplir cuando se decida: debe
existir al menos un pedido posible que reserve stock con éxito y **después**
supere el umbral de rechazo del pago, porque ese es el único camino que
ejercita la compensación en orden inverso. Hasta entonces, el sembrador se
escribe con su lista de datos en un único sitio, fácil de sustituir sin tocar
nada más.

**D5 — Guardia de migraciones en CI.** `dotnet ef migrations
has-pending-model-changes` detecta que alguien tocó el modelo sin generar la
migración correspondiente — exactamente el tipo de desincronización que
`ESTANDAR-CALIDAD.md` (sección 9) señala como lo que más se degrada. Añadirlo
significa un job más en `ci.yml` con `dotnet tool restore` y una ejecución por
servicio. Inclinación actual: es materia de **T42** (higiene de CI) y no de
T11. Sin cerrar; si no entra ahora, que no se pierda de vista en T42.

**Riesgo técnico abierto (no es una decisión, es algo que hay que
comprobar):** el mapeo de `Money` dentro de `OrderLine` descrito en el
paso 4.1. Si EF Core 10.0.12 no soporta esa combinación, el ADR-0003 se
reabre antes de escribir la migración de `Orders`.

---
_Actualizado: 2026-09-13 · Tarea: T11 · Hito: M1_
