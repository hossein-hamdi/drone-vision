---

description: "Executable task list for Multi-WLAN Drone Scanning & Streamlined Network Responses"
---

# Tasks: Multi-WLAN Drone Scanning & Streamlined Network Responses

**Input**: Design documents from `/specs/002-multi-wlan-scan/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/openapi.yaml`, `contracts/gateway-route.md`, `quickstart.md`

**Tests**: Unit and API integration tests are included per `plan.md` (`Checker.Tests`) and `quickstart.md`. Write tests first inside each story phase and verify they fail before implementing production logic.

**Organization**: Tasks are grouped by user story (`US1`–`US4`) in priority order so each slice is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (`[US1]`, `[US2]`, `[US3]`, `[US4]`) for story-phase tasks only
- All service file paths are relative to `src/services/Checker/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify baseline solution health before modifying scanning abstractions, parsers, DTOs, and container definitions.

- [X] T001 Verify baseline solution builds and passes existing tests with `dotnet test Checker.slnx` in `Checker.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared RF conversion helpers, multi-interface scanner contracts, and DTO foundations required before any user story phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Add `FrequencyMhzToChannel(int freqMhz)` (mapping 2.4 GHz `2412..2472` to `1..13`, `2484` to `14`, 5/5.8 GHz `5160..5885` to `32..177`, 6 GHz `5955..7115` to `1..233`) and `RssiDbmToApproxSignalPercent(int rssiDbm)` (`Math.Clamp(2 * (rssiDbm + 100), 0, 100)`) in `Checker.Infrastructure/Scanning/WiFiChannelConverter.cs`
- [X] T003 [P] Add `InterfaceName` (`string?`) to `ScannedWiFiNetwork` (`Bssid` required `string`, `Ssid` `string?`, `SignalStrengthPercent` `int?` clamped `0..100`, `RssiDbm` `int?`, `Band` `string?`, `FrequencyMhz` `int?`, `Channel` `int?`, `InterfaceName` `string?`) and add `WiFiScanBatch` (`IReadOnlyList<string> ScannedInterfaces`, `IReadOnlyList<ScannedWiFiNetwork> Networks`) in `Checker.Application/DTOs/ScannedDroneNetworksDto.cs`
- [X] T004 [P] Create `Checker.Application/DTOs/WlanInterfacesDto.cs` defining `WlanInterfaceInfo` (`Name` required `string` max 64 chars without leading `-` or control chars, `State` required normalized `Up | Down | Connected | Disconnected | Unavailable | Unknown`, `IsUsableForScan` required `bool`, `Description` `string?`, `MacAddress` `string?`), `WlanInterfaceDiscoveryResult` (`Available` required `bool`, `Reason` `string?`, `Interfaces` required `IReadOnlyList<WlanInterfaceInfo>`), `WlanInterfacesResponse` (`Available`, `Reason`, `Interfaces`), and `WlanInterfaceDto` (`Name`, `State`, `IsUsableForScan`, `Description`, `MacAddress`)
- [X] T005 Update `IWiFiScanner` (`Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default)` and `Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default)`) in `Checker.Application/Abstractions/IWiFiScanner.cs`, add `Task<WlanInterfacesResponse> GetWlanInterfacesAsync(CancellationToken ct = default)` in `Checker.Application/Abstractions/IDroneNetworkCheckerService.cs`, and update `UnavailableWiFiScanner` in `Checker.Application/Services/DroneNetworkCheckerService.cs` (depends on T003, T004)

**Checkpoint**: Foundation ready — user story phases (`US1`–`US4`) can now proceed cleanly.

---

## Phase 3: User Story 1 - Multi-WLAN Drone Scanning with Deduplication (Priority: P1) 🎯 MVP

**Goal**: Discover all usable WLAN interfaces across Linux and native Windows, scan across every usable interface, tolerate partial single-interface failures, deduplicate observations by canonical `MacAddress` prior to evaluation, filter out non-drone networks, and return consolidated station entries with `scannedInterfaces` and `interfaceNames`.

**Independent Test**: Run `WiFiScanParsersTests`, `DroneNetworkCheckerServiceTests`, and `NetworkScanEndpointTests` simulating multi-interface scans with duplicate BSSIDs (e.g., `wlan0` and `wlan1`), hidden vs. captured SSIDs, same-SSID distinct-BSSID pairs, non-drone stations, and single-interface scan failures.

