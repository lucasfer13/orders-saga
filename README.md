# orders-saga

Saga distribuida con compensaciones, outbox transaccional e idempotencia sobre .NET 10, MassTransit y RabbitMQ.

**Estado:** en construcción — ver [`docs/BACKLOG.md`](docs/BACKLOG.md) para el desglose de tareas y milestones.

## Estructura

```
src/
  Orders.Domain/      ← dominio de Orders, sin dependencias de infraestructura
  Orders.Api/          ← orquesta la saga, referencia Orders.Domain
  Inventory.Api/       ← vertical slice fino
  Payments.Api/        ← vertical slice fino
  Shipping.Api/        ← vertical slice fino
tests/
  unit/                ← dominio y behaviors, sin infraestructura
  integration/          ← con Testcontainers (Postgres, RabbitMQ)
  architecture/         ← reglas de fronteras verificadas con ArchUnitNET
docs/
  adr/                  ← decisiones de diseño, formato Nygard
  diagrams/             ← C4 nivel 2
  media/                ← GIFs y capturas de la demo
```

## Cómo levantarlo

Pendiente de `docker-compose.yml` (ver `docs/BACKLOG.md`, tarea T04).

Mientras tanto, para compilar y correr los tests (proyectos de test en xUnit v3 + Microsoft.Testing.Platform, `dotnet test` va en modo MTP vía `global.json`):

```
dotnet build OrdersSaga.slnx
dotnet test --solution OrdersSaga.slnx
```

## Dependencias verificadas (T03)

Las versiones de `Directory.Packages.props` se comprobaron contra nuget.org el **2026-09-10**, no se asumieron — ver `PLAN.md`, que ya avisa de que MassTransit, Polly y los exportadores de OpenTelemetry pueden ir por detrás de un release nuevo de .NET.

**MassTransit — fijado en 8.5.8, no en la última (9.2.1).** MassTransit pasó a licencia comercial de pago a partir de la v9 (ver [massient.com/license](https://massient.com/license)); 9.2.1 es la última versión pero requiere licencia. La **8.5.8** (7 feb 2026) es la última release bajo Apache 2.0, con soporte de seguridad anunciado hasta finales de 2026. Revisar este pin antes de esa fecha — existe un fork libre de la comunidad (`OpenTransit`) si el soporte de la v8 no se renueva.

**Verify.Xunit → Verify.XunitV3.** El paquete `Verify.Xunit` está deprecado por el propio mantenedor ("legacy y ya no mantenido", sugiere `Verify.XunitV3`). Como consecuencia, los tres proyectos de test se migraron de xUnit v2 a **xUnit v3** (paquete `xunit.v3` 4.0.0) en vez de esperar a que doliera más adelante. Esto también activó el modo nuevo de `dotnet test` con Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`), que ya no admite el modo VSTest heredado en el SDK de .NET 10.

**NetArchTest.Rules → ArchUnitNET.** `NetArchTest.Rules` no tiene un release desde 2021. Se usa `TngTech.ArchUnitNET` + `TngTech.ArchUnitNET.xUnitV3` en su lugar (activamente mantenido, con paquete companion para xUnit v3), que es la alternativa que `ESTANDAR-CALIDAD.md` ya dejaba abierta.

**EF Core 10 / Npgsql / Testcontainers / OpenTelemetry:** sin sorpresas — versiones estables, sin incidencias de compatibilidad con .NET 10 conocidas a la fecha de la comprobación.
