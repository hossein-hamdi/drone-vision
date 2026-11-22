# Checker Service (`src/services/Checker`)

The **Checker** service is a standalone `.NET 10.0` Clean Architecture microservice that evaluates wireless station BSSIDs (MAC addresses) and optional SSIDs against an embedded drone signature catalog, and executes live host Wi-Fi scans returning only detected drone stations with RF telemetry.

---

## 1. Tech Stack & Dependencies

| Component | Technology / Version | Notes |
| :--- | :--- | :--- |
| **Target Framework** | `.NET 10.0` (`net10.0`) | C# with `ImplicitUsings` and `Nullable` enabled |
| **Architecture** | Clean Architecture (5 projects) | `Checker.Domain`, `Checker.Application`, `Checker.Infrastructure`, `Checker.API`, `Checker.Tests` |
| **Storage / Signature Catalog** | Embedded JSON (`drone-signatures.json`) | Zero database dependency; loaded at startup into `FrozenDictionary` tables + compiled `RegexOptions.NonBacktracking` rules |
| **Authentication** | `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.14` + HTTP cookie validation | `"BrowserOrBearer"` dual scheme: stateless `HS512` JWT Bearer OR `__Secure-DroneVision.Refresh` validated via `Management` `/auth/me` |
| **API Docs** | `Swashbuckle.AspNetCore` `10.2.3` | Enabled in `Development` only; explicitly blocked (`404`) in non-Development |
| **OS Wi-Fi Scanning** | `netsh.exe` (Windows) / `nmcli` (`network-manager` in Linux Docker image) | Runtime `Dockerfile` installs `network-manager` (`nmcli`) |
| **Testing** | `xUnit` `2.9.3`, `Microsoft.AspNetCore.Mvc.Testing` `10.0.14`, `Microsoft.NET.Test.Sdk` `18.0.1` | 11 test files covering Domain/Application/Parsers (Unit) & HTTP/Auth/Swagger/RateLimit (Integration) |
| **Workspace** | `pnpm` workspace member `src/services/Checker` | Package name: `checkerService` (`Checker.slnx`) |

---

## 2. Directory & File Structure

```text
src/services/Checker/
├── Checker.slnx                                         # .NET solution file
├── Dockerfile                                           # Multi-stage: development (dotnet watch), build, runtime (installs network-manager, $APP_UID)
├── compose.yaml                                         # Production container config (:8080, Gateway__PathBase=/api/checker, Management__BaseUrl)
├── package.json                                         # pnpm scripts: dev, build, start, test, format, format:check
├── GEMINI.md                                            # Service invariants guide
├── .dockerignore                                        # Excludes bin, obj, .git, .env*, *.user, specs, .specify, .agents
├── .lintstagedrc.mjs                                    # Runs `dotnet format whitespace` on staged *.cs
├── Checker.Domain/                                      # Entities, enums, value objects (zero dependencies)
│   ├── Checker.Domain.csproj
│   ├── Entities/
│   │   ├── DroneSignatureEntry.cs                       # Curated IEEE OUI/MA-M/MA-S signature record
│   │   ├── FrequencyProfile.cs                          # Bands, MHz ranges, channel summary
│   │   └── SsidPatternRule.cs                           # Regex pattern rule + confidence weight
│   ├── Enums/                                           # ChipsetExclusivity, DetectionVerdict, FrequencyBand, MatchSource, SignalRangeTier, StationRole
│   └── ValueObjects/
│       ├── MacAddress.cs                                # IParsable<MacAddress>, MA-L/MA-M/MA-S prefixes, LAA & multicast bits
│       └── SsidName.cs                                  # 1..32 UTF-8 byte & no-control-character validation
├── Checker.Application/                                 # Contracts, DTOs, evaluators, scoring, ApiException
│   ├── Checker.Application.csproj
│   ├── Abstractions/                                    # IDroneNetworkCheckerService, IDroneSignatureCatalog, INetworkEvidenceEvaluator, IWiFiScanner
│   ├── Common/ApiException.cs                           # Typed HTTP domain exceptions (400, 401, 403, 404, 409, 503)
│   ├── DTOs/                                            # DroneNetworkCheckQuery, DroneNetworkCheckResponse, ScannedDroneNetworksDto
│   └── Services/
│       ├── DroneNetworkCheckerService.cs                # Orchestrates CheckAsync & ScanDroneNetworksAsync
│       ├── MacPrefixEvidenceEvaluator.cs                # Longest-prefix (36/28/24-bit) lookup
│       ├── SsidEvidenceEvaluator.cs                     # SSID regex pattern matching
│       └── VerdictScoringPolicy.cs                      # 6-branch conservative scoring & LAA handling
├── Checker.Infrastructure/                              # Embedded catalog, JWT/session options, OS Wi-Fi scanners
│   ├── Checker.Infrastructure.csproj                    # Embeds Catalog/Data/drone-signatures.json
│   ├── DependencyInjection.cs                           # AddCheckerServices() + ValidateOnStart options
│   ├── Authentication/
│   │   ├── JwtOptions.cs                                # Validates Jwt:SigningKey (>= 64 Base64 bytes)
│   │   └── ManagementSessionOptions.cs                  # Validates Management:BaseUrl (HTTP/HTTPS URI) & TimeoutSeconds (1..30)
│   ├── Catalog/
│   │   ├── Data/drone-signatures.json                   # 10 prefix groups & 19 SSID regex rules
│   │   └── EmbeddedDroneSignatureCatalog.cs             # FrozenDictionary prefix tables & NonBacktracking regexes
│   └── Scanning/
│       ├── NetshWiFiScanParser.cs                       # Parses Windows `netsh wlan show networks mode=bssid`
│       ├── NmcliWiFiScanParser.cs                       # Parses Linux `nmcli -t -f SSID,BSSID,SIGNAL,CHAN,FREQ,SECURITY`
│       ├── PlatformWiFiScanner.cs                       # Executes netsh.exe or nmcli with 8-second timeout
│       └── WiFiChannelConverter.cs                      # Channel <-> MHz <-> Band + Signal% -> approx RSSI dBm
├── Checker.API/                                         # HTTP host, controllers, auth handlers, Swagger, exception handlers
│   ├── Checker.API.csproj
│   ├── Program.cs                                       # Pipeline, ForwardedHeaders, Dual Auth, Global RateLimiter, Health, Swagger
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/launchSettings.json                   # Profiles: http (:5210), https (:7150; :5210)
│   ├── Authentication/
│   │   ├── BrowserSessionHandler.cs                     # Cookie scheme (__Secure-DroneVision.Refresh) via IManagementSessionValidator
│   │   ├── IManagementSessionValidator.cs
│   │   └── ManagementSessionValidator.cs                # HttpClient GET ${Management:BaseUrl}/auth/me (no redirects)
│   ├── Controllers/
│   │   ├── ApiController.cs                             # Base [ApiController], [Authorize], JSON & ProblemDetails attributes
│   │   └── NetworkCheckController.cs                    # GET /networks/check & GET /networks/scan
│   ├── ExceptionHandling/
│   │   ├── ApiProblems.cs                               # RFC 9457 enrichment (traceId) & ApiProblemDetailsWriter
│   │   └── ExceptionHandlers.cs                         # ValidationExceptionHandler -> ApiExceptionHandler -> GlobalExceptionHandler
│   └── OpenApi/SecurityOperationFilter.cs               # BrowserSession + Bearer security requirements
├── Checker.Tests/                                       # xUnit unit & integration tests
└── specs/001-drone-network-checker/                     # Feature spec, plan, research, data model & OpenAPI contracts
```