### Tests for User Story 1

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US1 production code.**

- [X] T006 [P] [US1] Write unit tests for `IwWiFiScanParser` (`iw dev` interface parsing + `iw dev <iface> scan` BSS/SSID/freq/signal/channel parsing + `\x00` hidden SSID handling + malformed lines), multi-interface `NetshWiFiScanParser` (`Interface name :` tracking across multiple adapters), `NetshWlanInterfacesParser` (`Name`, `Description`, `Physical address`, `State`), and `NmcliWiFiScanParser` interface status + Wi-Fi scan row attribution in `Checker.Tests/Unit/WiFiScanParsersTests.cs`
- [X] T007 [P] [US1] Write unit tests for `ScanDroneNetworksAsync` in `Checker.Tests/Unit/DroneNetworkCheckerServiceTests.cs` verifying cross-interface canonical BSSID deduplication (max `SignalStrengthPercent`/`RssiDbm` wins, non-empty `Ssid` adopted from secondary observation, sorted distinct `InterfaceNames` accumulated), distinct BSSIDs sharing the same SSID stay separate, `TotalNetworksScanned` counts unique valid BSSIDs, and non-drone networks are excluded

### Implementation for User Story 1

- [X] T008 [P] [US1] Create `Checker.Infrastructure/Scanning/IwWiFiScanParser.cs` implementing `ParseInterfaces(string iwDevOutput, IReadOnlyDictionary<string, string>? operStates = null)` (extracting `Interface <name>`, `addr`, `type`, excluding non-netdev `p2p-dev-*`/monitor interfaces) and `ParseScanOutput(string output, string interfaceName)` (parsing `BSS <mac>(on <iface>)`, `SSID:`, `freq:`, `signal: <dBm>`, `DS Parameter set: channel` / `HT operation` primary channel, resolving missing channel/percent via `WiFiChannelConverter`)
- [X] T009 [P] [US1] Update `Checker.Infrastructure/Scanning/NetshWiFiScanParser.cs` to capture `Interface name : <name>` headers onto each `ScannedWiFiNetwork.InterfaceName` and add `NetshWlanInterfacesParser.Parse(string output)` extracting `Name`, `Description`, `Physical address` (canonical `MacAddress`), and `State` (`Connected`/`Disconnected` -> `IsUsableForScan`), handling `"There is no wireless interface"` / `"is not running"`
- [X] T010 [P] [US1] Update `Checker.Infrastructure/Scanning/NmcliWiFiScanParser.cs` to accept `string? interfaceName` on `Parse(string output, string? interfaceName = null)` and add `ParseInterfaces(string output)` parsing `nmcli -t -f DEVICE,TYPE,STATE,CONNECTION dev status` filtered strictly to `TYPE == "wifi"` (excluding Ethernet, loopback, and virtual bridges)
- [X] T011 [US1] Implement canonical-BSSID cross-interface deduplication and `WiFiScanBatch` aggregation in `Checker.Application/Services/DroneNetworkCheckerService.cs` (`ScanDroneNetworksAsync`): validate `MacAddress.TryParse` (skip invalid/all-zero/broadcast), group by `mac.Canonical`, count `TotalNetworksScanned` as unique valid BSSIDs, merge primary observation by max `SignalStrengthPercent ?? int.MinValue` -> `RssiDbm ?? int.MinValue` -> non-empty valid `Ssid` -> ordinal `InterfaceName`, backfill missing RF metadata (`Band`, `FrequencyMhz`, `Channel`) and valid `SsidName` from secondary observations, accumulate sorted distinct `InterfaceNames`, evaluate once via `EvaluateSingle`, filter by `IsDroneStation || (includePossible && Verdict == DetectionVerdict.Possible)`, and populate `ScannedInterfaces` (depends on T003, T005, T007)
- [X] T012 [US1] Refactor `Checker.Infrastructure/Scanning/PlatformWiFiScanner.cs` to enumerate all WLAN interfaces and scan every usable validated interface (`^[A-Za-z0-9._\-\s]{1,64}$`, disallowing leading `-` and control chars, passed via `ProcessStartInfo.ArgumentList` with `UseShellExecute = false`): on Linux enumerate `/sys/class/net/*/wireless` + `iw dev` (or `nmcli` when `/run/dbus/system_bus_socket` exists) and scan each interface via `iw dev <iface> scan` (falling back to `iw dev <iface> scan dump` on permission denied or `nmcli dev wifi list ifname <iface> --rescan yes` when D-Bus is present); on Windows enumerate `netsh wlan show interfaces` and scan each usable interface via `netsh wlan show networks interface=<name> mode=bssid`; tolerate individual interface failures if at least one other interface succeeds (depends on T002, T004, T005, T008, T009, T010)
- [X] T013 [US1] Update `StubWiFiScanner` and scan integration assertions in `Checker.Tests/Integration/NetworkScanEndpointTests.cs` to return `WiFiScanBatch` and verify deduplicated multi-interface `/networks/scan` responses (`scannedInterfaces`, `totalNetworksScanned`, `interfaceNames`) (depends on T011, T012)

