# Backlog — orders-saga

Desglose de `orders-saga` (ver `PLAN.md` y `ESTANDAR-CALIDAD.md` en la raíz del workspace) en tareas numeradas. Pensado para convertirse 1:1 en issues de GitHub cuando se cree el repo remoto — por eso cada tarea tiene un ID estable (`T01`, `T02`...) que no se reutiliza aunque una tarea se cancele.

**Orden:** por fases transversales, no por servicio. Cada hito deja algo ejecutable antes de añadir la siguiente capa de complejidad (primero hay mensajería y outbox, luego orquestación, luego compensaciones, luego idempotencia, etc.).

**Fechas límite:** son de hito, no de tarea, y son objetivos blandos pensados para un ritmo de ~8-12h/semana. Aplica la regla de corte de `PLAN.md`: si un hito se pasa un 50% del tiempo estimado, cierra lo que haya y sigue al siguiente en vez de perseguir la fecha.

`[Manual]` marca tareas que, según `AGENTES.md`, ningún agente debe tocar sin permiso explícito: lógica de compensación, comprobación de idempotencia y manejo de la DLQ. Se escriben o se revisan línea a línea.

---

## Resumen de hitos

| Hito | Objetivo | Fecha límite |
|---|---|---|
| M0 | Fundación del repo y agentes | 30 ago 2026 |
| M1 | Esqueleto de los 4 servicios + mensajería y outbox | 6 sep 2026 |
| M2 | Orquestación de la saga (happy path) + compensaciones | 13 sep 2026 |
| M3 | Idempotencia + Dead Letter Queue + admin + feature flag | 20 sep 2026 |
| M4 | Contratos (AsyncAPI/OpenAPI/Verify) + tests de arquitectura | 27 sep 2026 |
| M5 | ADRs restantes, demo visual y cierre | 4 oct 2026 |

---

## M0 — Fundación del repo y agentes (hasta 30 ago 2026)

- [ ] **T01** — Estructura inicial del repo: `src/`, `tests/{unit,integration,architecture}`, `docs/{adr,diagrams,media}`, `README.md` mínimo.
- [ ] **T02** — Copiar y adaptar `Directory.Packages.props`, `Directory.Build.props` (`Nullable`, `TreatWarningsAsErrors`, `ImplicitUsings`), `.editorconfig` y `.gitignore` desde `_templates/dotnet`.
- [ ] **T03** — Comprobar versiones de MassTransit, EF Core, Testcontainers y exportadores de OpenTelemetry compatibles con .NET 10 antes de fijarlas en `Directory.Packages.props`. Anotarlo en el README.
- [ ] **T04** — `docker-compose.yml` base con Postgres y RabbitMQ (con management UI). `docker compose up` debe dejarlo todo arriba sin pasos manuales.
- [ ] **T05** — Ejecutar el Prompt 1 de `AGENTES.md`: `CLAUDE.md` raíz + los 6 subagentes (`architect`, `implementer`, `test-engineer`, `architecture-guard`, `adversarial-reviewer`, `docs-writer`) en `.claude/agents/`. Revisar lo generado antes de commitear.
- [ ] **T06** — Hook de pre-commit: `dotnet format --verify-no-changes`, `dotnet test`, `dotnet list package --vulnerable --include-transitive`.
- [ ] **T07** — `.github/workflows/ci.yml` (build, tests, análisis estático, escaneo de vulnerabilidades) + Renovate, desde `_templates/ci`.

---

## M1 — Esqueleto de los 4 servicios + mensajería y outbox (hasta 6 sep 2026)

- [ ] **T08** — Proyecto `Orders`: modelo de dominio con invariantes reales (agregado `Order`, estados, transiciones válidas). Arquitectura con dependencias hacia adentro.
- [ ] **T09** — Proyectos `Inventory`, `Payments`, `Shipping` como vertical slices finos (transaction script), con un slice mínimo cada uno.
- [ ] **T10** — Health checks `/health/live`, `/health/ready`, `/health/startup` en los 4 servicios, con tags y timeout. `/live` nunca comprueba dependencias.
- [ ] **T11** — EF Core 10 + PostgreSQL por servicio (base de datos independiente cada uno) con migraciones iniciales.
- [ ] **T12** — Pipeline de MediatR (`Logging → Validation → Idempotency → Transaction → Performance → Handler`), registro de handlers y validadores por escaneo con Scrutor.
- [ ] **T13** — FluentValidation en los comandos de `Orders` con reglas condicionales/entre campos, como piezas testeables del slice.
- [ ] **T14** — MassTransit + RabbitMQ configurado en los 4 servicios (bus operativo, todavía sin publicar eventos de la saga).
- [ ] **T15** — Patrón Outbox transaccional en cada productor (tabla outbox + publicador en background).
- [ ] **T16** — ADR 1: Outbox transaccional frente a 2PC.

---

