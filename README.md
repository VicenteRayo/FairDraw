# FairDraw

Sorteos aleatorios, verificables y resistentes a manipulaciones, para grupos de amigos (por ejemplo, elegir el destino de un viaje). El protocolo se basa en **Commit-Reveal**: nadie —ni el creador, ni el último en participar, ni el propio servidor— puede sesgar el resultado, y cualquiera puede comprobar de forma independiente que el resultado publicado es el correcto.

---

## En pocas palabras

### Qué hace

FairDraw es una web para hacer **sorteos entre amigos** en los que nadie puede hacer trampas. Por ejemplo, para decidir a qué ciudad vais de viaje o quién paga la cena.

Lo importante es que no hace falta fiarse de nadie: ni del que organiza el sorteo, ni del último en participar, ni siquiera de la propia aplicación. Además, cualquiera puede comprobar después que el resultado es el que tenía que salir.

### Cómo funciona

**1. Alguien crea el sorteo.**
Escribe las opciones (por ejemplo, "Pizza" y "Sushi") y los nombres de los participantes (Ana, Bruno, Carla). Después cierra la lista para que nadie pueda añadir ni quitar nada, y comparte un enlace con el grupo.

**2. Cada uno entra y elige un número del 1 al 10 en secreto.**
Es como meter tu número en un **sobre cerrado**. Todos ven que has entregado el sobre, pero nadie puede ver qué número hay dentro. La app te asigna además una especie de "huella" del sobre. Si alguien intentara cambiar el número más tarde, la huella ya no coincidiría y se notaría enseguida.

**3. Cuando el último entrega su sobre, se abren todos a la vez.**
La app junta todos los números y los mezcla con una receta fija y conocida. Esa receta siempre da el mismo resultado con los mismos números, y de ahí sale la opción ganadora. Nadie tiene que volver a entrar: al abrir el enlace ya se ve quién ha ganado.

### Por qué nadie puede hacer trampas

- **El último en elegir no tiene ventaja.** Cuando elige, todavía no puede ver los números de los demás, porque siguen en sobres cerrados.
- **Nadie puede cambiar su número después.** Su sobre ya tiene una huella que lo delataría.
- **El organizador tampoco puede manipularlo.** El resultado depende de los números de todos, no de él.
- **No vale "volver a tirar".** Con los mismos números siempre sale el mismo ganador, así que no se puede repetir el sorteo hasta que salga lo que uno quiere.

### Cómo se comprueba

Hay una página de **verificación**. Cualquiera, incluso alguien de fuera del grupo, puede repetir allí la receta con los datos públicos del sorteo. Así comprueba por su cuenta que:

- cada sobre contenía lo que se dijo,
- se abrieron todos,
- el ganador anunciado es el que tenía que salir.

**En resumen:** es como un sorteo con sobres cerrados que se abren todos a la vez, y en el que después cualquiera puede repetir las cuentas para ver que nadie ha hecho trampa.