**Checkpoint**: User Story 1 is functional and independently testable (multi-WLAN scanning & deduplication complete).

---

## Phase 4: User Story 2 - Safe Containerized Execution Across Windows Docker and Linux Docker (Priority: P1)

**Goal**: Install Linux wireless scanning dependencies (`iw`, `network-manager`, `libcap2-bin`) in `Dockerfile` under non-root `USER $APP_UID`, preserve safe default bridge networking in `compose.yaml`, and guarantee crash-free `available: false` / `503 Service Unavailable` fallback in Windows Docker and environments without wireless hardware.

**Independent Test**: Verify `PlatformWiFiScannerSafetyTests` (interface-name injection guards, empty `/sys/class/net` Windows Docker simulation, sanitized error messages), `NetworkScanEndpointTests` (`503` on scan when hardware unavailable while `/networks/check` and `/health` stay `200`), and `docker build --target runtime` verifying `iw`, `nmcli`, and non-root `id`.

### Tests for User Story 2

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US2 safety & container updates.**

- [X] T014 [P] [US2] Write unit tests in `Checker.Tests/Unit/PlatformWiFiScannerSafetyTests.cs` verifying strict interface name validation (rejecting leading `-`, shell metacharacters, control chars, length > 64), sanitized failure reasons (no OS paths or raw stderr), bounded timeout cleanup, and clean `WlanInterfaceDiscoveryResult` (`Available = false`, `Interfaces = []`) when no wireless interfaces exist
- [X] T015 [P] [US2] Add integration test in `Checker.Tests/Integration/NetworkScanEndpointTests.cs` verifying that when hardware is unavailable (Windows Docker simulation), `GET /networks/scan` returns `503 Service Unavailable` (`application/problem+json` `ProblemDetails`) while `GET /networks/check` and `GET /health` still return `200 OK`

### Implementation for User Story 2

- [X] T016 [US2] Harden `Checker.Infrastructure/Scanning/PlatformWiFiScanner.cs` for Windows Docker and timeout safety: return `WlanInterfaceDiscoveryResult(Available: false, Reason: "Live wireless scanning is unavailable because no accessible WLAN interfaces were detected in this environment.", Interfaces: [])` immediately when no `/sys/class/net/*/wireless` entries or D-Bus socket exist on Linux or when `netsh` reports no wireless interface / stopped `wlansvc` on Windows; throw sanitized `ApiException.ServiceUnavailable(...)` from `ScanAsync` when `Available == false` or all interfaces fail; enforce shared scan timeout budget with `TryKill` process cleanup (depends on T012, T014)
- [X] T017 [P] [US2] Update `Dockerfile` (`development` and `runtime` stages) to install `iw`, `network-manager`, and `libcap2-bin` via `apt-get update && apt-get install -y --no-install-recommends`, clean `/var/lib/apt/lists/*`, apply best-effort `setcap cap_net_admin,cap_net_raw+ep /usr/sbin/iw`, and retain non-root `USER $APP_UID`
- [X] T018 [P] [US2] Preserve safe default Docker bridge networking in `compose.yaml` (no `privileged: true` or `network_mode: host` breaking `http://checker:8080`) and add optional Linux host wireless Compose override `compose.linux-wifi.yaml` documenting NetworkManager D-Bus socket mount and Linux host-network/`NET_ADMIN` options per `specs/002-multi-wlan-scan/contracts/gateway-route.md`

