# 0002. Aplicar las migraciones al arrancar, desde un migrador ligado a `/health/startup`

## Estado

Aceptada

## Contexto

PostgreSQL arranca vacío a propósito (ADR-0001): la primera vez que un
servicio arranca, su base de datos no existe todavía. Alguien tiene que
crearla y aplicar el esquema, y ese "alguien" está sujeto a tres restricciones
que ya están fijadas:

1. `docker compose up -d` es el único paso admitido. Cualquier comando
   adicional para dejar el sistema usable es un bug del repo, no una nota al
   pie del README.
2. `ESTANDAR-CALIDAD.md` (sección 1) define `/health/startup` como
   "migraciones aplicadas, arranque completo", y `/health/live` como una sonda
   que **nunca** comprueba dependencias, precisamente para que el orquestador
   no reinicie en bucle un proceso sano.
3. El `healthcheck` del `docker-compose.yml` apunta a `/health/ready` con
   `start_period: 10s`, y los cuatro servicios ya esperan a
   `postgres: service_healthy`.

Hoy los tres probes existen y responden con la lista de checks vacía: `ready`
y `startup` no tienen ningún check registrado, a la espera de que haya
dependencias reales. T11 es el momento de llenarlos.

Hay además un detalle de temporización real: `pg_isready` puede responder
afirmativamente unos milisegundos antes de que el servidor acepte conexiones
de aplicación, y el arranque de los cuatro servicios es simultáneo.

## Decisión

**Cada servicio aplica sus propias migraciones al arrancar, desde un servicio
alojado (`IHostedService`) dedicado, y publica su progreso en un health check
registrado contra los tags `startup` y `ready`.**

Cuatro puntos concretos, que son la decisión:

- **El migrador corre después de que el host empiece a escuchar**, no antes de
  `app.Run()`. Así el proceso responde a `/health/live` desde el primer
  instante, incluso mientras la migración está en curso: un proceso que está
  migrando está vivo, y tratarlo de otro modo provoca exactamente el bucle de
  reinicios que la sección 1 del estándar quiere evitar.
- **La creación de la base la hace `Database.Migrate()`**, que crea la base si
  no existe y después aplica las migraciones pendientes. No hay SQL de
  creación en ningún otro sitio (ADR-0001).
- **El estado del migrador es observable.** Un singleton publica en qué punto
  está —pendiente, migrando, aplicada o fallida, con el detalle del error— y
  un health check lo lee. Ese check se registra con **ambos** tags:
  - `startup`, porque es literalmente lo que la sonda de arranque significa;
  - `ready`, porque un servicio con la base alcanzable pero sin esquema no
    puede atender tráfico, y `ready` es el probe al que apunta el compose.

  Mientras la migración no haya terminado, el check devuelve `Unhealthy`, no
  `Degraded`: `Degraded` se traduce a HTTP 200 por defecto y el compose lo
  leería como "listo".
- **Un fallo de migración no mata el proceso.** El servicio se queda
  permanentemente no-ready y el motivo se lee en el JSON de
  `/health/startup`. Un `Environment.Exit` dejaría el contenedor en un ciclo
  de reinicios que esconde la causa justo cuando más falta hace verla. El
  migrador sí reintenta un número acotado de veces con espera entre intentos,
  para absorber la ventana entre `pg_isready` y el primer `accept` real.

La conectividad con PostgreSQL se comprueba aparte, con su propio check
tagueado `ready` (ver `docs/plan.md`): son dos fallos distintos —"no llego a
la base" y "llego pero el esquema no está"— y el JSON del probe debe
distinguirlos.

## Consecuencias

**Más fácil**

- `docker compose up -d` sigue dejando el sistema utilizable sin pasos
  manuales, también en una máquina donde nunca se haya levantado antes.
- Los tests de integración obtienen el esquema por el mismo camino que
  producción: se levanta el host contra el contenedor de Testcontainers y las
  migraciones se aplican solas. No hay una segunda vía de creación de esquema
  que pueda divergir.
- Los tres probes pasan a decir algo distinto entre sí, que es la razón por la
  que son tres y no uno. `/health/startup` deja de ser decorativo.

**Más difícil / lo que se asume**

- La lógica de migración vive dentro del proceso de la aplicación. En un
  despliegue real esto se separa (un job que migra y termina, antes de lanzar
  la nueva versión), porque una aplicación en producción no debería tener
  permisos de DDL. Es deuda consciente, y va al apartado "qué haría distinto"
  del README (T41).
- **Con más de una réplica del mismo servicio, dos migradores compiten.**
  Hoy el compose levanta exactamente una réplica por servicio, así que no
  ocurre. No se implementa ningún bloqueo ahora: si en algún momento se escala
  un servicio, la solución es un *advisory lock* de PostgreSQL alrededor del
  `Migrate()`. Queda escrito aquí para que no se descubra en caliente.
- El arranque en frío es más lento que el de un proceso que no migra, y el
  `start_period` del healthcheck del compose (10 s) puede quedarse corto si el
  esquema crece. Se revisa si aparece.

**Cuándo revisar esta decisión**

Si se escala horizontalmente cualquier servicio, o si el repo pasa a tener un
pipeline de despliegue de verdad, la migración sale del proceso.

## Alternativas descartadas

- **`Database.Migrate()` síncrono justo antes de `app.Run()`** — es la forma
  más común y la más corta de escribir. Se descarta porque el proceso no
  escucha en el puerto hasta que la migración termina: durante ese tiempo
  `/health/live` no devuelve 503, devuelve *connection refused*, que para un
  orquestador es indistinguible de un proceso muerto. Justo el escenario que
  la regla de "`/live` nunca comprueba dependencias" pretende evitar, sólo que
  entrando por otra puerta.
- **`EnsureCreated()`** — crea el esquema a partir del modelo sin tabla de
  historial de migraciones. Es cómodo el primer día y bloquea el segundo: no
  hay forma de evolucionar el esquema sin borrar la base, y no existe ningún
  artefacto revisable en el PR que diga qué cambió. Además haría imposible
  T36 (medir el tiempo de recuperación tras reinicio) con datos previos.
- **Un contenedor de init por servicio que ejecute `dotnet ef database update`**
  — es lo correcto en un despliegue real y separa DDL de la aplicación. Aquí
  obliga a publicar una imagen con el SDK de .NET por servicio (frente a la
  imagen Alpine de runtime que ya usamos), multiplica el tiempo de `docker
  compose up` y añade cuatro contenedores efímeros al diagrama. El coste es
  alto y lo que demuestra no es lo que este repo quiere demostrar.
- **Migration bundles (`dotnet ef migrations bundle`)** — un ejecutable
  autocontenido con las migraciones, pensado exactamente para el caso
  anterior. Mismo problema: hace falta un paso y un artefacto más en el
  arranque local, sin ganancia en una demo que se levanta con un comando.
- **Herramienta externa de migraciones (Flyway, DbUp, scripts SQL versionados)**
  — introduce una segunda fuente de verdad del esquema junto al modelo de EF
  Core, con el riesgo permanente de que el modelo y los scripts divergan sin
  que nada lo detecte. Si algún día hiciera falta control fino del SQL, EF Core
  ya permite editar la migración generada.
- **Aplicar las migraciones desde los tests y dejar el arranque sin ellas** —
  garantiza que el camino probado no es el camino real, que es la peor
  propiedad posible para un repo cuyo argumento es que lo que afirma está
  verificado.

---
_Fecha: 2026-09-13 · Autor: Lucas · Relacionado con: T10, T11 · Ver también: ADR-0001_