## M2 — Orquestación de la saga + compensaciones (hasta 13 sep 2026)

- [ ] **T17** — Contratos de mensajes de la saga: `OrderCreated`, `StockReserved`, `PaymentCharged`, `OrderShipped` y sus variantes de fallo.
- [ ] **T18** — Orquestador en `Orders`: reservar stock → cobrar → expedir (happy path, sin fallos todavía).
- [ ] **T19** — ADR 2: Coreografía frente a orquestación.
- [ ] **T20** — `[Manual]` Lógica de compensación: deshacer en orden inverso cuando falla un paso (liberar stock, reembolsar pago).
- [ ] **T21** — Consumidores en `Inventory`/`Payments`/`Shipping` que reaccionan a los comandos de la saga y publican éxito o fallo.
- [ ] **T22** — Criterio de terminado: test que mata el proceso a mitad de saga y verifica que se reanuda o compensa correctamente.

---

## M3 — Idempotencia + DLQ + admin + feature flag (hasta 20 sep 2026)

- [ ] **T23** — `[Manual]` Tabla de mensajes procesados y comprobación de idempotencia en cada consumidor.
- [ ] **T24** — ADR 3: estrategia de idempotencia (clave y ventana de retención).
- [ ] **T25** — Criterio de terminado: test que reinyecta 10.000 mensajes duplicados y verifica cero efectos duplicados.
- [ ] **T26** — `[Manual]` Política de reintentos y cuarentena de mensajes envenenados hacia la Dead Letter Queue.
- [ ] **T27** — ADR 4: política de reintentos y criterio de cuarentena.
- [ ] **T28** — Criterio de terminado: test que verifica que un mensaje envenenado va a DLQ sin bloquear la cola.
- [ ] **T29** — Endpoint de administración para reinyectar mensajes desde la DLQ, protegido con JWT y policy (nunca `[Authorize(Roles=...)]` suelto).
- [ ] **T30** — Feature flag (`Microsoft.FeatureManagement`) para la ruta nueva de la saga junto a la antigua, con fecha de caducidad en comentario.

---

## M4 — Contratos, tests de arquitectura y snapshots (hasta 27 sep 2026)

- [ ] **T31** — Tests de arquitectura (NetArchTest/ArchUnitNET): dominio de `Orders` no referencia infraestructura/EF/MassTransit; un slice no importa de otro; los handlers no usan `DbContext` directamente; todo comando tiene handler y validador.
- [ ] **T32** — Criterio de terminado: snapshots (Verify) de los payloads de todos los eventos publicados.
- [ ] **T33** — Criterio de terminado: especificación AsyncAPI de canales y payloads de mensajería.
- [ ] **T34** — OpenAPI 3.1 nativo + Scalar para los endpoints REST expuestos (consulta de pedido, admin DLQ), servido también en YAML.
- [ ] **T35** — `IExceptionHandler` centralizado + ProblemDetails (RFC 9457) con `traceId`; `TypedResults` en todos los endpoints.
- [ ] **T36** — Medición: mensajes procesados por segundo, latencia extremo a extremo de la saga, tiempo de recuperación tras reinicio. Números documentados, nunca inventados.

---

## M5 — ADRs restantes, demo visual y cierre (hasta 4 oct 2026)

- [ ] **T37** — ADR 5: por qué arquitecturas distintas en el mismo repo (`Orders` con dominio vs. slices finos en el resto).
- [ ] **T38** — Revisión final de los 5 ADRs: formato Nygard, coherentes con lo implementado.
- [ ] **T39** — Pase del `adversarial-reviewer` en contexto limpio: condiciones de carrera, fallos parciales no cubiertos, supuestos de orden de mensajes.
- [ ] **T40** — Script de `vhs` reproducible + grabación del GIF: un pedido fallando en el pago y las compensaciones ejecutándose en orden inverso.
- [ ] **T41** — README final con el orden fijo de `PLAN.md`: demo GIF, problema en tres frases, cómo levantarlo (dos comandos), diagrama C4 nivel 2, decisiones con enlace a ADRs, alternativas descartadas, resultados medidos, qué haría distinto.
- [ ] **T42** — Higiene de CI visible: badges reales (build/cobertura), `TreatWarningsAsErrors` activo, `dotnet list package --vulnerable` en CI, Trivy o `docker scout` sobre las imágenes.

---

## Notas

- Las tareas `[Manual]` (T20, T23, T26) las escribes o revisas tú línea a línea, según `AGENTES.md`. Un agente puede proponer un borrador, pero no se commitea sin tu revisión.
- El ciclo TDD (rojo → verde → refactor) debe verse en el historial de commits: commit del test en rojo separado del de la implementación. No es una tarea aparte, es una regla de cómo se resuelve cada tarea de código.
- Si `docker compose up` deja de funcionar sin pasos manuales en algún punto, es un bug, no una nota a pie de página — trátalo como bloqueante del hito en curso.
