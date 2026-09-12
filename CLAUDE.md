# CLAUDE.md

## Qué es este proyecto

`orders-saga` demuestra consistencia en un flujo que cruza varios servicios y
puede fallar en cualquier punto: cuatro servicios (`Orders`, `Inventory`,
`Payments`, `Shipping`), una saga con compensaciones (reservar stock → cobrar
→ expedir), patrón Outbox transaccional, consumidores idempotentes y una dead
letter queue con reintentos y cuarentena. El objetivo no es entregar
features: es que las decisiones de diseño estén documentadas, defendidas y
verificadas. El lector objetivo es un tech lead evaluando mi nivel — ver
`PLAN.md` y `docs/BACKLOG.md` en el workspace para el desglose completo.

## Estructura

```
src/
  Orders.Domain/          ← dominio de Orders, cero dependencias de infraestructura
  Orders.Api/              ← orquesta la saga, referencia Orders.Domain
  Inventory.Api/            ← vertical slice fino (transaction script)
  Payments.Api/             ← vertical slice fino
  Shipping.Api/             ← vertical slice fino
tests/
  unit/                     ← dominio y behaviors, sin infraestructura
  integration/               ← Testcontainers reales, nunca mocks de infraestructura
  architecture/               ← tests de arquitectura, ver regla más abajo
docs/
  adr/                        ← 0001-titulo.md, formato Nygard
  diagrams/                   ← C4 nivel 2 en Mermaid
  media/                       ← GIFs y capturas de la demo
  BACKLOG.md                    ← desglose en tareas numeradas (T01, T02...) y milestones
README.md
docker-compose.yml
docker-compose.dcproj          ← proyecto de Visual Studio para el compose
global.json                     ← dotnet test en modo Microsoft.Testing.Platform
Directory.Packages.props
Directory.Build.props
```

**Vertical slices, no capas — con una excepción justificada.** Una feature
toca su carpeta, no cinco proyectos. `Orders` es la excepción: coordina la
saga y tiene invariantes de dominio reales (estados y transiciones válidas de
un pedido), así que sí lleva un núcleo de dominio (`Orders.Domain`) con
dependencias hacia adentro, separado de `Orders.Api`. `Inventory`, `Payments`
y `Shipping` son slices finos, casi transaction script. Esta distinción la
verifica `architecture-guard` con tests, no queda como una afirmación del
README sin más — y tiene su propio ADR (ver `docs/BACKLOG.md`, T37).

`Directory.Packages.props` con gestión centralizada de versiones: un solo
sitio donde mirar. `Directory.Build.props` con `Nullable`,
`TreatWarningsAsErrors` e `ImplicitUsings` para toda la solución.

## Decisiones ya tomadas (no las repitas ni las relitigues)

Estas ya se comprobaron contra nuget.org/docker hub y están documentadas con
su porqué en el README — si vas a añadir una dependencia relacionada, sigue
estos pines en vez de asumir la última versión:

- **MassTransit fijado en 8.5.8, no en la última.** MassTransit pasó a
  licencia comercial de pago en v9. La 8.5.8 es la última release Apache-2.0.
- **Tests en xUnit v3** (`xunit.v3`), no v2 — porque `Verify.Xunit` está
  deprecado y `Verify.XunitV3` lo exige. `dotnet test` corre en modo
  Microsoft.Testing.Platform vía `global.json`
  (`dotnet test --solution OrdersSaga.slnx`, no `dotnet test <archivo>.slnx`
  a secas).
- **Tests de arquitectura con ArchUnitNET** (`TngTech.ArchUnitNET` +
  `TngTech.ArchUnitNET.xUnitV3`), no con NetArchTest.Rules — abandonado desde
  2021.
- **Postgres arranca vacío, sin script de init.** Cada servicio crea y migra
  su propia base de datos (`orders`, `inventory`, `payments`, `shipping`) al
  arrancar vía `Database.Migrate()` de EF Core, ligado al healthcheck
  `/health/startup`. No añadas un script SQL de creación de bases de datos:
  esa responsabilidad vive en el código de migraciones de cada servicio.
- **Mediador solo en Orders, y es `Mediator` de martinothamar, no MediatR.**
  MediatR exige clave de licencia comercial desde la v13 (la última
  Apache-2.0 es la 12.5.0, de abril de 2025). `Mediator` es MIT, usa source
  generators y expone una API casi idéntica (`IRequest<T>`,
  `IPipelineBehavior<,>`), así que el pipeline de `ESTANDAR-CALIDAD.md` se
  traslada tal cual. Los handlers se registran en compilación, así que
  Scrutor solo hace falta para los validadores.
- **Inventory, Payments y Shipping no llevan mediador.** Sus endpoints y
  consumidores llaman directamente a su handler: eso es lo que significa
  "casi transaction script". El logging y las trazas los da la
  instrumentación de OpenTelemetry y el pipeline propio de MassTransit; la
  validación, la integrada de .NET 10; la idempotencia, un filtro de
  consumer. Ninguna de esas tres cosas necesita pasar por un mediador.
- **Los métodos de endpoint van `internal`, no `private`.** El generador que
  vuelca los comentarios XML al documento OpenAPI se salta los miembros
  privados: el `<summary>` se genera en el `.xml` pero nunca aparece en el
  documento. Comprobado contra `/openapi/v1.json`, no asumido.

## Cómo se levanta y se testea

```bash
docker compose up -d
dotnet build OrdersSaga.slnx
dotnet test --solution OrdersSaga.slnx
```

Si hace falta un paso manual además de estos comandos, es un bug de este
repo, no una excepción aceptable.

## Regla: ningún cambio de diseño sin su ADR

Ningún PR que cambie una decisión de diseño se mergea sin su ADR
correspondiente en `docs/adr/`, formato Nygard (contexto, decisión, estado,
consecuencias, alternativas descartadas — ver plantilla en
`_templates/docs/adr-template.md` del workspace). El `architect` los escribe
antes de que el `implementer` toque código.

## Regla: Testcontainers, nunca servicios compartidos

Los tests de integración levantan sus propias dependencias (Postgres,
RabbitMQ) vía Testcontainers. Ningún test de integración apunta al
`docker-compose.yml` de desarrollo local ni a un entorno persistente — eso es
lo que garantiza que el CI sea determinista y que un test pueda correr en
cualquier máquina sin estado previo.

## Regla: las fronteras arquitectónicas se verifican con tests

Toda afirmación de este repo sobre sus fronteras (qué no depende de qué, qué
slice no importa de otro, qué capa no toca la base de datos directamente) se
demuestra con un test en `tests/architecture/`, mantenido por el agente
`architecture-guard`. Las fronteras no se confían a la disciplina ni a la
revisión manual: si no hay un test, la afirmación no cuenta como verificada,
y no se escribe como un hecho en el README.

## Ficheros que ningún agente toca sin permiso explícito

- La lógica de compensación de la saga (rollback en orden inverso cuando
  falla un paso).
- La comprobación de idempotencia (tabla de mensajes procesados).
- El manejo de la dead letter queue (política de reintentos y cuarentena).

Si un cambio afecta a algo de esta lista, el agente para y avisa en vez de
tocarlo por su cuenta. Estas tres piezas se escriben o se revisan línea a
línea por mí.

## Atribución de commits

Nunca añadas coautoría de Claude ni referencias a la sesión en los mensajes
de commit ni en las descripciones de PR de este repo — es un portfolio
personal que evalúa un tech lead, y no debe parecer que el código lo escribió
un agente.