**Checkpoint**: User Stories 1 and 2 operate safely across Windows Docker, Linux Docker, and native hosts.

---

## Phase 5: User Story 3 - Discover Available WLAN Interfaces (Priority: P2)

**Goal**: Expose authenticated `GET /networks/interfaces` returning only host 802.11 WLAN interfaces (`available: true`) when accessible, or `200 OK` with `available: false`, sanitized `reason`, and `interfaces: []` when wireless hardware is unavailable (such as Windows Docker).

**Independent Test**: Run `WlanInterfacesEndpointTests` and `DroneNetworkCheckerServiceTests` verifying `401` unauthenticated rejection, `400` on unexpected query parameters, `200` with `available: true` and WLAN-only adapters (`lo`/`eth0`/`docker0` excluded), and `200` with `available: false` and `interfaces: []`.

### Tests for User Story 3

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US3 service and endpoint.**

- [X] T019 [P] [US3] Write unit tests for `GetWlanInterfacesAsync` in `Checker.Tests/Unit/DroneNetworkCheckerServiceTests.cs` covering multi-adapter discovery (`Available = true` with `WlanInterfaceDto` items) and unavailable hardware (`Available = false`, non-empty `Reason`, empty `Interfaces`)
- [X] T020 [P] [US3] Write API integration tests in `Checker.Tests/Integration/WlanInterfacesEndpointTests.cs` covering `GET /networks/interfaces`: `401 Unauthorized` without credentials, `400 ValidationProblemDetails` when any unexpected query parameter is passed, `200 OK` with `available: true` and WLAN interfaces (`name`, `state`, `isUsableForScan`, `description`, `macAddress`), and `200 OK` with `available: false`, `reason`, and `interfaces: []` when hardware is unavailable

### Implementation for User Story 3

- [X] T021 [US3] Implement `GetWlanInterfacesAsync(CancellationToken ct = default)` in `Checker.Application/Services/DroneNetworkCheckerService.cs` projecting `WlanInterfaceDiscoveryResult` to `WlanInterfacesResponse` (`Available`, `Reason`, `Interfaces`) (depends on T004, T005, T019)
- [X] T022 [US3] Add `[HttpGet("interfaces")]` action (`[ProducesResponseType<WlanInterfacesResponse>(StatusCodes.Status200OK)]`) with empty `AllowedInterfacesQueryKeys` strict query validation (`RejectUnexpectedQueryKeys`) and XML doc comments in `Checker.API/Controllers/NetworkCheckController.cs` (depends on T020, T021)

**Checkpoint**: User Story 3 (`GET /networks/interfaces`) is independently testable and protected by full auth/validation/rate-limiting controls.

---

## Phase 6: User Story 4 - Streamlined Network Check and Scan Responses (Priority: P2)

**Goal**: Streamline `/networks/check` to 9 essential fields and `/networks/scan` drone entries to 15 essential fields (unifying `bssid`, `ssid`, `frequencyBands` naming across both endpoints), while isolating internal scoring diagnostics in `DroneNetworkEvaluation` so domain scoring tests remain comprehensive.

**Independent Test**: Run `VerdictScoringPolicyTests`, `DroneNetworkCheckerServiceTests`, `NetworkCheckEndpointTests`, and `NetworkScanEndpointTests` verifying exact JSON fields per `contracts/openapi.yaml` and asserting all removed verbose/internal keys are absent from serialized payloads.

### Tests for User Story 4

> **NOTE: Write these tests FIRST and verify they FAIL before implementing US4 DTO streamlining.**

