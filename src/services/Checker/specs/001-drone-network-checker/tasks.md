---

description: "Executable task list for Drone Network Checker Service"
---

# Tasks: Drone Network Checker Service

**Input**: Design documents from `/specs/001-drone-network-checker/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/openapi.yaml`, `contracts/gateway-route.md`, `quickstart.md`

**Tests**: Unit and API integration tests are included per `plan.md` (`Checker.Tests`) and `spec.md` acceptance scenarios (`SC-001`–`SC-007`). Write tests first inside each story phase and verify they fail before implementing production logic.

**Organization**: Tasks are grouped by user story (`US1`–`US4`) in priority order so each slice is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unresolved dependencies)
- **[Story]**: User story label (`[US1]`, `[US2]`, `[US3]`, `[US4]`) for story-phase tasks only

## Path Conventions

- Service files reside under `src/services/Checker/` (written below relative to `src/services/Checker/`, e.g., `Checker.Domain/...`).
- Gateway and monorepo orchestration files outside `src/services/Checker/` use monorepo-relative paths from `D:\projects\drone-analyze\` (explicitly prefixed `../../gateway/...` or `../../../...`).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize the `.NET 10.0` Clean Architecture solution, workspace scripts, and base configuration.

- [X] T001 Initialize solution `Checker.slnx` and project files `Checker.Domain/Checker.Domain.csproj`, `Checker.Application/Checker.Application.csproj`, `Checker.Infrastructure/Checker.Infrastructure.csproj`, `Checker.API/Checker.API.csproj`, and `Checker.Tests/Checker.Tests.csproj` targeting `net10.0` with `ImplicitUsings` and `Nullable` enabled, `GenerateDocumentationFile=true` on `Checker.API`, `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.14`, `Swashbuckle.AspNetCore` `10.2.3`, `xunit`, and `Microsoft.AspNetCore.Mvc.Testing`
- [X] T002 [P] Create `package.json` (`dev`, `build`, `start`, `test`, `format`, `format:check` pnpm scripts matching `src/services/Management/package.json`) and `.lintstagedrc.mjs` in `src/services/Checker/`
- [X] T003 [P] Create `Checker.API/appsettings.json`, `Checker.API/appsettings.Development.json`, and `Checker.API/Properties/launchSettings.json` with `Jwt` (`Issuer: "DroneVision"`, `Audience: "DroneVision.Client"`), `RateLimiting:LookupRequestsPerMinute: 60`, and logging defaults

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain enums/entities, exception handling, RFC 9457 `ProblemDetails`, and base API pipeline required before any user story.

**⚠️ CRITICAL**: No user story implementation work can begin until this phase is complete.

- [X] T004 [P] Create domain enums `DetectionVerdict` (`NotRecognized`, `Possible`, `Probable`, `Confirmed`), `MatchSource` (`None`, `Oui`, `Ssid`, `OuiAndSsid`), `ChipsetExclusivity` (`DedicatedDroneVendor`, `SharedConsumerChipset`), `StationRole` (`Unknown`, `AircraftAp`, `GroundController`, `FpvVtx`, `TelemetryModule`, `MultiRole`), `FrequencyBand` (`Band433MHz`, `Band900MHz`, `Band24GHz`, `Band5GHz`, `Band58GHz`), and `SignalRangeTier` (`Unknown`, `ShortRangeWiFi`, `MediumRangeWiFi`, `ExtendedRangeDigital`) in `Checker.Domain/Enums/`
- [X] T005 [P] Create domain catalog entities `FrequencyProfile` (`Bands`, `FrequencyRangesMhz`, `ChannelSummary`), `DroneSignatureEntry` (`Id`, `Prefixes` of normalized hex lengths 6/7/9, `VendorName`, `BrandMark`, `Exclusivity`, `CandidateModels`, `StationRole`, `FrequencyProfile`, `RadioProtocols`, `SignalRangeTier`, `AssociatedSsidPatterns`, optional `Notes`), and `SsidPatternRule` (`Id`, `BrandMark`, `Pattern`, `DisplayPattern`, `CandidateModels`, `StationRole`, `FrequencyProfile`, `RadioProtocols`, `SignalRangeTier`, `ConfidenceWeight` constrained to `1..100`, optional `AssociatedChipsetVendors`, optional `Notes`) in `Checker.Domain/Entities/`
- [X] T006 [P] Create typed domain exception `ApiException` (`Validation` 400 with field error dictionary, `Unauthorized` 401, `NotFound` 404, `Conflict` 409) in `Checker.Application/Common/ApiException.cs`
- [X] T007 Configure RFC 9457 `ProblemDetails` enrichment (`traceId`, `instance`, `type`) and ordered `IExceptionHandler` pipeline (`ValidationExceptionHandler` -> `ApiExceptionHandler` -> `GlobalExceptionHandler`) omitting stack traces and internals in `Checker.API/ExceptionHandling/ApiProblems.cs` and `Checker.API/ExceptionHandling/ExceptionHandlers.cs`
- [X] T008 Create base `Checker.API/Controllers/ApiController.cs` (`[ApiController]`, `[Authorize]`, `[Produces("application/json")]`, `ProblemDetails` response types) and configure base JSON serialization (`DefaultIgnoreCondition = WhenWritingNull`, `UnmappedMemberHandling = Disallow`), `InvalidModelStateResponseFactory`, and `Gateway:PathBase` (`app.UsePathBase(...)`) in `Checker.API/Program.cs`

**Checkpoint**: Foundation ready — user story phases (`US1`–`US4`) can now proceed cleanly.

---

## Phase 3: User Story 1 - Identify Drone Stations by BSSID and Optional SSID (Priority: P1) 🎯 MVP

**Goal**: Parse and normalize `bssid` and optional `ssid`, evaluate longest-prefix IEEE MAC allocations and ReDoS-safe SSID rules, apply conservative shared-chipset scoring, and return full drone metadata via `GET /networks/check`.

**Independent Test**: Run `dotnet test` for `MacAddressTests` and `DroneNetworkCheckerServiceTests`, and verify `GET /networks/check?bssid=...&ssid=...` returns `Confirmed` for dedicated drone OUIs and shared-chipset+SSID matches, `Possible` (`isDroneStation: false`) for uncorroborated shared chipsets, `NotRecognized` for unknown/non-drone OUIs, and `400` for invalid inputs.

### Tests for User Story 1

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US1 production classes.**

- [X] T009 [P] [US1] Write unit tests for `MacAddress` parsing (`AA:BB:CC:DD:EE:FF`, `AA-BB-CC-DD-EE-FF`, `AABB.CCDD.EEFF`, `AABBCCDDEEFF`, mixed case/whitespace), `Oui24`/`Prefix28`/`Prefix36` slicing, LAA detection (`(firstByte & 0x02) != 0`), multicast detection, and rejection of empty/all-zero (`00:00:00:00:00:00`)/broadcast (`FF:FF:FF:FF:FF:FF`)/malformed MACs in `Checker.Tests/Unit/MacAddressTests.cs`
- [X] T010 [P] [US1] Write unit tests for `SsidName` (`1..32` UTF-8 bytes, no control chars `< 0x20` or `0x7F`), longest-prefix matching (36-bit > 28-bit > 24-bit), dedicated vendor vs shared chipset + SSID correlation (`Confirmed`, `Probable`, `Possible`, `NotRecognized`), and LAA caveats in `Checker.Tests/Unit/DroneNetworkCheckerServiceTests.cs`

### Implementation for User Story 1

- [X] T011 [P] [US1] Implement `MacAddress` (`IParsable<MacAddress>`, `Canonical`, `CompactHex`, `Oui24`, `Prefix28`, `Prefix36`, `IsLocallyAdministered`, `IsMulticast`, rejecting empty, all-zero, broadcast, and non-12-hex strings) in `Checker.Domain/ValueObjects/MacAddress.cs` and `SsidName` (enforcing `1..32` UTF-8 bytes when non-null and rejecting ASCII control characters `< 0x20` or `0x7F`) in `Checker.Domain/ValueObjects/SsidName.cs`
- [X] T012 [P] [US1] Create Application interfaces (`IDroneSignatureCatalog`, `INetworkEvidenceEvaluator`, `IDroneNetworkCheckerService`) in `Checker.Application/Abstractions/` and DTOs (`DroneNetworkCheckQuery`, `DroneNetworkCheckResponse` matching `contracts/openapi.yaml` required/nullable fields) in `Checker.Application/DTOs/`
- [X] T013 [US1] Implement `MacPrefixEvidenceEvaluator` (longest-prefix priority 36-bit MA-S -> 28-bit MA-M -> 24-bit MA-L lookup) and `SsidEvidenceEvaluator` (bounded `RegexOptions.NonBacktracking` matching) in `Checker.Application/Services/MacPrefixEvidenceEvaluator.cs` and `Checker.Application/Services/SsidEvidenceEvaluator.cs`
- [X] T014 [US1] Implement `VerdictScoringPolicy` (enforcing the `data-model.md` Section 4 scoring table: conservative `isDroneStation=false`/`Possible` for uncorroborated shared chipsets, `Confirmed` for dedicated vendors or shared chipset + SSID, LAA flag attribution downgrade) and `DroneNetworkCheckerService` in `Checker.Application/Services/VerdictScoringPolicy.cs` and `Checker.Application/Services/DroneNetworkCheckerService.cs`
- [X] T015 [US1] Implement `EmbeddedDroneSignatureCatalog` loading `Checker.Infrastructure/Catalog/Data/drone-signatures.json` into immutable `FrozenDictionary` prefix tables and compiled `RegexOptions.NonBacktracking` rules with 50 ms match timeout in `Checker.Infrastructure/Catalog/EmbeddedDroneSignatureCatalog.cs`, seed initial baseline DJI/Syma/MJX/Parrot/Autel/Espressif/Bilian entries in `Checker.Infrastructure/Catalog/Data/drone-signatures.json`, and wire DI in `Checker.Infrastructure/DependencyInjection.cs`
- [X] T016 [US1] Implement `Checker.API/Controllers/NetworkCheckController.cs` exposing `[HttpGet("networks/check")]` accepting `bssid` (required) and `ssid` (optional), rejecting any unexpected query keys outside `bssid`/`ssid` with `400` `ValidationProblemDetails`, and returning `DroneNetworkCheckResponse` (`200`)

**Checkpoint**: User Story 1 is functional and independently testable (MVP core lookup complete).

---

## Phase 4: User Story 2 - Gateway Access & Management JWT Security Enforcement (Priority: P1)

**Goal**: Enforce Management-compatible `HS512` JWT authentication, pre-auth rate limiting, input/query hardening, defensive security headers, and Ocelot API Gateway routing under `/api/checker/*`.

**Independent Test**: Run `NetworkCheckAuthSecurityTests` and `NetworkCheckEndpointTests` verifying 200 with valid `HS512` tokens, 401 on invalid/expired/wrong-issuer/wrong-audience/weak-algorithm/missing-claim tokens, 400 on malformed/unexpected inputs, 429 on bursts, security headers on all responses, and valid `ocelot.checker.json` gateway route configuration.

### Tests for User Story 2

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US2 security & gateway integration.**

- [X] T017 [P] [US2] Create `Checker.Tests/Integration/CheckerApiFactory.cs` (`WebApplicationFactory<Program>` with test `HS512` signing key >= 64 bytes) and write JWT enforcement tests (missing token, malformed token, expired token at 0 clock skew, wrong issuer/audience, `HS256`/unsigned `alg=none`, missing `sub`/`sid`/`jti` claims, sanitized `401` `ProblemDetails` with `WWW-Authenticate: Bearer`) in `Checker.Tests/Integration/NetworkCheckAuthSecurityTests.cs`
- [X] T018 [P] [US2]: Write API security hardening tests (normalized MAC notations, `400` on invalid BSSID / >32-byte SSID / control chars / unexpected query parameters, `429` rate limit throttling, `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`) in `Checker.Tests/Integration/NetworkCheckEndpointTests.cs`

### Implementation for User Story 2

- [X] T019 [P] [US2] Create `Checker.Infrastructure/Authentication/JwtOptions.cs` (`[Required] Issuer = "DroneVision"`, `[Required] Audience = "DroneVision.Client"`, `[Required] SigningKey`, `HasStrongKey` verifying Base64 >= 64 random bytes) and register `.BindConfiguration("Jwt").ValidateDataAnnotations().Validate(JwtOptions.HasStrongKey, ...).ValidateOnStart()` in `Checker.Infrastructure/DependencyInjection.cs`
- [X] T020 [US2] Configure `JwtBearerDefaults.AuthenticationScheme` in `Checker.API/Program.cs` matching `Management.API` (`MapInboundClaims = false`, `SaveToken = false`, `IncludeErrorDetails = false`, `ValidAlgorithms = [SecurityAlgorithms.HmacSha512]`, `ClockSkew = TimeSpan.Zero`, `NameClaimType = "sub"`, `OnTokenValidated` requiring positive int `sub` & `sid` and non-empty `jti`, sanitized `OnChallenge`/`OnForbidden` `ProblemDetails`, `FallbackPolicy = RequireAuthenticatedUser()`)
- [X] T021 [US2] Configure pre-authentication IP fixed-window rate limiter (`"checker-lookup"`, default 60 req/min configurable `1..10000` via `RateLimiting:LookupRequestsPerMinute`, returning `429` `ProblemDetails`), `ForwardedHeadersOptions`, `UseHsts()` outside Development, and security headers middleware (`X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`) in `Checker.API/Program.cs`
- [X] T022 [US2] Create Ocelot gateway route configuration in `../../gateway/ocelot.checker.json` routing `/api/checker/{everything}` to `http://checker:8080/api/checker/{everything}` at `Priority: 10` per `contracts/gateway-route.md`

**Checkpoint**: User Stories 1 and 2 work together with full security controls and gateway routing.

---

## Phase 5: User Story 3 - Comprehensive Drone Signature Catalog & Safe Documentation Discovery (Priority: P2)

**Goal**: Expand the curated drone signature database across 18+ drone brands/ecosystems with verified IEEE prefixes, shared chipsets, frequency bands/ranges, and ReDoS-safe SSID patterns, and enable environment-gated Swagger UI in Development only.

**Independent Test**: Run `DroneSignatureCatalogTests` verifying coverage and accuracy across all target brands plus regex safety, and run `SwaggerEnvironmentTests` confirming Swagger UI/OpenAPI JSON loads in Development and returns `404` in Production.

### Tests for User Story 3

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US3 catalog expansion & Swagger.**

- [X] T023 [P] [US3] Write catalog completeness, brand accuracy, and ReDoS timeout unit tests verifying DJI (`04:A8:5A`, `0C:9A:E6`, `34:D2:62`, `48:1C:B9`, `4C:43:F6`, `58:B8:58`, `60:60:1F`, `88:29:85`, `8C:58:23`, `E4:7A:2C`), Parrot (`00:12:1C`, `00:26:7E`, `90:03:B7`, `A0:14:3D`), Skydio (`38:1D:14`), Autel (`EC:5B:CD:E`), Hubsan (`98:AA:FC:7`), Yuneec (`E0:B6:F5:8`), Syma, MJX, Bugs, Ryze Tello, Holy Stone, Potensic, Eachine, JJRC, Cheerson, Walkera, 3DR, HoverAir, and ExpressLRS in `Checker.Tests/Unit/DroneSignatureCatalogTests.cs`
- [X] T024 [P] [US3] Write integration tests verifying `/swagger/index.html` and `/swagger/v1/swagger.json` return `200` with `Bearer` security schema and `Gateway:PathBase` server URL in `Development` and return `404` in `Production` in `Checker.Tests/Integration/SwaggerEnvironmentTests.cs`

### Implementation for User Story 3

- [X] T025 [US3] Populate the full verified curated dataset in `Checker.Infrastructure/Catalog/Data/drone-signatures.json` (and embed/copy asset in `Checker.Infrastructure/Checker.Infrastructure.csproj`) covering all 18+ drone brands, verified MA-L (24-bit) & MA-M (28-bit) IEEE blocks, shared Wi-Fi SoC OUIs (`Espressif`, `Shenzhen Bilian`, `Realtek`, `HiSilicon`, `MediaTek`, `AzureWave`, `Broadcom`), 35+ bounded SSID patterns, operating frequency bands/ranges (MHz), channel summaries, radio link standards, and signal range tiers from `research.md`
- [X] T026 [P] [US3] Create `Checker.API/OpenApi/SecurityOperationFilter.cs` attaching the `Bearer` security requirement to authorized operations and omitting `[AllowAnonymous]` endpoints
- [X] T027 [US3] Configure `AddSwaggerGen` (`OpenApiInfo`, `Checker.API.xml` comments, `Bearer` HTTP security scheme, `SecurityOperationFilter`) and `app.Environment.IsDevelopment()`-gated `UseSwagger` (setting `document.Servers` from `request.PathBase`) and `UseSwaggerUI` in `Checker.API/Program.cs`

**Checkpoint**: User Stories 1, 2, and 3 are complete with comprehensive brand coverage and environment-safe API documentation.

---

## Phase 6: User Story 4 - Standalone Service Isolation & Containerized Operations (Priority: P3)

**Goal**: Provide unauthenticated health/readiness reporting, fail-fast security startup guarantees, hardened non-root multi-stage Dockerfile, and root Docker Compose / pnpm workspace wiring.

**Independent Test**: Run `HealthAndStartupValidationTests` verifying `/health` returns `200 Healthy` without auth and startup aborts on invalid `Jwt:SigningKey`; build Docker images and verify non-root UID and full Compose startup.

### Tests for User Story 4

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US4 health/container wiring.**

- [X] T028 [P] [US4] Write integration tests verifying `GET /health` returns `200` (`{"status":"Healthy"}`) without authentication or rate limiting, and service startup throws `OptionsValidationException` when `Jwt:SigningKey` is missing or shorter than 64 Base64-decoded bytes in `Checker.Tests/Integration/HealthAndStartupValidationTests.cs`

### Implementation for User Story 4

- [X] T029 [P] [US4] Configure `AddHealthChecks()` and map `GET /health` (`AllowAnonymous`, `DisableRateLimiting`, minimal JSON `{ "status": "Healthy" }`) in `Checker.API/Program.cs`
- [X] T030 [P] [US4] Create `src/services/Checker/.dockerignore` and 3-stage `src/services/Checker/Dockerfile` (`development` on `mcr.microsoft.com/dotnet/sdk:10.0` with `dotnet watch`, `build` Release publish, `runtime` on `mcr.microsoft.com/dotnet/aspnet:10.0` with `USER $APP_UID`, `EXPOSE 8080`, `ASPNETCORE_URLS=http://+:8080`)
- [X] T031 [P] [US4] Create `src/services/Checker/compose.yaml` defining the `checker` service (`Gateway__PathBase=/api/checker`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey=${JWT_SIGNING_KEY:?Set JWT_SIGNING_KEY in .env}`, `expose: ["8080"]`)
- [X] T032 [US4] Integrate `checker` into monorepo orchestration: include `./src/services/Checker/compose.yaml` in `../../../compose.yaml`, add `checker` hot-reload watch override in `../../../compose.dev.yaml`, add `checker` to `depends_on` in `../../gateway/compose.yaml`, and add `src/services/Checker` to `../../../pnpm-workspace.yaml`

**Checkpoint**: All 4 user stories are independently implemented, tested, containerized, and wired into the platform gateway and Compose stack.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, code formatting verification, full test suite check, and end-to-end validation.

- [X] T033 [P] Create `src/services/Checker/GEMINI.md` and `src/services/Checker/README.md` documenting Clean Architecture structure, design patterns, JWT/Gateway parity, and instructions for extending `drone-signatures.json`
- [X] T034 Run `dotnet format whitespace . --folder --verify-no-changes` and `dotnet test Checker.slnx` in `src/services/Checker/` to confirm clean formatting and zero test regressions
- [X] T035 Validate Docker build and end-to-end gateway scenarios per `specs/001-drone-network-checker/quickstart.md` (health endpoint, Development Swagger, Management JWT login, DJI/Autel/Syma/MJX/Espressif/non-drone lookups, Production Swagger 404 lockout, non-root UID check)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — starts immediately.
- **Foundational (Phase 2)**: Depends on Phase 1 (`T001`–`T003`) — BLOCKS all user stories.
- **User Story 1 (Phase 3 - P1 MVP)**: Depends on Phase 2 (`T004`–`T008`).
- **User Story 2 (Phase 4 - P1 Security & Gateway)**: Depends on Phase 2 (`T004`–`T008`); integrates with `NetworkCheckController` (`T016`) and `Program.cs` (`T008`) — can develop Auth options/gateway config in parallel with US1 and wire into `Program.cs` once US1 controller exists.
- **User Story 3 (Phase 5 - P2 Catalog & Swagger)**: Depends on Phase 2 and US1 (`T015` catalog loader, `T016` API controller); expands catalog & adds Swagger middleware.
- **User Story 4 (Phase 6 - P3 Containerization & Ops)**: Depends on Phase 2 (`Program.cs`) and US2 (`JwtOptions`, `Gateway:PathBase`); Dockerfile/Compose definitions (`T030`–`T032`) can be authored in parallel once `.csproj` paths (`T001`) are established.
- **Polish (Phase 7)**: Depends on Phases 3–6 completion.

### Within Each User Story

- Tests (`T009`–`T010`, `T017`–`T018`, `T023`–`T024`, `T028`) are written first and must fail before implementing production code.
- Value objects & DTOs before evaluators/services; services before controllers/middleware; internal wiring before gateway/Compose integration.

### Parallel Opportunities

- **Phase 1**: `T002` and `T003` run in parallel after `T001`.
- **Phase 2**: `T004`, `T005`, and `T006` run in parallel before `T007` and `T008`.
- **US1**: `T009` and `T010` run in parallel; `T011` and `T012` run in parallel.
- **US2**: `T017` and `T018` run in parallel; `T019` and `T022` run in parallel with `T020`–`T021` (respecting `Program.cs` single-writer order).
- **US3**: `T023` and `T024` run in parallel; `T025` and `T026` run in parallel.
- **US4**: `T029`, `T030`, and `T031` run in parallel before `T032`.

---

## Parallel Example: User Story 1 (MVP)

```text
# 1. Write US1 unit tests in parallel:
T009 [P] [US1] MacAddressTests in Checker.Tests/Unit/MacAddressTests.cs
T010 [P] [US1] DroneNetworkCheckerServiceTests in Checker.Tests/Unit/DroneNetworkCheckerServiceTests.cs

# 2. Implement Domain value objects and Application contracts/DTOs in parallel:
T011 [P] [US1] MacAddress & SsidName in Checker.Domain/ValueObjects/
T012 [P] [US1] Abstractions & DTOs in Checker.Application/
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2 Security Baseline)

1. Complete Phase 1 (Setup) + Phase 2 (Foundational).
2. Complete Phase 3 (**US1** — core drone BSSID/SSID lookup engine & `GET /networks/check`).
3. Complete Phase 4 (**US2** — Management-compatible `HS512` JWT enforcement, rate limiting, security headers, and `ocelot.checker.json`).
4. **STOP & VALIDATE**: Run `dotnet test Checker.slnx` to verify full lookup accuracy + strict JWT security.

### Incremental Delivery

1. **MVP**: Setup + Foundational + US1 + US2 -> authenticated, gateway-routed drone lookup service.
2. **Catalog & DX Expansion**: Add US3 -> complete 18+ brand signature catalog + Development-only Swagger UI.
3. **Production Packaging**: Add US4 + Polish -> hardened non-root Dockerfile, Compose orchestration, formatting checks, and full `quickstart.md` validation.
