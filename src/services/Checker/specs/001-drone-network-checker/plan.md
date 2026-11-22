# Implementation Plan: Drone Network Checker Service

**Branch**: `001-drone-network-checker` | **Date**: 2026-11-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-drone-network-checker/spec.md`

> **Clarification Note**: `/speckit-clarify` was initiated and the user proceeded directly to `/speckit-plan` before answering interactive questions. Planning proceeds using the documented defaults in `spec.md` (`Assumptions`: stateless Management-compatible `HS512` JWT validation, single `bssid` + optional `ssid` GET lookup, conservative `isDroneStation = false` on uncorroborated shared Wi-Fi chipsets, gateway pass-through authentication). This carries minor downstream adjustment risk only if real-time session-revocation coupling or role-based permissions against `Management` are required later.

## Summary

Build **Checker** (`src/services/Checker`), an independent `.NET 10.0` (C#) Clean Architecture microservice that inspects a wireless station BSSID (and optional SSID) via a read-only `GET` API to determine whether it corresponds to a drone station, controller, or telemetry link. It evaluates normalized MAC addresses against variable-length IEEE OUI/MA-M/MA-S allocations (dedicated drone vendors and shared consumer Wi-Fi chipsets) combined with ReDoS-safe SSID pattern rules covering DJI, Syma, MJX, Bugs, Parrot, Autel, Hubsan, Yuneec, Skydio, Ryze Tello, Holy Stone, Potensic, Eachine, JJRC, Cheerson, Walkera, 3DR, HoverAir/Zero Zero, and ELRS/FPV ecosystems.

The service exposes its endpoints behind the existing Ocelot API Gateway (`src/gateway/ocelot.checker.json` at `/api/checker/*`), enforces stateless JWT Bearer authentication matching `Management`'s `JwtOptions` (`HS512`, `Issuer`, `Audience`, Base64 >= 64-byte `SigningKey`, `ClockSkew = TimeSpan.Zero`, `sub`/`sid`/`jti` claims), applies strict input validation, rate limiting, security headers, RFC 9457 `ProblemDetails`, and environment-gated Swagger/OpenAPI documentation, and ships with a hardened non-root multi-stage `Dockerfile` and Compose configuration.

## Technical Context

**Language/Version**: C# / `.NET 10.0` (`net10.0`, `ImplicitUsings` enabled, `Nullable` enabled, matching `src/services/Management` and `src/gateway`)

**Primary Dependencies**:
- `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.14` (JWT validation matching `Management.API`)
- `Swashbuckle.AspNetCore` `10.2.3` (Development-only Swagger/OpenAPI UI & schema generation matching `Management.API`)
- Built-in ASP.NET Core Rate Limiting (`System.Threading.RateLimiting`), `IExceptionHandler` + RFC 9457 `ProblemDetails`, `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Ocelot` `25.0.2` (existing gateway in `src/gateway`, new route file `ocelot.checker.json`)

**Storage**: Embedded / packaged structured JSON signature catalog (`drone-signatures.json`) loaded once at startup into immutable, indexed in-memory lookup structures (zero database dependency; decoupled from `Management` SQL Server)

**Testing**: `xUnit` (`Microsoft.NET.Test.Sdk`, `xunit`, `Microsoft.AspNetCore.Mvc.Testing`) for domain unit tests (MAC parsing, LAA detection, OUI/MA-M/MA-S longest-prefix matching, SSID matching, scoring), API security integration tests (JWT claims/alg/expiry/clock-skew, rate limiting, security headers, Swagger production lockout), and contract verification

**Target Platform**: Linux container (`mcr.microsoft.com/dotnet/aspnet:10.0` runtime, `mcr.microsoft.com/dotnet/sdk:10.0` build/dev) orchestrated via Docker Compose behind `src/gateway`

**Project Type**: Standalone backend microservice (`web-service` Clean Architecture solution under `src/services/Checker`) + Gateway route config (`src/gateway/ocelot.checker.json`) + Root Compose wiring

**Performance Goals**: Single lookup p95 latency < 50 ms after warm startup (`SC-005`); O(1) prefix dictionary/trie lookup and `RegexOptions.NonBacktracking` SSID matching with bounded 50 ms evaluation timeouts

**Constraints**:
- Zero database or synchronous runtime dependency on `Management`
- Non-root container execution (`USER $APP_UID`), fail-fast startup validation if `Jwt:SigningKey` < 64 random bytes Base64 or missing `Issuer`/`Audience`
- Zero internal error/cryptographic detail leakage (`IncludeErrorDetails = false`, sanitized `ProblemDetails` with `traceId`)
- Swagger/OpenAPI strictly disabled in non-Development environments (both inside `Checker.API` and via `Gateway` boundary guard)

**Scale/Scope**: Initial curated catalog of ~20+ drone brand ecosystems (15+ verified dedicated IEEE OUI/MA-M blocks, ~10+ shared consumer drone Wi-Fi SoC OUIs, 35+ bounded SSID pattern rules); designed cleanly for trivial extension via catalog entries without modifying matching engine logic

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Note: `.specify/memory/constitution.md` in `src/services/Checker` currently retains starter template placeholders (`[PROJECT_NAME]`, `[PRINCIPLE_1_NAME]`, etc.). Accordingly, this gate evaluates compliance against both the constitution template's core software engineering principles and the established repository governance documented in `src/services/Management/GEMINI.md` and `src/gateway/GEMINI.md`:

| Principle / Repository Invariant | Status | Plan Alignment |
| --- | --- | --- |
| Clean Modularity & Library-First Domain | PASS | Pure domain logic (`Checker.Domain`) has zero framework dependencies; application orchestration (`Checker.Application`) depends only on abstractions. |
| Test-First & Automated Verification | PASS | Dedicated `Checker.Tests` project covers domain unit tests (every brand, OUI length, LAA, shared chipset, invalid MAC), integration tests (`WebApplicationFactory` JWT, rate-limit, Swagger gating, headers). |
| Security & Auth Parity with `Management` | PASS | Exact `JwtOptions` validation (`HasStrongKey` >= 64 bytes Base64, `ValidateOnStart`), `HS512` algorithm pinning, `ClockSkew = Zero`, `sub`/`sid`/`jti` validation, rate limiting, security headers, non-root Docker user. |
| Gateway Boundary Invariants (`src/gateway`) | PASS | `ocelot.checker.json` uses `/api/checker/{everything}` with `Priority: 10`, `Gateway__PathBase=/api/checker`, pass-through JWT (no auth logic in Gateway), automatic prod `/swagger` 404 lockout. |
| Simplicity & Zero Over-Engineering | PASS | Zero external DB/cache infrastructure; immutable in-memory indexed catalog loaded once at startup; standard C# patterns (Strategy, Composite/Chain scoring, Repository abstraction, Options, `IParsable`). |

**Post-Design Re-Check (after Phase 1)**: Confirmed PASS — `data-model.md`, `contracts/openapi.yaml`, `contracts/gateway-route.md`, and `quickstart.md` preserve strict Clean Architecture isolation, ReDoS-safe pattern evaluation, zero `Management` DB coupling, and exact gateway/JWT compatibility.

## Project Structure

### Documentation (this feature)

```text
specs/001-drone-network-checker/
├── plan.md              # This implementation plan (/speckit-plan output)
├── research.md          # Phase 0 research: IEEE OUI/MA-M verification, C# patterns, JWT/Gateway security
├── data-model.md        # Phase 1 data model: Domain value objects, catalog schema, DTOs, scoring matrix
├── quickstart.md        # Phase 1 runnable validation scenarios (local & Docker + Gateway)
├── contracts/
│   ├── openapi.yaml     # OpenAPI 3.0.3 API specification (GET lookup + health + JWT security + ProblemDetails)
│   └── gateway-route.md # Ocelot route & Compose environment contract for src/gateway + root compose
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code & Repository Integration Layout

```text
src/services/Checker/                          # Active service workspace
├── .dockerignore
├── .lintstagedrc.mjs
├── Checker.slnx                               # Solution file (Domain, Application, Infrastructure, API, Tests)
├── Dockerfile                                 # 3-stage build: development, build, runtime (non-root USER $APP_UID)
├── compose.yaml                               # Production service definition (Gateway__PathBase=/api/checker, Jwt__*)
├── package.json                               # pnpm scripts mirroring src/services/Management/package.json
├── Checker.Domain/                            # Zero dependencies: Value Objects, Enums, Domain Entities
│   ├── Checker.Domain.csproj
│   ├── Enums/
│   │   ├── ChipsetExclusivity.cs              # DedicatedDroneVendor, SharedConsumerChipset
│   │   ├── DetectionVerdict.cs                # Confirmed, Probable, Possible, NotRecognized
│   │   ├── FrequencyBand.cs                   # Band24GHz, Band5GHz, Band58GHz, Band900MHz, SubGHz433MHz
│   │   ├── MatchSource.cs                     # None, Oui, Ssid, OuiAndSsid
│   │   ├── SignalRangeTier.cs                 # ShortRangeWiFi, MediumRangeWiFi, ExtendedRangeDigital, Unknown
│   │   └── StationRole.cs                     # AircraftAp, GroundController, TelemetryModule, FpvVtx, Unknown
│   ├── Entities/
│   │   ├── DroneSignatureEntry.cs             # Catalog record for OUI/MA-M/MA-S prefix & brand metadata
│   │   ├── FrequencyProfile.cs                # Frequency bands, spectrum ranges (MHz), channel notes
│   │   └── SsidPatternRule.cs                 # Prefix/regex rule linked to brand/model/band/confidence
│   └── ValueObjects/
│       ├── MacAddress.cs                      # IParsable<MacAddress>, normalization, OUI/MA-M/MA-S extraction, LAA/multicast checks
│       └── SsidName.cs                        # Validated <= 32 UTF-8 byte / safe printable SSID value object
├── Checker.Application/                       # Use-case abstractions, Strategy/Scoring pipeline, DTOs, ApiException
│   ├── Checker.Application.csproj
│   ├── Abstractions/
│   │   ├── IDroneNetworkCheckerService.cs
│   │   ├── IDroneSignatureCatalog.cs
│   │   └── INetworkEvidenceEvaluator.cs       # Strategy interface for MAC prefix & SSID evaluators
│   ├── Common/
│   │   └── ApiException.cs                    # Typed HTTP domain exceptions (400, 401, 403, 404)
│   ├── DTOs/
│   │   ├── DroneNetworkCheckQuery.cs
│   │   └── DroneNetworkCheckResponse.cs
│   └── Services/
│       ├── DroneNetworkCheckerService.cs      # Orchestrates evaluators + VerdictScoringPolicy
│       ├── MacPrefixEvidenceEvaluator.cs      # Longest-prefix (36/28/24-bit) strategy
│       ├── SsidEvidenceEvaluator.cs           # Non-backtracking SSID pattern strategy
│       └── VerdictScoringPolicy.cs            # Deterministic verdict, confidence score & conservative LAA/shared rules
├── Checker.Infrastructure/                    # Catalog loader, JwtOptions, DI registration
│   ├── Checker.Infrastructure.csproj
│   ├── Authentication/
│   │   └── JwtOptions.cs                      # Matches Management JwtOptions + HasStrongKey (>= 64 bytes Base64)
│   ├── Catalog/
│   │   ├── EmbeddedDroneSignatureCatalog.cs   # Immutable indexed dictionary + compiled NonBacktracking regex cache
│   │   └── Data/
│   │       └── drone-signatures.json          # Verified IEEE drone OUIs, shared chipsets, famous SSID patterns
│   └── DependencyInjection.cs
├── Checker.API/                               # Composition root, Controller, ExceptionHandling, Auth, Swagger, Health
│   ├── Checker.API.csproj
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── Controllers/
│   │   ├── ApiController.cs                   # Base controller ([ApiController], [Authorize], Produces ProblemDetails)
│   │   └── NetworkCheckController.cs          # GET networks/check?bssid=...&ssid=...
│   ├── ExceptionHandling/
│   │   ├── ApiProblems.cs                     # RFC 9457 ProblemDetails enricher & writer (with traceId)
│   │   └── ExceptionHandlers.cs               # Ordered IExceptionHandler chain
│   ├── OpenApi/
│   │   └── SecurityOperationFilter.cs         # Bearer security requirement annotation for Swagger
│   └── Program.cs                             # PathBase, Security headers, RateLimiter, JwtBearer, Health, Swagger (Dev)
└── Checker.Tests/                             # Automated unit & API integration tests
    ├── Checker.Tests.csproj
    ├── Unit/
    │   ├── MacAddressTests.cs                 # Parsing, formatting, LAA, broadcast/zero rejection
    │   ├── DroneSignatureCatalogTests.cs      # Catalog integrity, regex ReDoS timeout safety, brand coverage
    │   └── DroneNetworkCheckerServiceTests.cs # OUI+SSID correlation, conservative shared chipset, DJI/Syma/MJX/Parrot/Autel
    └── Integration/
        ├── CheckerApiFactory.cs               # WebApplicationFactory + HS512 test token generator
        ├── NetworkCheckAuthSecurityTests.cs   # Missing/expired/wrong-iss/wrong-aud/alg-none/HS256/missing-claims 401 tests
        ├── NetworkCheckEndpointTests.cs       # Happy paths, 400 validation, 429 rate limiting, security headers
        └── SwaggerEnvironmentTests.cs         # Dev enabled vs Production 404 lockout

# Cross-repository integration files (outside src/services/Checker, inside D:\projects\drone-analyze):
src/gateway/
├── ocelot.checker.json                        # Priority 10 route: /api/checker/{everything} -> checker:8080
└── compose.yaml                               # Add checker to gateway depends_on
compose.yaml                                   # Include ./src/services/Checker/compose.yaml
compose.dev.yaml                               # Add checker development hot-reload watch definition
pnpm-workspace.yaml                            # Register src/services/Checker workspace member
```

**Structure Decision**: Follow the exact 4-layer Clean Architecture layout used by `src/services/Management` (`Domain`, `Application`, `Infrastructure`, `API`) plus a dedicated `Checker.Tests` project, cleanly separated from `src/gateway` (`ocelot.checker.json`) and root orchestration (`compose.yaml` / `compose.dev.yaml` / `pnpm-workspace.yaml`). Using Clean Architecture avoids coupling business logic to ASP.NET Core or JSON catalog storage while keeping the codebase immediately familiar to contributors working in `Management`.

## Complexity Tracking

No constitution violations require justification. All four Clean Architecture assemblies and the test assembly mirror existing repository standards (`src/services/Management`), and omitting a database keeps infrastructure complexity minimal.
