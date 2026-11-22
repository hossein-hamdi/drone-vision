# Phase 0 Research: Drone Network Checker Service

**Feature**: `001-drone-network-checker` | **Date**: 2026-11-22

---

## 1. Architecture & C# Design Patterns

### Decision
Adopt a 4-project Clean Architecture (`Checker.Domain`, `Checker.Application`, `Checker.Infrastructure`, `Checker.API`) plus `Checker.Tests` targeting `.NET 10.0` (`net10.0`, C# with `ImplicitUsings` and `Nullable` enabled), using idiomatic C# design patterns:
- **Value Object & `IParsable<T>` Pattern**: `MacAddress` and `SsidName` encapsulate normalization, invariant enforcement (e.g., rejecting broadcast/all-zero MACs, detecting Locally Administered Addresses via bit 1 of octet 0, enforcing `<= 32` UTF-8 bytes on SSIDs).
- **Strategy + Composite Evidence Pattern**: `INetworkEvidenceEvaluator` defines independent evaluators (`MacPrefixEvidenceEvaluator`, `SsidEvidenceEvaluator`) composed by `DroneNetworkCheckerService` and resolved through `VerdictScoringPolicy`.
- **Repository / Catalog Pattern**: `IDroneSignatureCatalog` abstracts signature storage from evaluation; `EmbeddedDroneSignatureCatalog` loads a structured JSON dataset once into immutable frozen/read-only lookup indexes (`FrozenDictionary` for 24-bit, 28-bit, and 36-bit hex prefixes) and precompiled `RegexOptions.NonBacktracking` patterns.
- **Options + Fail-Fast Startup Validation**: `JwtOptions` binds `"Jwt"` config with `[Required]` DataAnnotations and `.Validate(JwtOptions.HasStrongKey, ...).ValidateOnStart()`, matching `Management.Infrastructure`.
- **Ordered Exception Handler Chain & RFC 9457 ProblemDetails**: Matches `Management.API` (`ValidationExceptionHandler` -> `ApiExceptionHandler` -> `GlobalExceptionHandler`) enriching responses with `traceId` while stripping stack traces and internals.

### Rationale
Matches `src/services/Management` conventions verbatim so maintainers experience zero architectural friction across services, while keeping domain logic pure, testable without HTTP hosting, and cleanly extensible as new drone OUIs or SSID patterns emerge.

### Alternatives Considered
- **Minimal API single-project service**: Fewer files initially, but mixes parsing, catalog loading, security config, and HTTP routing, failing the user's requirement for clean, readable C# design patterns and parity with `Management`.
- **SQL Server / EF Core database for signatures**: Adds migration and DB container coupling for a small, mostly static lookup dataset (~dozens to low hundreds of signatures) and increases latency; embedded structured JSON loaded into `FrozenDictionary` delivers sub-millisecond lookups and zero external failure points.

---

## 2. Drone OUI / MAC Address Prefixes & SSID Pattern Catalog

### Decision
Support **longest-prefix-first matching across variable IEEE prefix lengths** — **36-bit MA-S (9 hex chars)**, **28-bit MA-M (7 hex chars)**, and **24-bit MA-L / OUI (6 hex chars)** — paired with **explicit chipset exclusivity classification** (`DedicatedDroneVendor` vs `SharedConsumerChipset`) and **SSID correlation rules**.

### Verified IEEE OUI / MA-M Blocks & Famous Drone SSID Conventions
Every IEEE allocation below was verified against IEEE / MAC lookup registry data during Phase 0 research (rejecting common internet misattributions such as Cisco `00:2A:6A`, TI `34:08:E1`, and Microchip `68:27:19` that sometimes appear inaccurately on informal lists):

#### A. Dedicated Drone Manufacturers (Verified IEEE Allocations)
| Brand Mark | IEEE Block(s) | Block Type | Typical Drone Models / Series | Typical Operating Frequency Bands | Radio / Link Profile |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **DJI (SZ DJI Technology Co., Ltd.)** | `04:A8:5A`, `0C:9A:E6`, `34:D2:62`, `48:1C:B9`, `4C:43:F6`, `58:B8:58`, `60:60:1F`, `88:29:85`, `8C:58:23`, `E4:7A:2C` | MA-L (24-bit) | Mavic, Air, Mini, Phantom, Inspire, Matrice, Avata, Spark, FPV, Smart/RC Controllers | `2.4 GHz` (2400–2483.5 MHz), `5.8 GHz` (5725–5850 MHz), `5 GHz` UNII | Wi-Fi 802.11a/b/g/n/ac, Lightbridge, OcuSync (O2/O3/O4), Extended Range |
| **Parrot (Parrot SA / Parrot Drones)** | `00:12:1C`, `00:26:7E`, `90:03:B7`, `A0:14:3D` | MA-L (24-bit) | Anafi, Anafi USA, Bebop, Bebop 2, AR.Drone, Mambo, Swing, Skycontroller | `2.4 GHz` (2400–2483.5 MHz), `5 GHz` (5150–5850 MHz) | Wi-Fi 802.11a/b/g/n/ac, Medium/Short Range |
| **Skydio (Skydio Inc.)** | `38:1D:14` | MA-L (24-bit) | Skydio 2, Skydio 2+, X2, X10 | `2.4 GHz`, `5 GHz` (5150–5850 MHz) | Wi-Fi 802.11ac / Proprietary 5 GHz link, Extended Range |
| **Autel Robotics (Autel Robotics USA LLC)** | `EC:5B:CD:E` (`EC:5B:CD:E0:00:00`–`EF:FF:FF`) | MA-M (28-bit) | EVO, EVO II, EVO Nano, EVO Lite, Dragonfish, Smart Controller | `2.4 GHz`, `5.8 GHz` (plus `900 MHz` on select SkyLink regional variants) | Wi-Fi 802.11n/ac, SkyLink, Extended Range |
| **Hubsan (Shenzhen Hubsan Technology Co., Ltd.)** | `98:AA:FC:7` (`98:AA:FC:70:00:00`–`7F:FF:FF`) | MA-M (28-bit) | Zino, Zino Mini, Zino Pro, Ace, X4 FPV / H501 / H109S | `2.4 GHz`, `5.8 GHz` (5725–5850 MHz) | Wi-Fi 802.11n/ac, SyncLeas, Medium/Extended Range |
| **Yuneec (Yuneec International China Co., Ltd.)** | `E0:B6:F5:8` (`E0:B6:F5:80:00:00`–`8F:FF:FF`) | MA-M (28-bit) | Typhoon H, Typhoon Q500, Mantis Q, H520, H850, Breeze, ST16/ST10 Ground Station | `2.4 GHz`, `5.8 GHz` | Wi-Fi 802.11n/ac (5.8 GHz video link + 2.4 GHz control), Medium Range |

#### B. Shared Wi-Fi SoC / Module Vendors Frequently Found in Wi-Fi Drones
Budget and consumer FPV Wi-Fi drones (e.g., **Syma**, **MJX**, **Bugs**, **Holy Stone**, **Potensic**, **Eachine**, **JJRC**, **Cheerson**, **Ryze Tello**, **ELRS Wi-Fi modules**) typically embed white-label Wi-Fi SoCs rather than holding dedicated IEEE OUIs:
- **Espressif Inc.** (`24:0A:C4`, `30:AE:A4`, `A4:CF:12`, `C4:4F:33`, `84:CC:A8`, `EC:FA:BC`, etc.) — used in ESP32/ESP8266-based micro-drones, telemetry bridges, and ExpressLRS Wi-Fi configuration APs (`ExpressLRS TX`, `ExpressLRS RX`).
- **Shenzhen Bilian Electronic Co., Ltd.** (`20:F4:1B`, `00:1A:EF`, etc.) — widely used Wi-Fi camera/FPV module in toy & beginner drones (Syma, JJRC, Eachine, Holy Stone).
- **Realtek / MediaTek / HiSilicon / AzureWave / Broadcom** — common Wi-Fi modules inside Ryze Tello, MJX Bugs, Potensic, and Holy Stone aircraft.
*(Because these OUIs are shared with millions of non-drone IoT devices, BSSID-only lookups matching a shared OUI MUST return `DetectionVerdict.Possible`, `isDroneStation = false`, and note that SSID correlation is required unless a drone SSID is provided.)*

#### C. Famous Drone SSID Patterns (Prefix & Bounded Regex Rules)
Cataloged across major consumer, hobby, FPV, and enterprise brands:
- **DJI**: `^DJI[-_]`, `^Mavic[-_]?`, `^Phantom[-_]?[0-9]?`, `^Inspire[-_]?`, `^Spark[-_]?`, `^Matrice[-_]?`, `^Avata[-_]?`, `^DJI[-_]FPV`, `^Mini[-_]?[234]?[-_]?[A-Za-z0-9]{4,}` (carefully scoped so generic words like `Mini` alone don't over-match!)
- **Ryze / DJI Tello**: `^TELLO[-_][0-9A-Fa-f]{6}$`, `^RMTT[-_][0-9A-Fa-f]{6}$` (RoboMaster TT)
- **Syma**: `^SYMA[-_]?(FPV|HD|X[0-9]+)?[-_]?[A-Za-z0-9]*`, `^FPV[-_]WIFI[-_][0-9A-Za-z]+`, `^WIFI[-_]?UFO[-_][0-9A-Za-z]+` (note: `WIFI_UFO_*` is a well-known shared white-label drone app SSID used by Syma/Eachine/JJRC budget models -> tagged appropriately as multi-brand budget FPV drone!)
- **MJX / Bugs**: `^Bugs[-_]?[0-9A-Za-z]+`, `^MJX[-_]?[0-9A-Za-z_-]+`, `^B[0-9]+[WE]?[-_][0-9A-Fa-f]+` (e.g., MJX Bugs B2W/B5W/B16/B20 style SSIDs when matched with chipset or explicit `Bugs`)
- **Parrot**: `^ANAFI[-_][A-Za-z0-9]+`, `^Bebop(Drone|2)[-_][A-Za-z0-9]+`, `^Parrot[-_][A-Za-z0-9]+`, `^ardrone[0-9]?[-_][A-Za-z0-9]+`, `^Skycontroller[-_]?[A-Za-z0-9]*`
- **Autel Robotics**: `^Autel[-_]?(EVO|Nano|Lite|Dragonfish)?[-_]?[A-Za-z0-9]+`
- **Hubsan**: `^Hubsan[-_][A-Za-z0-9]+`, `^Zino[-_][A-Za-z0-9]+`, `^H501[AS][-_][A-Za-z0-9]+`
- **Yuneec**: `^Yuneec[-_][A-Za-z0-9]+`, `^Typhoon[-_][A-Za-z0-9]+`, `^Breeze[-_][A-Za-z0-9]+`, `^H520[-_][A-Za-z0-9]+`, `^ST16[-_][A-Za-z0-9]+`
- **Skydio**: `^Skydio[2X]?[-_][A-Za-z0-9]+`
- **Holy Stone**: `^HolyStone[-_][A-Za-z0-9]+`, `^HS(110|165|175|360|440|510|600|700|720)[A-Za-z]?[-_][A-Za-z0-9]+`
- **Potensic**: `^Potensic[-_][A-Za-z0-9]+`, `^Dreamer[-_][A-Za-z0-9]+`, `^Atom[-_]SE[-_][A-Za-z0-9]+`
- **Eachine**: `^Eachine[-_][A-Za-z0-9]+`, `^EFPV[-_][A-Za-z0-9]+`
- **JJRC**: `^JJRC[-_][A-Za-z0-9]+`, `^JJPRO[-_][A-Za-z0-9]+`
- **Cheerson**: `^Cheerson[-_][A-Za-z0-9]+`, `^CX[-_]?[0-9]{2,3}[-_][A-Za-z0-9]+`
- **Walkera**: `^Walkera[-_][A-Za-z0-9]+`, `^WK[-_]?(F210|Vitus|Rodeo)[-_][A-Za-z0-9]+`
- **3DR**: `^SoloLink[-_][A-Za-z0-9]+`, `^3DR[-_][A-Za-z0-9]+`
- **HoverAir / Zero Zero Robotics**: `^Hover(Air)?[-_][A-Za-z0-9]+`, `^ZeroZero[-_][A-Za-z0-9]+`
- **Open-Source FPV / ExpressLRS / BetaFPV**: `^ExpressLRS[-_](TX|RX)[-_]?[A-Za-z0-9]*`, `^ELRS[-_](TX|RX)`, `^OpenHD[-_]?`, `^ArduPilot[-_]?`

*(Every regex in `drone-signatures.json` will be validated in unit tests and compiled with `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking` + explicit match timeout `TimeSpan.FromMilliseconds(50)` so ReDoS is structurally impossible.)*

### Rationale
Combining longest-prefix IEEE blocks (MA-L/MA-M/MA-S) with chipset exclusivity flags and `NonBacktracking` SSID rules accurately identifies both dedicated drones (via BSSID alone) and white-label Wi-Fi drones like Syma/MJX/Bugs (via BSSID chipset + SSID correlation or explicit SSID), while preventing false positives on everyday IoT hardware.

### Alternatives Considered
- **Calling an external online MAC vendor API at runtime**: Unacceptable latency, external availability risk, outbound network exposure inside a security-hardened container, and still lacks drone model/frequency/SSID intelligence.
- **24-bit OUI-only string slicing**: Fails on IEEE 28-bit MA-M (`EC:5B:CD:E` Autel, `98:AA:FC:7` Hubsan, `E0:B6:F5:8` Yuneec) and 36-bit MA-S blocks.

---

## 3. Security, JWT Alignment & Gateway Integration

### Decision
1. **JWT Authentication Parity (`Management` Config)**:
   - Bind `"Jwt"` section to `JwtOptions` (`Issuer = "DroneVision"`, `Audience = "DroneVision.Client"`, `SigningKey` validated on startup to be Base64 of `>= 64` random bytes).
   - Configure `JwtBearerDefaults.AuthenticationScheme` identically to `Management.API/Program.cs`:
     - `MapInboundClaims = false`
     - `SaveToken = false`
     - `IncludeErrorDetails = false`
     - `ValidAlgorithms = [SecurityAlgorithms.HmacSha512]`
     - `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true`, `ValidateLifetime = true`, `RequireExpirationTime = true`, `RequireSignedTokens = true`, `ClockSkew = TimeSpan.Zero`, `NameClaimType = "sub"`
     - `OnTokenValidated`: verify `sub` and `sid` are positive integers and `jti` is non-empty.
     - `OnChallenge`: set `WWW-Authenticate: Bearer` and write sanitized `401` RFC 9457 `ProblemDetails` (`"A valid access token is required."`).
2. **Gateway Routing (`src/gateway/ocelot.checker.json`)**:
   - Mirror `src/gateway/ocelot.management.json`: route `/api/checker/{everything}` at `Priority: 10` over `http` to `checker:8080`.
   - `src/gateway/Program.cs` automatically extracts `/api/checker` into `servicePrefixes`, enforces `/api/*` allowlisting, overwrites `X-Forwarded-Proto`/`X-Forwarded-For`, and blocks `/api/checker/swagger` outside Development.
   - Set `Gateway__PathBase=/api/checker` in `Checker/compose.yaml` and call `app.UsePathBase(...)` before routing so Swagger servers and `ProblemDetails.Instance` match `/api/checker/...`.
3. **API Hardening Controls**:
   - **Rate Limiting**: Fixed-window rate limiter (`"checker-lookup"`, default 60 req/min configurable via `RateLimiting:LookupRequestsPerMinute`, partitioned by authenticated `sub` claim if authenticated else `RemoteIpAddress` — wait: note `UseRateLimiter()` runs *after* `UseAuthentication()` if partitioned by user, OR runs *before* `UseAuthentication()` partitioned by IP to shield JWT signature verification from CPU DoS! Partitioning by IP *before* authentication — or having a pre-auth IP limiter like `Management` — prevents unauthenticated token-spam CPU exhaustion!). Using pre-auth IP rate limiting (`60` req/min default, configurable `1..10000`, `429` ProblemDetails response) + `Health` exempt protects cryptographic verification from DoS!
   - **Forwarded Headers**: Configure `ForwardedHeadersOptions` (`XForwardedFor | XForwardedProto`) safely when `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` behind the Docker gateway so per-IP rate limiting sees the real client IP forwarded by `Gateway`.
   - **Security Headers Middleware**: `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, plus `app.UseHsts()` in non-Development.
   - **Strict Input Validation**: Enforce `bssid` required (`[Required]`, max length 32, strict `MacAddress.TryParse`, reject `00:00:00:00:00:00` and `FF:FF:FF:FF:FF:FF`), optional `ssid` (`<= 32` UTF-8 bytes, reject ASCII control characters `< 0x20` or `0x7F`), and reject unknown query parameters on the lookup endpoint? Wait: standard query binding ignores extra query params unless explicitly rejected; adding an action filter or validating `Request.Query.Keys` against allowed keys (`bssid`, `ssid`) cleanly rejects unexpected query parameters (`FR-010`) with `400` ValidationProblemDetails!
   - **Swagger Gating**: `app.Environment.IsDevelopment()` only, with Bearer security scheme + `SecurityOperationFilter` + XML doc comments.
   - **Health Endpoint**: `GET /health` (`[AllowAnonymous]`, excluded from rate limiting, returning minimal JSON `{ "status": "Healthy" }`) for Docker healthcheck without exposing system internals.

### Rationale
Matches `Management` and `Gateway` security posture line-for-line while addressing service-specific threats (unauthenticated JWT verification CPU DoS, ReDoS on user-supplied SSIDs, unknown parameter smuggling, container privilege escalation).

### Alternatives Considered
- **Validations in `Gateway` only**: Violates `src/gateway/GEMINI.md` invariant (*"No Business Logic or Auth Verification in Gateway"*); downstream services must enforce their own authentication and input validation (defense-in-depth).
- **Sharing Management SQL Server for `sid` lookup**: Introduces tight database coupling and blocks independent deployment (`FR-012`).

---

## 4. Containerization & Workspace Integration

### Decision
- Multi-stage `src/services/Checker/Dockerfile` mirroring `src/services/Management/Dockerfile`:
  - `development` stage on `mcr.microsoft.com/dotnet/sdk:10.0` running `dotnet watch`
  - `build` stage running `dotnet publish Checker.API/Checker.API.csproj -c Release -o /app --no-restore`
  - `runtime` stage on `mcr.microsoft.com/dotnet/aspnet:10.0` running as non-root `USER $APP_UID` on port `8080`
- `src/services/Checker/compose.yaml` + root `compose.yaml` include + `compose.dev.yaml` watch entry + `src/gateway/compose.yaml` `depends_on: checker` + `src/services/Checker/package.json` registered in `pnpm-workspace.yaml`.
- Note on Docker runtime healthcheck: ASP.NET 10 runtime base images (`mcr.microsoft.com/dotnet/aspnet:10.0`) are chiseled/minimal or may lack `curl`/`wget`! Instead of relying on `curl` in `Dockerfile`, verify whether `curl` exists or avoid broken `curl` healthchecks by either checking with `dotnet` / Compose or leaving host/port readiness clean (we can test `mcr.microsoft.com/dotnet/aspnet:10.0` or check `Management/Dockerfile` — notice `Management/Dockerfile` has NO `HEALTHCHECK` using `curl`, and `Management` exposes `/health` or HTTP on `8080`; or an HTTP readiness probe via Compose/k8s or minimal probe!). Let's document `/health` endpoint cleanly in `contracts/openapi.yaml` and `compose.yaml`!

### Rationale
Guarantees seamless `pnpm docker:dev` / `pnpm docker:prod` execution from the repository root using the shared `.env` (`JWT_SIGNING_KEY`).
