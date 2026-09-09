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
  architecture/         ← reglas de fronteras verificadas con NetArchTest
docs/
  adr/                  ← decisiones de diseño, formato Nygard
  diagrams/             ← C4 nivel 2
  media/                ← GIFs y capturas de la demo
```

## Cómo levantarlo

Pendiente de `docker-compose.yml` (ver `docs/BACKLOG.md`, tarea T04).

Mientras tanto, para compilar y correr los tests:

```
dotnet build OrdersSaga.slnx
dotnet test OrdersSaga.slnx
```