- [X] T023 [P] [US4] Update integration tests in `Checker.Tests/Integration/NetworkCheckEndpointTests.cs` and `Checker.Tests/Integration/NetworkScanEndpointTests.cs` to assert exact streamlined JSON fields (`isDroneStation`, `verdict`, `confidenceScore`, `bssid`, optional `ssid`, optional `brandMark`, `candidateModels`, `frequencyBands`, `stationRole` on `/networks/check`; plus `signalStrengthPercent`, `rssiDbm`, `band`, `frequencyMhz`, `channel`, `interfaceNames` and top-level `scannedInterfaces` on `/networks/scan`) and explicitly verify removed keys (`ouiPrefix`, `matchedPrefix`, `isLocallyAdministered`, `isMulticast`, `matchedSsidPattern`, `hardwareVendor`, `chipsetExclusivity`, `matchSource`, `frequencyRangesMhz`, `channelSummary`, `radioProtocols`, `signalRangeTier`, `knownSsidPatterns`, `radioType`, `authentication`, `notes`, `supportedFrequencyBands`, `normalizedBssid`, `observedSsid`) are absent
- [X] T024 [P] [US4] Write/split internal scoring unit tests in `Checker.Tests/Unit/VerdictScoringPolicyTests.cs` validating `MatchSource`, `MatchedPrefix` (36/28/24-bit), `ChipsetExclusivity`, and `IsLocallyAdministered` directly against `VerdictScoringPolicy.EvaluateVerdict`

### Implementation for User Story 4

- [X] T025 [P] [US4] Create internal `Checker.Application/DTOs/DroneNetworkEvaluation.cs` holding complete internal evaluation fields (`IsDroneStation`, `Verdict`, `ConfidenceScore`, `MatchSource`, `NormalizedBssid`, `MatchedPrefix`, `IsLocallyAdministered`, `IsMulticast`, `ObservedSsid`, `MatchedSsidPattern`, `HardwareVendor`, `BrandMark`, `ChipsetExclusivity`, `StationRole`, `CandidateModels`, `FrequencyBands`, `Notes`) and update `Checker.Application/Services/VerdictScoringPolicy.cs` to return `DroneNetworkEvaluation`
- [X] T026 [P] [US4] Streamline `Checker.Application/DTOs/DroneNetworkCheckResponse.cs` to exact 9 fields (`IsDroneStation`, `Verdict`, `ConfidenceScore`, `Bssid`, `Ssid`, `BrandMark`, `CandidateModels`, `FrequencyBands`, `StationRole`) and streamline `ScannedDroneNetworksDto.cs` (`ScannedDroneNetworksResponse`: `ScannedAtUtc`, `ScannedInterfaces`, `TotalNetworksScanned`, `DroneCount`, `Drones`; `ScannedDroneNetworkDto` exact 15 fields: `Bssid`, `Ssid`, `IsDroneStation`, `Verdict`, `ConfidenceScore`, `BrandMark`, `CandidateModels`, `FrequencyBands`, `StationRole`, `SignalStrengthPercent`, `RssiDbm`, `Band`, `FrequencyMhz`, `Channel`, `InterfaceNames`)
- [X] T027 [US4] Update `Checker.Application/Services/DroneNetworkCheckerService.cs` to project `DroneNetworkEvaluation` onto streamlined `DroneNetworkCheckResponse` and `ScannedDroneNetworkDto`, and align `Checker.Tests/Unit/DroneNetworkCheckerServiceTests.cs` assertions with the streamlined DTO properties (depends on T023, T024, T025, T026)

**Checkpoint**: All 4 user stories are independently implemented, streamlined, and verified.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation alignment, formatting validation, full test verification, and end-to-end container checks.

