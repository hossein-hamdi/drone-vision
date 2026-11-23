# Implementation Plan: Multi-WLAN Drone Scanning & Streamlined Network Responses

**Branch**: `002-multi-wlan-scan` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-multi-wlan-scan/spec.md`

## Summary

Extend **Checker** (`src/services/Checker`) to:
1. **Enumerate host WLAN interfaces** via a new authenticated `GET /networks/interfaces` endpoint that filters strictly to 802.11 WLAN adapters (`iw` + `/sys/class/net/*/wireless` or `nmcli` on Linux; `netsh wlan show interfaces` on Windows) and returns `200 OK` with `available: false` and `interfaces: []` when wireless hardware is inaccessible (such as Windows Docker).
2. **Scan across all usable WLAN interfaces and deduplicate results** in `GET /networks/scan`, merging duplicate BSSID observations across adapters (preferring strongest signal, preserving non-empty SSIDs, recording sorted `interfaceNames`, and tolerating partial single-interface failures), while returning sanitized `503 Service Unavailable` when no wireless scanning capability exists (Windows Docker).
3. **Install Linux wireless scanning dependencies safely in `Dockerfile`** (`iw`, `network-manager`, `libcap2-bin`, with best-effort `setcap` on `iw` and non-root `USER $APP_UID`), keeping default `compose.yaml` bridge networking intact for Gateway routing across both Windows Docker and Linux Docker.
4. **Streamline `/networks/check` and `/networks/scan` response payloads** to the confirmed Balanced Core field sets (9 fields on `/check`, 15 fields per drone on `/scan`), separating internal `DroneNetworkEvaluation` richness from external DTO projection so domain scoring tests remain comprehensive.

## Technical Context

**Language/Version**: C# / `.NET 10.0` (`net10.0`, `ImplicitUsings` enabled, `Nullable` enabled)

**Primary Dependencies**:
- Existing ASP.NET Core 10 Web API (`JwtBearer` `10.0.14`, `Swashbuckle.AspNetCore` `10.2.3`, `System.Threading.RateLimiting`, RFC 9457 `ProblemDetails`)
- Linux container OS utilities added to `Dockerfile`: `iw`, `network-manager` (`nmcli`), `libcap2-bin` (for `nl80211` scanning without running `NetworkManager` daemon inside the container)
- Windows native OS utility: `%SystemRoot%\System32\netsh.exe` (`wlan show interfaces` & `wlan show networks interface=... mode=bssid`)

**Storage**: Zero database dependency (unchanged embedded JSON signature catalog in `Checker.Infrastructure/Catalog/Data/drone-signatures.json`)

**Testing**: `xUnit` + `Microsoft.AspNetCore.Mvc.Testing` (`Checker.Tests` unit tests for parsers, deduplication, streamlined DTO mapping, and integration tests for `/networks/interfaces`, `/networks/scan`, `/networks/check`, `503` Windows Docker fallback, `400`/`401` security)

**Target Platform**: Linux Docker containers (`mcr.microsoft.com/dotnet/aspnet:10.0` / `sdk:10.0` running on Linux hosts AND Windows Docker Desktop), plus native Windows and Linux host execution

**Project Type**: Standalone Clean Architecture microservice (`Checker.Domain`, `Checker.Application`, `Checker.Infrastructure`, `Checker.API`, `Checker.Tests`) behind Ocelot API Gateway (`/api/checker/*`)

**Performance Goals**:
- `/networks/check`: < 50 ms warm latency (unchanged)
- `/networks/interfaces`: < 1 s bounded timeout (instant < 10 ms when `/sys/class/net/*/wireless` is empty in Windows Docker)
- `/networks/scan`: Bounded multi-interface scan timeout (e.g., 10–12 s cap across all interfaces with immediate process termination on cancellation/timeout)

**Constraints**:
- Non-root container execution (`USER $APP_UID`) preserved in `Dockerfile`
- Default `compose.yaml` preserves Docker bridge DNS (`http://checker:8080`) so Windows Docker and Linux Docker never fail Gateway routing
- Safe subprocess execution (`UseShellExecute = false`, `ArgumentList`, strict interface-name allowlisting, no shell interpolation)
- Sanitized error responses (`ProblemDetails` / `WlanInterfacesResponse.Reason`) never expose raw OS command strings, paths, or stack traces