---

## 3. Ports & URLs

| Mode | URL / Port | Gateway Prefix | Swagger / Health URLs |
| :--- | :--- | :--- | :--- |
| **Docker (Dev & Prod)** | `http://+:8080` (internal network) | `/api/checker` (`Gateway__PathBase`) | Health: `https://localhost:8443/api/checker/health`<br/>Swagger (Dev only): `https://localhost:8443/api/checker/swagger/index.html` |
| **Native (`https` profile)** | `https://localhost:7150`, `http://localhost:5210` | `/` | Health: `https://localhost:7150/health`<br/>Swagger (Dev only): `https://localhost:7150/swagger/index.html` |
| **Native (`http` profile)** | `http://localhost:5210` | `/` | Health: `http://localhost:5210/health` |

---

## 4. Environment Variables & Configuration Keys

| Configuration Key | Env Var Syntax | Default (`appsettings.json` / Compose) | Description |
| :--- | :--- | :--- | :--- |
| `Jwt:Issuer` | `Jwt__Issuer` | `DroneVision` (Compose: `${JWT_ISSUER:-DroneVision}`) | Expected JWT `iss` claim |
| `Jwt:Audience` | `Jwt__Audience` | `DroneVision.Client` (Compose: `${JWT_AUDIENCE:-DroneVision.Client}`) | Expected JWT `aud` claim |
| `Jwt:SigningKey` | `Jwt__SigningKey` | *Required* (`${JWT_SIGNING_KEY}`) | Must match `Management`'s Base64 key (>= 64 bytes); validated eagerly at startup (`ValidateOnStart`) |
| `Management:BaseUrl` | `Management__BaseUrl` | Compose: `http://management:8080/api/management`; Native: `null` | Base URL used by `ManagementSessionValidator` to verify browser `__Secure-DroneVision.Refresh` cookies against `/auth/me`. Must be an absolute `http://` or `https://` URI when set. |
| `Management:TimeoutSeconds` | `Management__TimeoutSeconds` | `3` (range `1..30`) | `HttpClient` timeout when validating browser cookies with `Management` |
| `Gateway:PathBase` | `Gateway__PathBase` | Compose: `/api/checker`; Native: `null` | Base path passed to `app.UsePathBase(...)` |
| `RateLimiting:LookupRequestsPerMinute` | `RateLimiting__LookupRequestsPerMinute` | `60` (clamped `1..10000`) | Global per-IP fixed-window limit per minute (exempts `/health`) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Compose: `true` | Honors `X-Forwarded-For` and `X-Forwarded-Proto` (`Program.cs` also clears `KnownIPNetworks`/`KnownProxies`) |
| `UserSecretsId` | — | `DroneVision-Checker-API` | Local development user secrets ID |

---

## 5. Run, Build, Test & Format Commands

Run from `src/services/Checker` (or via `pnpm --filter checkerService <script>` from root):

```powershell
# Build Checker.API
pnpm --filter checkerService build

# Run natively with hot reload (recommended on Windows/Linux hosts when testing live Wi-Fi scan)
pnpm --filter checkerService dev

# Run natively without watch
pnpm --filter checkerService start

# Run all Unit & Integration tests (Checker.slnx)
pnpm --filter checkerService test

# Format C# whitespace / verify
pnpm --filter checkerService format
pnpm --filter checkerService format:check
```
