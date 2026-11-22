# Checker Service — Testing Guide

---

## 1. Test Stack & Suite Structure (`Checker.Tests`)

`src/services/Checker/Checker.Tests/` uses **xUnit (`2.9.3`)** and **ASP.NET Core `WebApplicationFactory<Program>` (`10.0.14`)** with zero external database or network dependencies.

```text
src/services/Checker/Checker.Tests/
├── Checker.Tests.csproj
├── Unit/
│   ├── DroneNetworkCheckerServiceTests.cs     # Single check & scan filtering logic, LAA, shared chipsets, perf (<50ms)
│   ├── DroneSignatureCatalogTests.cs          # All 18 dedicated OUIs/MA-Ms, 21 famous drone SSIDs, ReDoS safety (<100ms)
│   ├── MacAddressTests.cs                     # Colon/hyphen/dot/bare parsing, LAA & multicast bits, 00:00/FF:FF rejection
│   └── WiFiScanParsersTests.cs                # Netsh & Nmcli parsing, hidden SSIDs, escaped colons, channel->MHz->band
└── Integration/
    ├── CheckerApiFactory.cs                   # WebApplicationFactory<Program> with in-memory JWT key, rate limit & PathBase
    ├── BrowserSessionAuthTests.cs             # Cookie format short-circuit, valid & revoked cookie stubbing, Set-Cookie deletion
    ├── HealthAndStartupValidationTests.cs     # Anonymous /health bypasses rate limiter; weak/missing JWT key fail-fast
    ├── NetworkCheckAuthSecurityTests.cs       # Missing/expired/wrong-iss/wrong-aud/HS256/alg=none/bad-claims -> 401
    ├── NetworkCheckEndpointTests.cs           # Normalization, security headers, 400 validation, 404/405 ProblemDetails, 429 rate limit
    ├── NetworkScanEndpointTests.cs            # /networks/scan auth, StubWiFiScanner filtering, includePossible, 503 UnavailableScanner
    └── SwaggerEnvironmentTests.cs             # /swagger 200 in Development vs 404 in Production
```

---

## 2. Mocking & Test Isolation Strategy

- **Ephemeral JWT Signing Keys (`CheckerApiFactory`):** Generates a fresh 64-byte random Base64 key per `CheckerApiFactory` instance (`RandomNumberGenerator.GetBytes(64)`) and overrides `Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience`, `RateLimiting:LookupRequestsPerMinute`, and `Gateway:PathBase` in memory. Provides `CreateToken(...)` to mint valid or intentionally malformed JWTs (`alg: none`, `HS256`, expired, wrong issuer/audience, missing `sub`/`sid`/`jti`).
- **Stubbing `IWiFiScanner`:** Integration (`NetworkScanEndpointTests`) and Unit (`DroneNetworkCheckerServiceTests`) tests replace `IWiFiScanner` via DI (`services.RemoveAll<IWiFiScanner>()`) with deterministic `StubWiFiScanner` or `UnavailableScanner` implementations—so tests never touch host Wi-Fi hardware and pass in CI/Linux/Windows equally.
- **Stubbing `IManagementSessionValidator`:** `BrowserSessionAuthTests` replaces `IManagementSessionValidator` in DI with `StubManagementSessionValidator` / `TrackingManagementSessionValidator` to test valid cookies, revoked cookies, and malformed-cookie short-circuiting (`CallCount == 0` without calling Management).

---

## 3. Commands to Run Checker Tests

Run from `src/services/Checker` (or via `pnpm --filter checkerService test` from root):

```powershell
# Run full Checker test suite (Unit + Integration)
pnpm --filter checkerService test

# Equivalent dotnet CLI command
dotnet test src/services/Checker/Checker.slnx

# Run only Unit tests
dotnet test src/services/Checker/Checker.slnx --filter "FullyQualifiedName~Checker.Tests.Unit"

# Run only Integration tests
dotnet test src/services/Checker/Checker.slnx --filter "FullyQualifiedName~Checker.Tests.Integration"

# Run a single test class
dotnet test src/services/Checker/Checker.slnx --filter "FullyQualifiedName~NetworkScanEndpointTests"

# Detailed console output
dotnet test src/services/Checker/Checker.slnx --logger "console;verbosity=detailed"
```
