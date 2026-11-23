# GEMINI.md — Drone Network Checker Service (`src/services/Checker`)

## 1. Tech Stack & Workspace Overview
The **Checker** service (`src/services/Checker`) is a standalone `.NET 10.0` Clean Architecture microservice fronted by the **Ocelot API Gateway** (`src/gateway`). It inspects wireless station BSSIDs (MAC addresses) and optional SSIDs against curated IEEE drone OUI/MA-M/MA-S blocks, shared consumer Wi-Fi chipsets, and drone SSID naming conventions, and scans nearby Wi-Fi networks to return only detected drone stations.

- **Target Framework:** `.NET 10.0` (`net10.0`, C# with `ImplicitUsings` and `Nullable` enabled)
- **Architecture:** Clean Architecture (`Checker.Domain`, `Checker.Application`, `Checker.Infrastructure`, `Checker.API`, `Checker.Tests`)
- **Authentication & Docs:** `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.14`, `Swashbuckle.AspNetCore` `10.2.3`
- **Upstream API Gateway (`src/gateway`):** `ocelot.checker.json` routes `/api/checker/{everything}` to `checker:8080` at `Priority: 10`, strips unknown `/api/*` paths, blocks `/swagger` outside Development, and overwrites `X-Forwarded-Proto` / `X-Forwarded-For`
- **Storage:** Embedded JSON signature catalog (`Checker.Infrastructure/Catalog/Data/drone-signatures.json`) loaded into immutable `FrozenDictionary` indexes and compiled `RegexOptions.NonBacktracking` rules (zero database dependency)
- **Package/Task Runners:** `dotnet` CLI and `pnpm` scripts in `src/services/Checker/package.json`

### Key Scripts & Commands (`src/services/Checker`)
| Command | Purpose |
| :--- | :--- |
| `pnpm dev` (`dotnet watch run --project Checker.API --launch-profile https`) | Run Checker API with hot reload (`https://localhost:7150`, `http://localhost:5210`) |
| `pnpm build` (`dotnet build Checker.API/Checker.API.csproj`) | Compile Checker API |
| `pnpm start` (`dotnet run --project Checker.API --launch-profile https`) | Run Checker API without watch |
| `pnpm test` (`dotnet test Checker.slnx`) | Run unit and integration test suite |
| `pnpm format` (`dotnet format whitespace . --folder`) | Format C# whitespace |
| `pnpm format:check` (`dotnet format whitespace . --folder --verify-no-changes`) | Verify C# whitespace formatting |
| `pnpm docker:dev` / `pnpm docker:prod` (repo root) | Run full stack (SQL Server, Management, Checker, Frontend, Ocelot Gateway) via Docker Compose |

---

## 2. Directory Structure
```text
src/services/Checker/
├── Checker.slnx                                # .NET solution file
├── Dockerfile                                  # Multi-stage non-root ($APP_UID) container build (includes iw, nmcli, libcap2-bin)
├── compose.yaml                                # Production service definition for checker (safe bridge DNS)
├── compose.linux-wifi.yaml                     # Optional Linux host wireless D-Bus / NET_ADMIN Compose override
├── package.json                                # pnpm dev/build/start/test/format scripts
├── GEMINI.md                                   # Service architecture & invariants guide
├── .dockerignore
├── .lintstagedrc.mjs
├── Checker.Domain/                             # Entities, enums & value objects (zero project dependencies)
│   ├── Checker.Domain.csproj
│   ├── Entities/
│   │   ├── DroneSignatureEntry.cs              # Curated IEEE OUI/MA-M/MA-S signature record
│   │   ├── FrequencyProfile.cs                 # Supported Wi-Fi bands & channels
│   │   └── SsidPatternRule.cs                  # Compiled NonBacktracking SSID regex rule
│   ├── Enums/
│   │   ├── ChipsetExclusivity.cs
│   │   ├── DetectionVerdict.cs
│   │   ├── FrequencyBand.cs
│   │   ├── MatchSource.cs
│   │   ├── SignalRangeTier.cs
│   │   └── StationRole.cs
│   └── ValueObjects/
│       ├── MacAddress.cs                       # IParsable<MacAddress>, MA-L/MA-M/MA-S prefix & LAA/multicast extraction
│       └── SsidName.cs                         # 1..32 UTF-8 byte bounds & control-char validation
├── Checker.Application/                        # Use-case abstractions, DTOs, evaluators, scoring & ApiException
│   ├── Checker.Application.csproj
│   ├── Abstractions/
│   │   ├── IDroneNetworkCheckerService.cs      # CheckAsync, GetWlanInterfacesAsync & ScanDroneNetworksAsync
│   │   ├── IDroneSignatureCatalog.cs
│   │   ├── INetworkEvidenceEvaluator.cs
│   │   └── IWiFiScanner.cs                     # Host WLAN discovery & multi-interface scan abstraction
│   ├── Common/
│   │   └── ApiException.cs                     # Typed HTTP domain exceptions (400, 401, 403, 404, 503)
│   ├── DTOs/
│   │   ├── DroneNetworkCheckQuery.cs
│   │   ├── DroneNetworkCheckResponse.cs        # Streamlined 9-field /networks/check DTO
│   │   ├── DroneNetworkEvaluation.cs           # Internal full evidence evaluation before DTO projection
│   │   ├── ScannedDroneNetworksDto.cs          # ScannedDroneNetworksResponse, streamlined 15-field ScannedDroneNetworkDto & WiFiScanBatch
│   │   └── WlanInterfacesDto.cs                # WlanInterfaceInfo, WlanInterfaceDiscoveryResult & WlanInterfacesResponse
│   └── Services/
│       ├── DroneNetworkCheckerService.cs       # Orchestrates checks, WLAN discovery & cross-interface BSSID deduplication
│       ├── MacPrefixEvidenceEvaluator.cs       # Longest-prefix (36/28/24-bit) MAC lookup
│       ├── SsidEvidenceEvaluator.cs            # NonBacktracking SSID pattern evaluation
│       └── VerdictScoringPolicy.cs             # Conservative scoring & non-confirmation for shared chipsets
├── Checker.Infrastructure/                     # Embedded catalog, JWT/session options & OS Wi-Fi scanners
│   ├── Checker.Infrastructure.csproj
│   ├── DependencyInjection.cs                  # Registers catalog, evaluators, scoring, scanner & options
│   ├── Authentication/
│   │   ├── JwtOptions.cs                       # Validates Jwt:SigningKey (>= 64 Base64 bytes)
│   │   └── ManagementSessionOptions.cs         # Validates Management:BaseUrl
│   ├── Catalog/
│   │   ├── Data/drone-signatures.json          # Curated drone OUI/MA-M/MA-S & SSID signature dataset
│   │   └── EmbeddedDroneSignatureCatalog.cs    # FrozenDictionary prefix indexes & NonBacktracking regexes
│   └── Scanning/
│       ├── IwWiFiScanParser.cs                 # Parses Linux `iw dev` & `iw dev <iface> scan` output
│       ├── NetshWiFiScanParser.cs              # Parses Windows `netsh wlan show networks` & `show interfaces`
│       ├── NmcliWiFiScanParser.cs              # Parses Linux `nmcli` device status & Wi-Fi scan output
│       ├── PlatformWiFiScanner.cs              # Multi-WLAN discovery/scanning (`iw`/`nmcli` on Linux, `netsh` on Windows)
│       └── WiFiChannelConverter.cs             # Converts channel <-> MHz <-> FrequencyBand & RSSI <-> %
├── Checker.API/                                # Composition root, controllers, auth handlers, Swagger
│   ├── Checker.API.csproj
│   ├── Program.cs                              # Middleware pipeline, Dual Auth, RateLimiting, Security Headers, Swagger
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/launchSettings.json
│   ├── Authentication/
│   │   ├── BrowserSessionHandler.cs            # Cookie auth scheme (__Secure-DroneVision.Refresh) via Management
│   │   ├── IManagementSessionValidator.cs
│   │   └── ManagementSessionValidator.cs       # Calls Management /auth/me without redirects
│   ├── Controllers/
│   │   ├── ApiController.cs                    # [ApiController], [Authorize], JSON & ProblemDetails attributes
│   │   └── NetworkCheckController.cs           # GET /networks/check, GET /networks/interfaces & GET /networks/scan
│   ├── ExceptionHandling/
│   │   ├── ApiProblems.cs                      # RFC 9457 ProblemDetails enrichment with traceId
│   │   └── ExceptionHandlers.cs                # Ordered ValidationExceptionHandler -> ApiExceptionHandler -> GlobalExceptionHandler
│   └── OpenApi/
│       └── SecurityOperationFilter.cs          # Swagger BrowserSession + Bearer security requirements
├── Checker.Tests/                              # Unit & WebApplicationFactory integration tests
│   ├── Checker.Tests.csproj
│   ├── Integration/
│   │   ├── BrowserSessionAuthTests.cs
│   │   ├── CheckerApiFactory.cs
│   │   ├── HealthAndStartupValidationTests.cs
│   │   ├── NetworkCheckAuthSecurityTests.cs
│   │   ├── NetworkCheckEndpointTests.cs
│   │   ├── NetworkScanEndpointTests.cs
│   │   ├── SwaggerEnvironmentTests.cs
│   │   └── WlanInterfacesEndpointTests.cs
│   └── Unit/
│       ├── DroneNetworkCheckerServiceTests.cs
│       ├── DroneSignatureCatalogTests.cs
│       ├── MacAddressTests.cs
│       ├── PlatformWiFiScannerSafetyTests.cs
│       ├── VerdictScoringPolicyTests.cs
│       └── WiFiScanParsersTests.cs
└── specs/                                      # SpecKit specifications (001 & 002-multi-wlan-scan)
```

---

## 3. Core Files & Responsibilities
- `Checker.API/Program.cs`: Configures `UnmappedMemberHandling.Disallow` on JSON serialization, registers RFC 9457 `ProblemDetails` + 3 ordered `IExceptionHandler`s, configures `"BrowserOrBearer"` dual auth, registers IP fixed-window rate limiting (`429`), adds security response headers, configures `Gateway:PathBase`, and exposes `/health` anonymously plus `/swagger` in Development only.
- `Checker.API/Controllers/NetworkCheckController.cs`: Exposes `GET /networks/check`, `GET /networks/interfaces`, and `GET /networks/scan` with strict query parameter allowlisting (`RejectUnexpectedQueryKeys`).
- `Checker.API/Authentication/BrowserSessionHandler.cs` & `ManagementSessionValidator.cs`: Validates the 86-character `__Secure-DroneVision.Refresh` cookie against `Management:BaseUrl` (`/auth/me`) over `HttpClient` (`AllowAutoRedirect = false`), returning sanitized `401` responses on invalid/expired cookies.
- `Checker.Application/Services/DroneNetworkCheckerService.cs`: Orchestrates `CheckAsync` (streamlined 9-field lookup), `GetWlanInterfacesAsync` (WLAN discovery & availability status), and `ScanDroneNetworksAsync` (scans all usable interfaces via `IWiFiScanner`, deduplicates by canonical BSSID, and returns streamlined 15-field drone records).
- `Checker.Infrastructure/Catalog/EmbeddedDroneSignatureCatalog.cs`: Loads `drone-signatures.json` once at startup into immutable `FrozenDictionary` lookup tables for 36-bit MA-S, 28-bit MA-M, and 24-bit MA-L prefixes plus compiled `RegexOptions.NonBacktracking` SSID rules.
- `Checker.Infrastructure/Scanning/PlatformWiFiScanner.cs`: Discovers 802.11 WLAN adapters and scans across all usable interfaces (`netsh.exe` on Windows; `/sys/class/net/*/wireless` + `iw` or D-Bus `nmcli` on Linux), returning `available: false` on interface discovery and `503 Service Unavailable` on scan when wireless hardware is inaccessible (such as Windows Docker).

---

## 4. Signature Catalog & Wi-Fi Scanning Rules

### Signature Catalog (`drone-signatures.json`)
- **Zero-DB Embedded Resource:** Signatures are compiled into `Checker.Infrastructure.dll` as an embedded resource (`Catalog/Data/drone-signatures.json`)—no SQL Server or external database dependency.
- **Longest-Prefix Priority:** `MacPrefixEvidenceEvaluator` checks 36-bit MA-S (9 hex chars) -> 28-bit MA-M (7 hex chars) -> 24-bit MA-L (6 hex chars) so specific sub-allocations override broader OUI blocks.
- **Regex Safety:** All SSID patterns compile with `RegexOptions.NonBacktracking` and a match timeout to prevent ReDoS.

### Platform Wi-Fi Scanning Constraints
- **Linux Containers on Windows (Docker Desktop / WSL2):** Docker Desktop on Windows runs Linux containers inside a WSL2 VM without PCIe Wi-Fi passthrough (`eth0`/`lo` only). `GET /networks/interfaces` cleanly returns `200 OK` with `available: false` and `interfaces: []`, `GET /networks/scan` safely returns `503 Service Unavailable`, and `GET /networks/check` continues operating normally.
- **Linux Hosts / Linux Docker:** Both `iw` (`nl80211`) and `network-manager` (`nmcli`) are installed in `Dockerfile` while running as non-root `USER $APP_UID`. Mount `/run/dbus/system_bus_socket` via `compose.linux-wifi.yaml` (preserving Docker bridge DNS) or use host networking with `NET_ADMIN` for direct `iw` scanning across all WLAN adapters.

---

## 5. Code Snapshots & API Conventions

### Controller Base & Strict Query Allowlisting
All controllers inherit `ApiController` (`[ApiController]`, `[Authorize]`, `[Produces("application/json")]`, standard `ProblemDetails` response attributes) and reject unknown query parameters with `400 ValidationProblemDetails`.
```csharp
// CORRECT — Strict Query Key Allowlist (from NetworkCheckController.cs)
private static readonly HashSet<string> AllowedCheckQueryKeys = new(StringComparer.OrdinalIgnoreCase)
{
    "bssid",
    "ssid"
};

[HttpGet("check")]
[ProducesResponseType<DroneNetworkCheckResponse>(StatusCodes.Status200OK)]
public async Task<ActionResult<DroneNetworkCheckResponse>> Check(
    [FromQuery] string? bssid,
    [FromQuery] string? ssid,
    CancellationToken ct)
{
    RejectUnexpectedQueryKeys(AllowedCheckQueryKeys);
    var ssidWasProvided = Request.Query.ContainsKey("ssid");
    return Ok(await checkerService.CheckAsync(new DroneNetworkCheckQuery(bssid, ssid, ssidWasProvided), ct));
}
```

### Error Handling & RFC 9457 `ProblemDetails`
Never return ad-hoc error objects. Throw `ApiException` from Application/Infrastructure layers; the ordered `IExceptionHandler` pipeline (`ValidationExceptionHandler` -> `ApiExceptionHandler` -> `GlobalExceptionHandler`) formats RFC 9457 `ProblemDetails` with `traceId`.
```csharp
// CORRECT — Ordered IExceptionHandler (from ExceptionHandlers.cs)
if (exception is not ApiException { StatusCode: 400 } error) return false;
await ApiProblems.WriteAsync(context, new ValidationProblemDetails(error.Errors ?? new Dictionary<string, string[]>())
{
    Status = StatusCodes.Status400BadRequest,
    Title = error.Title,
    Detail = error.Message
});
return true;
```

---

## 6. Authentication & Security Hardening
- **Dual Transport (`BrowserOrBearer`):**
  - Requests with `Authorization: Bearer <jwt>` validate `HS512`, `ClockSkew = TimeSpan.Zero`, `Issuer`, `Audience`, `sub`, `sid`, and `jti` claims statelessly (`JwtOptions.HasStrongKey` enforces `>= 64` Base64-decoded random bytes on startup).
  - All other requests forward to `BrowserSessionHandler`, which verifies the 86-character `__Secure-DroneVision.Refresh` cookie via `Management:BaseUrl` (`/auth/me`).
- **Rate Limiting & Security Headers:** Pre-auth IP fixed-window rate limiter returns `429`; responses include `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, `X-Frame-Options: DENY`, and `Referrer-Policy: no-referrer`.
- **Gateway & Container Parity:** `Gateway:PathBase=/api/checker` configures `app.UsePathBase(...)` behind `src/gateway`. Production Dockerfile runs as non-root `USER $APP_UID` with `network-manager` (`nmcli`) installed and Swagger disabled outside `Development`.

---

## 7. Core Business Logic & Domain Invariants
- **Value Objects (`IParsable<MacAddress>`, `SsidName`):** `MacAddress` rejects empty, all-zero (`00:00:00:00:00:00`), and broadcast (`ff:ff:ff:ff:ff:ff`) MACs and extracts LAA/multicast bits. `SsidName` enforces `1..32` UTF-8 byte bounds with zero control characters.
- **Conservative Scoring (`VerdictScoringPolicy`):** Uncorroborated shared consumer Wi-Fi chipsets (e.g., Espressif / Broadcom / Realtek without a matching drone SSID) yield `DetectionVerdict.Possible` with `isDroneStation: false` and are excluded from `GET /networks/scan` unless `includePossible=true`.
- **Strict JSON Deserialization:** `UnmappedMemberHandling.Disallow` is enabled globally.