- [X] T028 [P] Update Swagger/OpenAPI XML descriptions in `Checker.API/Controllers/NetworkCheckController.cs` and `Checker.API/Program.cs`, and verify `specs/002-multi-wlan-scan/contracts/openapi.yaml` matches generated Swagger schema
- [X] T029 [P] Update `GEMINI.md` and `README.md` in `src/services/Checker/` documenting `GET /networks/interfaces`, multi-WLAN scanning & canonical BSSID deduplication, streamlined `/networks/check` and `/networks/scan` schemas, `iw`/`nmcli` in `Dockerfile`, and safe Windows Docker vs Linux Docker deployment
- [X] T030 Run `dotnet format whitespace . --folder --verify-no-changes` and `dotnet test Checker.slnx` in `src/services/Checker/` to confirm clean formatting and zero regressions across all unit and integration tests
- [X] T031 Validate Docker runtime image build (`docker build --target runtime` verifying `iw`, `nmcli`, and non-root `$APP_UID`) and end-to-end scenarios per `specs/002-multi-wlan-scan/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately (`T001`).
- **Foundational (Phase 2)**: Depends on `T001` — BLOCKS all user stories (`T002`–`T005`).
- **User Story 1 (Phase 3 - P1 MVP)**: Depends on Phase 2 (`T002`–`T005`). Delivers multi-WLAN discovery/scanning and canonical BSSID deduplication.
- **User Story 2 (Phase 4 - P1 Container & Platform Safety)**: Depends on `T012` for runtime scanner hardening (`T016`), while `T014`, `T015`, `T017` (`Dockerfile`), and `T018` (`compose.yaml`) can proceed immediately after Phase 2.
- **User Story 3 (Phase 5 - P2 WLAN Interfaces Endpoint)**: Depends on Phase 2 (`T004`, `T005`) and `PlatformWiFiScanner` interface enumeration (`T012` / `T016`).
- **User Story 4 (Phase 6 - P2 Streamlined Responses)**: Depends on Phase 2 (`T003`) and US1 (`T011`); cleanly slims `DroneNetworkCheckResponse` (9 fields) and `ScannedDroneNetworkDto` (15 fields) after multi-interface fields are wired.
- **Polish (Phase 7)**: Depends on Phases 3–6 completion.

### Within Each User Story

- Tests (`T006`–`T007`, `T014`–`T015`, `T019`–`T020`, `T023`–`T024`) are written first and must fail before implementing production code.
- DTOs/parsers before service orchestration (`DroneNetworkCheckerService`, `PlatformWiFiScanner`); services before controller endpoints (`NetworkCheckController`).

### Parallel Opportunities

- **Phase 2**: `T002`, `T003`, and `T004` run in parallel before `T005`.
- **US1**: `T006` and `T007` (tests) run in parallel; `T008`, `T009`, and `T010` (OS parsers) run in parallel before `T011` and `T012`.
- **US2**: `T014`, `T015`, `T017`, and `T018` run in parallel while `T016` hardens `PlatformWiFiScanner`.
- **US3**: `T019` and `T020` run in parallel before `T021` and `T022`.
- **US4**: `T023`, `T024`, `T025`, and `T026` run in parallel before `T027`.
- **Polish**: `T028` and `T029` run in parallel before `T030` and `T031`.

---

## Parallel Example: User Story 1

```text
# 1. Write US1 tests in parallel:
T006 [P] [US1] Parser tests (IwWiFiScanParser, NetshWlanInterfacesParser, Nmcli) in Checker.Tests/Unit/WiFiScanParsersTests.cs
T007 [P] [US1] Multi-interface deduplication unit tests in Checker.Tests/Unit/DroneNetworkCheckerServiceTests.cs

# 2. Implement platform scan parsers in parallel:
T008 [P] [US1] IwWiFiScanParser in Checker.Infrastructure/Scanning/IwWiFiScanParser.cs
T009 [P] [US1] NetshWiFiScanParser & NetshWlanInterfacesParser in Checker.Infrastructure/Scanning/NetshWiFiScanParser.cs
T010 [P] [US1] NmcliWiFiScanParser interface & scan parsing in Checker.Infrastructure/Scanning/NmcliWiFiScanParser.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2 Safety Baseline)

1. Complete Phase 1 (Setup) + Phase 2 (Foundational).
2. Complete Phase 3 (**US1** — multi-WLAN interface scanning + canonical BSSID deduplication).
3. Complete Phase 4 (**US2** — `Dockerfile` Linux wireless tooling + Windows Docker safe `503`/`available: false` hardening).
4. **STOP & VALIDATE**: Run `dotnet test Checker.slnx` and verify multi-interface deduplication + Windows Docker safe fallback.

### Incremental Delivery

1. **MVP**: Setup + Foundational + US1 + US2 -> safe multi-WLAN scanning across Windows Docker and Linux Docker.
2. **Interface Discovery**: Add US3 -> `GET /networks/interfaces` endpoint with clean `available` status reporting.
3. **Payload Cleanup**: Add US4 + Polish -> streamlined 9-field `/networks/check` and 15-field `/networks/scan` payloads, updated docs, formatting verification, and Docker runtime validation.