Hay un matiz: el servidor de la aplicación sí ve los números antes de que se abran. Eso no le permite cambiar el resultado sin que se note, pero sí sabe antes que nadie quién va a ganar (ver [§13](#13-amenazas-y-mitigaciones)).

---

## Índice

1. [Quickstart con Docker](#1-quickstart-con-docker)
2. [Alternativa nativa (sin Docker)](#2-alternativa-nativa-sin-docker)
3. [Qué problema resuelve FairDraw](#3-qué-problema-resuelve-fairdraw)
4. [Qué es Commit-Reveal, y por qué nadie puede hacer trampas](#4-qué-es-commit-reveal-y-por-qué-nadie-puede-hacer-trampas)
5. [Arquitectura](#5-arquitectura)
6. [Modelo de datos](#6-modelo-de-datos)
7. [Formatos canónicos exactos (referencia del protocolo)](#7-formatos-canónicos-exactos-referencia-del-protocolo)
8. [Ejemplo end-to-end completo (valores reales)](#8-ejemplo-end-to-end-completo-valores-reales)
9. [Contrato de API](#9-contrato-de-api)
10. [Identidad de los participantes ("reclamar nombre")](#10-identidad-de-los-participantes-reclamar-nombre)
11. [Máquina de estados](#11-máquina-de-estados)
12. [Cómo verificar un sorteo de forma independiente](#12-cómo-verificar-un-sorteo-de-forma-independiente)
13. [Amenazas y mitigaciones](#13-amenazas-y-mitigaciones)
14. [Retención y borrado de datos](#14-retención-y-borrado-de-datos)
15. [Testing](#15-testing)
16. [Estructura del repositorio](#16-estructura-del-repositorio)
17. [Roadmap / fuera de alcance](#17-roadmap--fuera-de-alcance)
18. [Licencia](#18-licencia)

---

## 1. Quickstart con Docker

Requisitos: Docker y Docker Compose. No hace falta tener .NET ni Node instalados.

```bash
git clone <este-repositorio>
cd FairDraw
docker compose up --build
```

Cuando los tres servicios estén arriba:

- Frontend: http://localhost:5173
- API: http://localhost:5080/api/v1
- Healthcheck: http://localhost:5080/health

Las migraciones de base de datos se aplican automáticamente al arrancar el backend (con reintentos de ~5×3s por si Postgres tarda en aceptar conexiones).

Para pararlo: `docker compose down` (añade `-v` si además quieres borrar los datos de Postgres).

**Importante:** `VITE_API_BASE_URL` (variable de entorno del contenedor `frontend`) debe apuntar a una URL que el **navegador** pueda resolver (`http://localhost:5080/api/v1`), nunca al nombre interno de servicio de Docker Compose (`http://backend:8080`), que solo existe dentro de la red interna de los contenedores.

---

## 2. Alternativa nativa (sin Docker)

Backend (.NET 10 + PostgreSQL):

```bash
sudo apt install dotnet-sdk-10.0
# Necesitas un Postgres accesible en localhost:5432 (o ajusta ConnectionStrings:FairDraw
# en backend/src/FairDraw.Api/appsettings.Development.json)
cd backend
dotnet run --project src/FairDraw.Api
```

Frontend (Node 22+):

```bash
sudo apt install nodejs npm
cd frontend
npm install
VITE_API_BASE_URL=http://localhost:5080/api/v1 npm run dev
```

---

## 3. Qué problema resuelve FairDraw

Un grupo de amigos quiere sortear algo (un destino de viaje, quién paga, etc.) y quiere estar seguro de que:

- nadie ha podido preparar el resultado de antemano,
- nadie ha podido cambiar su elección después de verla en manos de los demás,
- el organizador del sorteo no puede manipular el resultado aunque quisiera,
- cualquiera —incluso alguien externo al grupo— puede comprobar, con los datos públicos, que el resultado anunciado es exactamente el que el protocolo debía producir.

Una app "bonita" que simplemente calcula `Math.random()` en el servidor no ofrece ninguna de estas garantías: hay que confiar ciegamente en que el servidor no ha hecho trampas. FairDraw sustituye esa confianza por matemáticas verificables.

---

## 4. Qué es Commit-Reveal, y por qué nadie puede hacer trampas

**Commit-Reveal** es un protocolo en dos fases:

1. **Commit:** cada participante elige en secreto su número (1-10) y el navegador genera un secreto aleatorio de 256 bits. Se publica el **hash** de ambos (`SHA256(drawId, participantId, number, secret)`); nadie más que el servidor ve el número ni el secreto mientras dure esta fase.
2. **Reveal:** el número y el secreto viajan al servidor **en la misma petición que el commitment**. El servidor comprueba que coinciden con el hash (si no, `422` y no se guarda nada) y los guarda ocultos: no aparecen en ningún endpoint mientras falte alguien por elegir. Cuando elige el último participante, el servidor calcula el resultado en esa misma transacción y el sorteo pasa directamente a `Completed`: **nadie tiene que volver a entrar** para revelar; cualquiera que abra el enlace ve el resultado.

> Antes, el reveal era una segunda ronda: cada participante tenía que volver a abrir el enlace, en el mismo navegador, para que su elección se revelase. Si alguien no volvía (o había perdido su secreto), el sorteo se quedaba atascado. Esa ronda sigue existiendo solo como respaldo: si algún commitment llega sin su reveal (un cliente antiguo), el sorteo pasa a `Revealing` y espera a los reveals que falten.

### Por qué el último participante no puede elegir el resultado después de conocer las demás elecciones

Este es el ataque clásico que Commit-Reveal está diseñado para evitar. En FairDraw:

- Durante la fase de commit, el servidor **nunca** expone números ni secretos — solo hashes, que no revelan nada sobre el número elegido. El último participante en comprometer no sabe absolutamente nada sobre lo que eligieron los demás.
- Los números solo se hacen públicos cuando el **100%** de los participantes ha comprometido. Por tanto, para cuando alguien puede "ver" elecciones ajenas, su propia elección ya está criptográficamente sellada por su commitment — no puede cambiarla sin que el hash deje de coincidir.
- Como el reveal va en la misma petición que el commitment, nadie puede, una vez vistos los demás números, decidir no revelar el suyo para bloquear el sorteo. La contrapartida es que el servidor sí conoce los números antes de tiempo (ver §13).

---

## 5. Arquitectura

```
FairDraw/
├── backend/    ASP.NET Core Web API (.NET 10) + PostgreSQL
└── frontend/   React + TypeScript + Vite
```

**Backend** (Clean-ish layering, Minimal API):

- `FairDraw.Domain` — entidades del dominio (POCOs), sin dependencias de EF ni ASP.NET.
- `FairDraw.Crypto` — canonicalización, SHA-256 y rejection sampling. Sin estado, sin IO. Es el módulo más importante del repositorio: aquí vive la lógica que decide el resultado del sorteo, y su contraparte exacta en TypeScript (`frontend/src/crypto/`) debe producir siempre los mismos hashes.
- `FairDraw.Infrastructure` — `DbContext`, configuración de EF Core, migraciones.
- `FairDraw.Api` — endpoints Minimal API, servicios de aplicación (`DrawService`, `IdentityService`, `CommitmentService`, `RevealService`, `VerificationService`), autenticación por token portador.

**Frontend:**

- `src/crypto/` — la misma lógica criptográfica que el backend (canonicalización, SHA-256 vía `window.crypto.subtle`, generación de secretos vía `crypto.getRandomValues`, rejection sampling), usada tanto en el flujo normal como en `/draw/:code/verify`.
- `src/api/` — cliente HTTP tipado.
- `src/pages/` — Home, crear sorteo, el "hub" del sorteo (reclamar nombre, comprometer, revelar, resultado), y la página de verificación.

Ninguna primitiva criptográfica está implementada a mano: todo se apoya en `System.Security.Cryptography` (backend) y `window.crypto` (frontend).

---

## 6. Modelo de datos

```
Draw          — Id (interno), PublicCode (público, usado en el protocolo), Name, State,
                ConfigurationHash, CreatorTokenHash, timestamps.
Option        — Id, DrawId, Text, SortOrder.        unique(DrawId, SortOrder), unique(DrawId, Text)
Participant   — Id, DrawId, Name, SortOrder,
                ClaimSecretHash, ClaimedAt.          unique(DrawId, SortOrder), unique(DrawId, Name)
Commitment    — Id, ParticipantId (unique), DrawId, CommitmentHash, CommittedAt.
Reveal        — Id, ParticipantId (unique), DrawId, Number?, SecretHex?, IsValid, Attempts.
Result        — Id, DrawId (unique), ResultHash, AttemptsUsed, WinnerIndex, WinnerOptionId.
```

Decisiones clave:

- `Draw.Id` (Guid) es la clave interna de base de datos y **nunca** aparece en el protocolo criptográfico ni en URLs. `Draw.PublicCode` (p. ej. `8F4K92LM`) es el `drawId` usado literalmente en **todas** las fórmulas canónicas — así la página de verificación nunca necesita conocer detalles del esquema de base de datos.
- `Commitment` y `Reveal` tienen una clave foránea **única** sobre `ParticipantId`: un participante solo puede tener un commitment y (como fila) un reveal — aunque un reveal inválido puede reintentarse, porque el commitment ya es inmutable y un reintento con otros datos simplemente seguirá sin coincidir.

---

## 7. Formatos canónicos exactos (referencia del protocolo)

Esta sección es la fuente de verdad del protocolo. El backend (`FairDraw.Crypto`) y el frontend (`src/crypto/`) implementan exactamente esto, y están probados contra los mismos [vectores dorados](test-vectors/canonical-vectors.json) para garantizar que produzcan bit a bit los mismos resultados.

Reglas generales: toda cadena canónica se codifica en **UTF-8** y se hashea con **SHA-256**; el hash se representa siempre en **hex minúsculas**, sin prefijo `0x`. `drawId` es siempre el código público (nunca un identificador interno). Los nombres de opciones y participantes no pueden contener `|`.

### 7.1 `configurationHash` (se calcula una vez, al bloquear el sorteo)

```
canonicalData = "{drawId}|{OPTION_1}|...|{OPTION_M}|{PARTICIPANT_1}|...|{PARTICIPANT_N}"
configurationHash = SHA256(canonicalData)
```

Opciones y participantes en el orden (`SortOrder`) fijado en el momento del bloqueo.

### 7.2 `commitment` (uno por participante, fase Commit)

```
version=1|drawId={drawId}|participantId={participantId}|number={number}|secret={secretHex}
commitment = SHA256(esa cadena)
```

- `participantId`: el GUID del participante tal cual lo emite la API (36 caracteres, minúsculas, con guiones).
- `number`: entero decimal 1–10, sin ceros a la izquierda.
- `secretHex`: 256 bits = 64 caracteres hex en minúsculas.

### 7.3 `resultHash` (una vez por sorteo, cuando todos han revelado)

```
reveals ordenados por participantId (comparación ordinal de strings)
canonicalRevealedData = "version=1|drawId={drawId}|reveals={pid1}:{num1},{pid2}:{num2},...,{pidN}:{numN}"
resultHash = SHA256(canonicalRevealedData)
```

### 7.4 Selección del ganador sin sesgo de módulo (rejection sampling)

Un `resultHash % N` directo introduce sesgo cuando `N` no divide exactamente `2^64`. Como aquí solo disponemos de **un** hash fijo (no de un flujo de aleatoriedad resampleable — volver a intentarlo sería en sí mismo una forma de manipular el resultado), usamos un bucle de rechazo determinista basado en un contador:

```
attempt = 0
mientras verdadero:
    seed = "{resultHashHex}|{attempt}"
    candidateHash = SHA256(seed)
    value = uint64 big-endian de los primeros 8 bytes de candidateHash
    limit = floor(2^64 / N) * N
    si value < limit:
        winnerIndex = value % N
        romper
    attempt += 1
```

Esto es determinista y reproducible por cualquiera: mismos `resultHash`/`N` ⇒ mismo `winnerIndex`. Con `N` potencia de dos, `limit == 2^64` exactamente, por lo que **nunca** se rechaza ningún valor. `attemptsUsed` (casi siempre `0`) se publica junto al resultado precisamente para que la verificación pueda reproducir el mismo número de intentos.

**Nota de implementación:** `value` puede superar `Number.MAX_SAFE_INTEGER`; el frontend usa `BigInt` (nunca `Number`) para esta cuenta, y el backend usa `ulong`/`UInt128`.

---

## 8. Ejemplo end-to-end completo (valores reales)

Estos valores están calculados de verdad (no inventados) y son exactamente los que usan los tests automáticos de ambos lados (`test-vectors/canonical-vectors.json`).

```
drawId = 8F4K92LM
opciones = [Pizza, Sushi]
participantes = [Ana, Bruno, Carla]

configurationHash:
  canonical = "8F4K92LM|Pizza|Sushi|Ana|Bruno|Carla"
  hash      = 3bb94afaed4434e98d57a6a7ba704ac6aac6158fa49eeb8da2a15663e69c43d0

Commitments:
  Ana   (número 7, participantId 3fa85f64-5717-4562-b3fc-2c963f66afa6)
        → commitment = b1ec228e672925583ee13618ff7295f6c9187d1f26196dfb0c85cc9ac273b337
  Bruno (número 3, participantId 5b6c7d8e-1234-4562-b3fc-2c963f66afa7)
        → commitment = fdb2b7d8533add8fed36c43c48f21d85768fade0db763c2f193ec652d4a189a4
  Carla (número 9, participantId 9c8d7e6f-4321-4562-b3fc-2c963f66afa8)
        → commitment = 72241f235462812105427791eda810bb64be0b92c3c56e93fc70931188103a2d

Reveal (canonicalRevealedData, ya ordenado por participantId):
  "version=1|drawId=8F4K92LM|reveals=3fa85f64-...:7,5b6c7d8e-...:3,9c8d7e6f-...:9"
  resultHash = 45cc6d37e1faf733446b44691d15ff58b6c060b6eae0d6e667ddaefbc0064f68

Selección del ganador (N=2):
  attempt=0 → candidateHash = 338387df41adee40...
  primeros 8 bytes → value = 3711959910832795200
  limit (N=2) = 2^64 (2 divide 2^64 exactamente) → value < limit siempre
  winnerIndex = value % 2 = 0
  attemptsUsed = 0
  → options[0] = "Pizza"

VERIFICACIÓN:
  Commitments válidos: ✅
  Todos los participantes revelaron: ✅
  Hash del resultado correcto: ✅
  Índice correcto: ✅
  Resultado correcto: ✅ (Pizza)
```

Este mismo flujo, pero contra la API real (con GUIDs generados por el servidor), se ejecuta automáticamente en `EndToEndGoldenVectorTests` (backend) y puede reproducirse en cualquier despliegue: crea un sorteo con esas mismas opciones/participantes, usa los mismos números/secretos, y compara.

---

## 9. Contrato de API

Base: `/api/v1`. Autenticación: `Authorization: Bearer <token>` (mismo mecanismo para el token del creador y el secreto de cada participante).

| Método y ruta | Auth | Descripción |
|---|---|---|
| `POST /draws` | — | Crea el sorteo en `Created`. Devuelve `creatorToken` **una sola vez**. |
| `GET /draws/{code}` | — | Vista pública, filtrada según el estado (ver §11). |
| `PATCH /draws/{code}` | Bearer creatorToken | Reemplaza nombre/opciones/participantes. Solo en `Created`. |
| `POST /draws/{code}/lock` | Bearer creatorToken | Calcula `configurationHash` y congela la configuración. |
| `POST /draws/{code}/cancel` | Bearer creatorToken | Cancela el sorteo (si no está `Completed`). |
| `POST /draws/{code}/participants/{id}/claim` | — | Reclama un nombre libre; devuelve el secreto **una sola vez**. |
| `POST /draws/{code}/participants/{id}/commitments` | Bearer participantSecret | Body `{ commitment, number, secret }`. Publica el commitment y guarda (oculto) el reveal tras validarlo; si es el último, resuelve el sorteo. `number`/`secret` son opcionales solo por compatibilidad. |
| `POST /draws/{code}/participants/{id}/reveals` | Bearer participantSecret | Solo en `Revealing` (respaldo): revela número + secreto; el servidor valida contra el commitment. |
| `GET /draws/{code}/verification` | — | Payload plano y estable para recomputar todo en el navegador. |
| `GET /health` | — | Healthcheck (backend + Postgres). |

Códigos de estado: `400` datos inválidos, `401` credencial ausente/incorrecta, `403` no autorizado (p. ej. nombre ya reclamado), `404` no existe, `409` conflicto de estado/inmutabilidad, `422` el reveal no coincide con el commitment.

**Regla dura, probada explícitamente en los tests:** ningún `number`/`secret` se expone en ningún endpoint mientras el sorteo no esté en `Revealing`/`Completed`, y solo para reveals válidos.

---

## 10. Identidad de los participantes ("reclamar nombre")

El creador comparte **una única URL pública** (`/draw/{code}`) por WhatsApp, tal y como pide el caso de uso. Como todos abren el mismo enlace, hace falta un mecanismo para que cada persona solo pueda actuar en nombre de sí misma:

1. El creador crea el sorteo con una lista de nombres (sin cuentas de usuario).
2. Al bloquear el sorteo, cada `Participant` queda con un "asiento" libre.
3. Cualquiera que abra la URL puede pulsar un nombre **todavía no reclamado** ("Soy yo"). El servidor genera entonces un secreto de 256 bits, guarda solo su hash, y devuelve el secreto en claro **una única vez** — como al fijar una contraseña.
4. Ese secreto se guarda en el `localStorage` del navegador y debe presentarse (como `Bearer`) en cada commit/reveal posterior para esa identidad.
5. Un nombre ya reclamado no puede volver a reclamarse (409/403).

Es una identidad ligera, pensada para el modelo de confianza de "un grupo de amigos" — no un sistema de identidad formal (ver §13, "Sybil").

---

## 11. Máquina de estados

```
CREATED → OPEN → COMMITTING → REVEALING → COMPLETED
   ↓        ↓         ↓            ↓
   └────────┴─────────┴──── CANCELLED
```

| Transición | Disparador |
|---|---|
| `Created → Open` | Explícito: el creador bloquea el sorteo (`/lock`). |
| `Open → Committing` | Automático, al llegar el primer commitment. |
| `Committing → Completed` | Automático, cuando el 100% de los participantes ha comprometido y todos enviaron su reveal con el commitment (flujo normal). Aquí se calcula el `Result`. |
| `Committing → Revealing` | Automático, cuando el 100% ha comprometido pero a alguno le falta el reveal (respaldo). |
| `Revealing → Completed` | Automático, cuando el 100% de los reveals son válidos (aquí se calcula el `Result`). |
| `* → Cancelled` | Explícito: el creador cancela (siempre que no esté `Completed`). |

Y la visibilidad de datos por estado (regla dura, no negociable):

| Estado | Opciones/participantes | Commitments | Reveals (número+secreto) | Resultado |
|---|---|---|---|---|
| Created | editables | — | — | — |
| Open/Committing | inmutables + hash | hashes de quien ya comprometió | — | — |
| Revealing | inmutables | todos | solo los **válidos**, a medida que llegan | — |
| Completed | inmutables | todos | todos (todos válidos) | completo |

---

## 12. Cómo verificar un sorteo de forma independiente

La página `/draw/{code}/verify` no confía en ningún cálculo del servidor: descarga el payload público (`GET /draws/{code}/verification`) y, con el mismo módulo `src/crypto/` usado durante el sorteo, recalcula en el propio navegador:

1. el `configurationHash` a partir de las opciones/participantes publicados,
2. cada `commitment` a partir de los números/secretos revelados,
3. el `resultHash` a partir de todos los reveals,
4. el `winnerIndex` (con el mismo rejection sampling) y la opción ganadora correspondiente,

mostrando un checklist ✅/❌ por cada comprobación y un veredicto final: **✅ SORTEO VERIFICADO** o **❌ DATOS NO VÁLIDOS**. La misma función también puede ejecutarse manualmente contra cualquier despliegue de FairDraw, incluso uno que ya no exista, siempre que se conserven los datos públicos.

---

## 13. Amenazas y mitigaciones

Análisis realizado *antes* de implementar (como pide el enunciado), y que determinó decisiones de diseño concretas, no solo texto:

| Amenaza | Estado | Mitigación |
|---|---|---|
| El creador altera opciones/participantes tras publicar el sorteo | **Resuelto** | `Lock` congela la configuración y fija `configurationHash`; la API rechaza cualquier cambio posterior (409). |
| El último en comprometer conoce las elecciones ajenas | **Resuelto** | Durante `Committing` solo se exponen hashes; la transición a `Revealing` solo ocurre al 100% de commits, así que nadie ve un número ajeno antes de que su propio commitment ya esté sellado. |
| Un participante cambia su número al revelar | **Resuelto** | El servidor recalcula el hash y lo compara byte a byte; si no coincide, `422` y el reveal no se acepta como válido. |
| El servidor manipula el resultado | **Mitigado** | Todo es recomputable públicamente (`/verification` + `/verify`). Limitación residual: el servidor podría, en teoría, mostrar datos distintos a observadores distintos (equivocación) — fuera de alcance de esta versión; mitigación social (guardar/compartir capturas de pantalla o el JSON de verificación). |
| Un participante se niega a revelar tras comprometer | **Resuelto** | El reveal se envía con el commitment, así que no existe una segunda ronda en la que alguien pueda no aparecer. Solo en el flujo de respaldo (`Revealing`) puede quedarse atascado; el creador puede cancelarlo. |
| El servidor (o quien opere la base de datos) conoce los números antes de que elija el último | **Aceptado, documentado** | Es el coste de no exigir una segunda ronda: el servidor guarda número y secreto desde el commit. Nunca los expone por la API antes de tiempo (probado en los tests), pero un operador malicioso podría filtrárselos al último participante, que entonces podría elegir su número para forzar un resultado. Aceptable en el modelo de confianza de "grupo de amigos con servidor propio"; si no lo es, hay que volver al reveal en dos rondas. |
| Un participante crea varias identidades (Sybil) | **No resuelto** | Fuera del límite criptográfico del sistema. Mitigación puramente social: el creador conoce a sus amigos. |
| Predecir el resultado antes del reveal | **Resuelto** | Imposible mientras se cumpla que ningún dato se revela antes de `Revealing`, y esa transición exige el 100% de los commits. |
| Sesgo de módulo al elegir la opción ganadora | **Resuelto** | Rejection sampling determinista (ver §7.4) en vez de `% N` directo. |

---

## 14. Retención y borrado de datos

Cada sorteo **solo está disponible durante 30 días desde su creación** (`Draw.RetentionDays`), independientemente de si llegó a completarse, se canceló, o simplemente se quedó abierto sin que nadie lo ejecutara. Pasado ese plazo:

* Cualquier petición sobre ese sorteo (`GET /draws/{code}`, `/verification`, `claim`, `commit`, `reveal`, `lock`, `cancel`...) responde `404 draw_not_found`, exactamente igual que si el código no hubiera existido nunca. Esta comprobación se hace en memoria justo tras cargar el sorteo (nunca como filtro SQL — ver nota de implementación más abajo), así que el límite de los 30 días es exacto, no aproximado.
* Por debajo, un proceso en segundo plano (`DrawExpirationHostedService`) revisa cada hora todos los sorteos y **borra físicamente** los que superan los 30 días —incluyendo opciones, participantes, commitments, reveals y resultado, en cascada— para no acumular indefinidamente nombres, números revelados y secretos de nadie.

`DrawDto` y `VerificationResponse` incluyen un campo `expiresAt` con la fecha exacta de caducidad, que la interfaz muestra en la propia página del sorteo y en la de verificación.

**Nota de implementación:** la comprobación de expiración se hace comparando fechas en memoria (`DateTimeOffset`) después de cargar la entidad, no mediante un `WHERE CreatedAt > ...` en la consulta SQL. El proveedor de SQLite usado en los tests de integración no sabe traducir comparaciones de `DateTimeOffset` a SQL (falla con *"could not be translated"*), mientras que PostgreSQL sí — comparar en memoria mantiene el mismo código funcionando igual en ambos, sin necesidad de una rama especial para tests, a coste despreciable porque siempre es una búsqueda de una sola fila por código único.

---

## 15. Testing

Vectores dorados compartidos en [`test-vectors/canonical-vectors.json`](test-vectors/canonical-vectors.json), usados tanto por los tests de C# como por los de TypeScript, para garantizar que ambas implementaciones producen exactamente los mismos hashes.

```bash
# Backend (xUnit): 43 tests de FairDraw.Crypto/Domain + 68 de integración (API completa, SQLite)
cd backend
dotnet test

# Frontend (Vitest): 50 tests de src/crypto/ y src/testing/
cd frontend
npm run test
```

Cobertura destacada:

- **Caducidad (§14):** un reloj controlable (`FakeTimeProvider`) permite saltar en el tiempo sin esperar; se prueba que un sorteo sigue disponible a los 29 días, deja de estarlo exactamente a los 30 (`GET`, `claim`, `commit` y `verification` responden 404), que esto aplica igual a un sorteo completado que a uno abandonado a medias, y que el purgador borra de verdad el sorteo y todas sus filas relacionadas (sin tocar los que aún están dentro del plazo).

- **Unit (crypto):** cada formato canónico y el rejection sampling contra los vectores dorados; caso de rechazo forzado (N=3, hash inyectado); N potencia de dos nunca rechaza; `winnerIndex` siempre dentro de rango.
- **Integración (API + SQLite):** validaciones de creación/lock; reclamar nombre (incluida una carrera de 8 intentos concurrentes sobre el mismo nombre, de los que solo uno debe ganar); commit (duplicado, hash inválido, fuera de fase); reveal (número/secreto incorrecto, reintento tras fallo, reveal duplicado tras uno válido); un test end-to-end que reproduce el ejemplo del §8 contra la API real y comprueba que el resultado coincide con un cálculo recomputado de forma independiente; concurrencia de los dos últimos commits/reveals simultáneos (`Task.WhenAll`).
- **Verificación:** nunca se exponen números/secretos antes de tiempo, ni reveals inválidos, en ningún endpoint público.

---

## 16. Estructura del repositorio

```
FairDraw/
├── README.md
├── docker-compose.yml
├── test-vectors/canonical-vectors.json
├── backend/
│   ├── src/
│   │   ├── FairDraw.Domain/
│   │   ├── FairDraw.Crypto/
│   │   ├── FairDraw.Infrastructure/
│   │   └── FairDraw.Api/
│   └── tests/
│       ├── FairDraw.UnitTests/
│       └── FairDraw.IntegrationTests/
└── frontend/
    └── src/
        ├── api/
        ├── crypto/
        ├── pages/
        └── testing/
```

---

## 17. Roadmap / fuera de alcance

Deliberadamente fuera de esta versión (documentado, no olvidado):

- Timeout/VDF para forzar el reveal de participantes que dejan de responder.
- Cuentas de usuario / prevención de Sybil.
- WebSockets para progreso en tiempo real (se usa polling cada 2s).
- Dockerfile de producción del frontend (build estático + nginx) — el actual sirve el servidor de desarrollo de Vite, adecuado para este MVP.
- Rate limiting y HTTPS obligatorio (necesarios antes de cualquier despliegue público real).
- Endpoint de listado de sorteos (por diseño: solo son descubribles por su código público).

---

## 18. Licencia

MIT.