**Scale/Scope**: Single-host multi-adapter scanning (typically 1–8 WLAN interfaces per host, dozens to hundreds of nearby BSSIDs deduplicated in memory in < 5 ms)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` retains starter template placeholders; therefore, compliance is evaluated against the project's established architecture and security invariants in `GEMINI.md` and `specs/001-drone-network-checker`:

| Principle / Repository Invariant | Status | Plan Alignment |
| --- | --- | --- |
| Clean Architecture Layer Boundaries | PASS | `Checker.Domain` stays pure; `Checker.Application` defines `IWiFiScanner` multi-interface abstractions, deduplication, and streamlined DTOs; `Checker.Infrastructure` implements OS scanners (`iw`/`nmcli`/`netsh`) and parsers; `Checker.API` stays thin. |
| Security & Auth Parity | PASS | `GET /networks/interfaces` inherits `ApiController` (`[Authorize]`), dual `"BrowserOrBearer"` auth, IP rate limiting, security headers, and strict empty-query allowlisting (`400` on unknown query keys). Subprocess invocations avoid shells and validate interface names. |
| Cross-Platform Container Safety | PASS | Windows Docker returns safe `200 { available: false }` on `/interfaces` and `503` on `/scan` without crashing; Linux Docker installs `iw` + `network-manager` in `Dockerfile` while preserving non-root `USER $APP_UID` and bridge networking. |
| Test-First & Automated Verification | PASS | New parser unit tests (`NetshWlanInterfacesParser`, `IwWiFiScanParser`), deduplication/partial-failure service tests, streamlined payload assertions, and `WebApplicationFactory` integration tests cover every new path. |
| Zero Over-Engineering | PASS | Uses native OS tools (`iw`/`nmcli`/`netsh`) already aligned with `PlatformWiFiScanner`, zero extra NuGet packages, zero database dependency. |

**Post-Design Re-Check (after Phase 1)**: Confirmed PASS — `research.md`, `data-model.md`, `contracts/openapi.yaml`, `contracts/gateway-route.md`, and `quickstart.md` satisfy all Clean Architecture, security, Windows/Linux Docker, and streamlined contract requirements with zero unresolved clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/002-multi-wlan-scan/
├── spec.md                    # Resolved feature specification (all 3 clarifications incorporated)
├── plan.md                    # This implementation plan
├── research.md                # Phase 0 research: iw vs nmcli, netsh multi-iface, deduplication, DTO slimming
├── data-model.md              # Phase 1 data model: WlanInterfaceInfo, WiFiScanBatch, deduplication & streamlined DTOs
├── quickstart.md              # Phase 1 validation guide (tests, Windows Docker, native, Linux Docker)
├── checklists/
│   └── requirements.md        # Validated specification checklist (all items passed)
└── contracts/
    ├── openapi.yaml           # Updated OpenAPI 3.0.3 spec (/networks/check, /networks/interfaces, /networks/scan)
    └── gateway-route.md       # Gateway parity & Windows/Linux Docker deployment contract
```

### Source Code (repository root: `src/services/Checker`)

```text
src/services/Checker/
├── Dockerfile                                  # Updated: install iw, network-manager, libcap2-bin (+ setcap) while keeping USER $APP_UID
├── compose.yaml                                # Preserved safe bridge networking for Windows Docker & Linux Docker
├── Checker.Domain/                             # Unchanged core value objects, entities & enums
├── Checker.Application/
│   ├── Abstractions/
│   │   ├── IDroneNetworkCheckerService.cs      # Add GetWlanInterfacesAsync(ct)
│   │   └── IWiFiScanner.cs                     # Add DiscoverInterfacesAsync(ct); ScanAsync returns WiFiScanBatch
│   ├── DTOs/
│   │   ├── DroneNetworkCheckResponse.cs        # Streamlined to 9 essential fields
│   │   ├── ScannedDroneNetworksDto.cs          # Streamlined ScannedDroneNetworkDto (15 fields) + ScannedInterfaces + WiFiScanBatch
│   │   └── WlanInterfacesDto.cs                # New: WlanInterfaceInfo, WlanInterfaceDiscoveryResult, WlanInterfacesResponse, WlanInterfaceDto
│   └── Services/
│       ├── DroneNetworkCheckerService.cs       # Multi-interface deduplication + GetWlanInterfacesAsync + streamlined mapping
│       └── VerdictScoringPolicy.cs             # Returns internal DroneNetworkEvaluation (retaining diagnostics for unit tests)
├── Checker.Infrastructure/
│   └── Scanning/
│       ├── PlatformWiFiScanner.cs              # Multi-WLAN discovery & scanning (Linux: iw + sysfs / nmcli fallback; Windows: netsh)
│       ├── IwWiFiScanParser.cs                 # New: parses `iw dev` interfaces and `iw dev <iface> scan` BSS output
│       ├── NetshWiFiScanParser.cs              # Updated: tracks `Interface name :` + added NetshWlanInterfacesParser
│       ├── NmcliWiFiScanParser.cs              # Updated: parses interface status & per-interface Wi-Fi rows
│       └── WiFiChannelConverter.cs             # Updated: add FrequencyMhzToChannel & RssiDbmToApproxSignalPercent
├── Checker.API/
│   └── Controllers/
│       └── NetworkCheckController.cs           # Add GET /networks/interfaces + strict query allowlist; update Swagger XML docs
└── Checker.Tests/
    ├── Unit/
    │   ├── DroneNetworkCheckerServiceTests.cs  # Updated for streamlined DTOs + new multi-interface deduplication & GetWlanInterfaces tests
    │   └── WiFiScanParsersTests.cs             # Updated + IwWiFiScanParser, NetshWlanInterfacesParser, NmcliInterfaceParser tests
    └── Integration/
        ├── NetworkCheckEndpointTests.cs        # Verify 9 streamlined fields & absence of removed fields
        ├── NetworkScanEndpointTests.cs         # Verify 15 streamlined fields, deduplication, scannedInterfaces, 503 fallback
        └── WlanInterfacesEndpointTests.cs      # New: 401 auth, 400 unexpected query, 200 available=true/false
```

**Structure Decision**: Preserve the existing 4-layer Clean Architecture layout (`Domain`, `Application`, `Infrastructure`, `API`) and `Checker.Tests` project established in `001-drone-network-checker`. All OS-specific wireless tooling (`iw`, `nmcli`, `netsh`, `/sys/class/net`) remains strictly isolated in `Checker.Infrastructure/Scanning`, while cross-interface deduplication and streamlined DTO mapping live cleanly in `Checker.Application`.

## Complexity Tracking

No constitution violations require justification.
