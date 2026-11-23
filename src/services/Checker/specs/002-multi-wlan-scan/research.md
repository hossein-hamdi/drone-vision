# Phase 0 Research: Multi-WLAN Drone Scanning & Streamlined Network Responses

**Feature**: `002-multi-wlan-scan` | **Date**: 2026-11-24

---

## 1. Multi-WLAN Interface Discovery & Scanning Strategy (Linux Docker, Native Linux, Windows Docker, Native Windows)

### Decision
Implement explicit multi-interface wireless adapter enumeration and per-interface scanning in `PlatformWiFiScanner` (`Checker.Infrastructure/Scanning/`), backed by a dedicated interface discovery model (`WlanInterfaceDiscoveryResult` / `WlanInterfaceInfo`) and platform-native adapters:

1. **Linux (Linux Docker & Native Linux)**:
   - **Interface Enumeration**: Inspect Linux kernel sysfs `/sys/class/net/<iface>/wireless` (and `phy80211`) combined with `iw dev` output to enumerate true 802.11 WLAN interfaces (`wlan0`, `wlp3s0`, `wlx...`) while excluding non-wireless interfaces (`lo`, `eth0`, `docker0`, `br-*`, `veth*`, `tun*`), reading operational state (`/sys/class/net/<iface>/operstate`) and hardware MAC address (`/sys/class/net/<iface>/address`). When `/sys/class/net` shows no wireless interfaces (e.g., Windows Docker Desktop WSL2 VM) or `iw`/`nmcli` cannot access wireless hardware, fall back to checking `nmcli -t -f DEVICE,TYPE,STATE dev status` if `/run/dbus/system_bus_socket` is present; if still none, report `available: false` safely.
   - **Multi-Interface Scanning**:
     - Primary Linux path (daemonless, works directly with kernel `nl80211`): execute `iw dev <iface> scan` across every discovered usable WLAN interface, with read-only `iw dev <iface> scan dump` fallback if active scan initiation returns `-EPERM`/`Operation not permitted` and cached BSS entries exist. Parse `BSS <mac>(on <iface>)`, `SSID:`, `freq:`, `signal: <dBm>`, and channel IEs via a new `IwWiFiScanParser`.
     - Secondary/D-Bus Linux path (works over bridge network when host NetworkManager D-Bus socket `/run/dbus/system_bus_socket` is bind-mounted): if `iw` fails or is unavailable and `nmcli` + D-Bus socket are accessible, execute `nmcli -t -f SSID,BSSID,SIGNAL,CHAN,FREQ,SECURITY dev wifi list ifname <iface> --rescan yes` per WLAN interface via `NmcliWiFiScanParser`.
   - **Container Dependencies (`Dockerfile`)**: Install `iw`, `network-manager`, and `libcap2-bin` (`--no-install-recommends`) in both `development` and `runtime` stages, applying `setcap cap_net_admin,cap_net_raw+ep` on `/usr/sbin/iw` (best-effort) while keeping runtime execution strictly non-root (`USER $APP_UID`).

2. **Native Windows (Outside Docker)**:
   - **Interface Enumeration**: Execute `netsh.exe wlan show interfaces` and parse each interface block (`Name`, `Description`, `Physical address`, `State`) via a dedicated `NetshWlanInterfacesParser`. When `wlansvc` is stopped or `"There is no wireless interface on the system"`, safely report `available: false` (on `/networks/interfaces`) and `503 Service Unavailable` (on `/networks/scan`).
   - **Multi-Interface Scanning**: Iterate all discovered usable WLAN interfaces (or run `netsh wlan show networks interface="<name>" mode=bssid` per interface via `ArgumentList` without shell interpretation), updating `NetshWiFiScanParser` to attach the parsed `Interface name : <name>` to every `ScannedWiFiNetwork`.

3. **Windows Docker (Docker Desktop on Windows / WSL2)**:
   - Containers running under Docker Desktop on Windows use a Linux kernel in a VM without host Wi-Fi passthrough (`eth0`/`lo` only; zero `/sys/class/net/*/wireless` entries and no D-Bus NetworkManager socket).
   - `PlatformWiFiScanner.DiscoverInterfacesAsync` detects zero accessible WLAN interfaces cleanly without throwing unhandled exceptions and returns `WlanInterfaceDiscoveryResult(Available: false, Reason: "Live wireless scanning is unavailable because no accessible WLAN interfaces were detected in this environment.", Interfaces: [])`.
   - `GET /networks/interfaces` returns `200 OK` with that payload (`available: false`, `interfaces: []`).
   - `GET /networks/scan` throws `ApiException.ServiceUnavailable(...)`, returning sanitized RFC 9457 `503 Service Unavailable` (`application/problem+json`).
   - Non-scan capabilities (`/networks/check`, `/health`) continue functioning normally with 100% availability.

### Rationale
- Relying exclusively on `nmcli` fails inside standard Linux Docker containers unless `/run/dbus/system_bus_socket` is mounted from a NetworkManager-managed host. Installing both `iw` (kernel `nl80211`, daemonless) and `network-manager` (`nmcli`, D-Bus compatible) supports both Linux container topologies seamlessly:
  - Host-networked Linux container with `cap_add: [NET_ADMIN]` (`iw` + `setcap`, no host NetworkManager required).
  - Bridge-networked Linux container with `/run/dbus/system_bus_socket` mounted (`nmcli`, preserving `gateway -> http://checker:8080` service discovery!).
- Checking `/sys/class/net/*/wireless` before spawning scan processes makes Windows Docker unavailability detection sub-millisecond, deterministic, and completely crash-free.

### Alternatives Considered
- **`nmcli`-only inside Docker**: Breaks on Linux hosts using `systemd-networkd`/`wpa_supplicant`/`iwd` or containers without host D-Bus socket mounting; also fails in `Dockerfile` currently because `network-manager` was not yet installed in `Dockerfile`.
- **`iwlist` (`wireless-tools`)**: Deprecated legacy WEXT API with inconsistent output formatting and weak 5 GHz/6 GHz support compared to modern `nl80211` `iw`.
- **Disabling Windows native scanning completely**: Rejected per user clarification (A3); keeping native `netsh` multi-interface scanning preserves local Windows developer testing (`pnpm dev`) while Windows Docker containers automatically return `503` / `available: false`.

---

## 2. Cross-Interface Deduplication & Partial-Failure Tolerance

### Decision
Perform canonical BSSID-based deduplication across all scanned WLAN interfaces inside `DroneNetworkCheckerService.ScanDroneNetworksAsync` **prior** to drone verdict evaluation, using `WiFiObservationAggregator` (or clean helper inside `Checker.Application/Services/`):

1. **Pre-evaluation normalization & filtering**: Parse `item.Bssid` via `MacAddress.TryParse`; skip malformed/all-zero/broadcast addresses without crashing.
2. **Deduplication key**: `mac.Canonical` (`AA:BB:CC:DD:EE:FF`). Two distinct BSSIDs sharing the same SSID remain separate entries; the same BSSID observed on `wlan0` and `wlan1` collapses into one entry.
3. **Merge priority**:
   - **Primary RF observation**: Pick the observation with higher `SignalStrengthPercent` (breaking ties by higher `RssiDbm`, then lexicographically earlier `InterfaceName` for deterministic ordering).
   - **SSID**: Retain the primary observation's valid non-empty SSID if present, otherwise adopt a non-empty valid SSID from another interface's observation of the same BSSID (critical if one adapter missed a beacon probe response while the other captured the SSID).
   - **RF fields (`Band`, `FrequencyMhz`, `Channel`)**: Prefer the primary observation's non-null values and backfill any nulls from secondary observations.
   - **Interface attribution (`InterfaceNames`)**: Collect all distinct non-empty source interface names in sorted `StringComparer.OrdinalIgnoreCase` order.
4. **Partial interface failure resilience**: If a host has multiple WLAN interfaces and one interface is down/busy/unpermitted while at least one other interface succeeds (returning >= 0 stations cleanly), log a warning for the failed interface and return the deduplicated detections from the succeeding interfaces. Throw `503 Service Unavailable` only when zero usable interfaces exist or every usable interface fails to scan.
5. **Bounded execution time**: Enforce an overall scan cancellation budget (e.g., 10–12 seconds across multi-interface discovery + scans, with bounded per-command timeout and `TryKill` process cleanup) so slow interfaces cannot hang HTTP requests indefinitely.

### Rationale
- Merging observations *before* `EvaluateSingle` ensures that if `wlan0` sees a hidden beacon for a shared-chipset drone and `wlan1` captures its SSID (`SYMA_FPV_1234`), the evaluator correlates BSSID + SSID into `Confirmed` instead of losing the match or emitting two conflicting verdicts.
- Counting `totalNetworksScanned` over deduplicated unique BSSIDs avoids inflating network counts by `N` interfaces.

### Alternatives Considered
- **Deduplicating after evaluation**: Risks evaluating a duplicate BSSID twice with inconsistent SSIDs (e.g., `Possible` on one interface and `Confirmed` on another).
- **Deduplicating by `(Bssid, Ssid)`**: Would produce duplicate entries for the exact same AP when one adapter sees a blank/hidden SSID and the other captures the SSID.

---

## 3. Streamlined API Response Contracts & Internal Evaluation Separation

### Decision
Separate internal evaluation richness from the streamlined external API DTOs:
- Introduce an internal `DroneNetworkEvaluation` record inside `Checker.Application` returned by `VerdictScoringPolicy.EvaluateVerdict(...)` containing full diagnostic properties (`MatchSource`, `MatchedPrefix`, `ChipsetExclusivity`, `IsLocallyAdministered`, etc.) so all existing domain scoring invariants and unit tests continue verifying internal matching behavior rigorously.
- Project `DroneNetworkEvaluation` to the streamlined public response contracts confirmed in user clarification A1:
  - **`DroneNetworkCheckResponse` (9 fields, down from 23 — 61% reduction)**:
    `isDroneStation`, `verdict`, `confidenceScore`, `bssid`, `ssid`, `brandMark`, `candidateModels`, `frequencyBands`, `stationRole`.
  - **`ScannedDroneNetworkDto` (15 fields, down from 26 — 42% reduction)**:
    `bssid`, `ssid`, `isDroneStation`, `verdict`, `confidenceScore`, `brandMark`, `candidateModels`, `frequencyBands`, `stationRole`, `signalStrengthPercent`, `rssiDbm`, `band`, `frequencyMhz`, `channel`, `interfaceNames`.
  - **`ScannedDroneNetworksResponse`**:
    `scannedAtUtc`, `scannedInterfaces`, `totalNetworksScanned`, `droneCount`, `drones`.
  - **`WlanInterfacesResponse` (NEW)**:
    `available`, `reason`, `interfaces` (list of `WlanInterfaceDto`: `name`, `state`, `isUsableForScan`, `description`, `macAddress`).

### Rationale
- Preserves 100% of internal OUI/MA-M/MA-S, LAA, shared-chipset, and ReDoS-safe SSID scoring accuracy while eliminating 14 unused/verbose fields from `/networks/check` and 12 from `/networks/scan` (`ouiPrefix`, `matchedPrefix`, `isLocallyAdministered`, `isMulticast`, `matchedSsidPattern`, `hardwareVendor`, `chipsetExclusivity`, `matchSource`, `frequencyRangesMhz`, `channelSummary`, `radioProtocols`, `signalRangeTier`, `knownSsidPatterns`, `authentication`, `radioType`, `notes`), and aligns naming (`bssid`, `ssid`, `frequencyBands`) uniformly across both endpoints.

### Alternatives Considered
- **Stripping fields out of `VerdictScoringPolicy` itself**: Would weaken testability of internal LAA, longest-prefix, and shared-chipset scoring rules.

---

## 4. Security & Input Validation Hygiene

### Decision
- **Interface name sanitization**: Validate every discovered interface name before passing it to any subprocess `ArgumentList` using a strict allowlist regex/validator (`^[A-Za-z0-9._\-\s]{1,64}$`, disallowing leading `-` and control characters) to eliminate flag/argument injection even from crafted virtual interface names.
- **No shell invocation**: Continue using `ProcessStartInfo` with `UseShellExecute = false` and `ArgumentList` exclusively.
- **Strict query allowlisting**: Add `AllowedInterfacesQueryKeys` (empty set) to `NetworkCheckController.Interfaces()` so any unexpected query parameter on `GET /networks/interfaces?...` is rejected with `400 ValidationProblemDetails`, matching `/networks/check` and `/networks/scan`.
- **Gateway & Auth parity**: `GET /networks/interfaces` sits under `[Route("networks")]` on `NetworkCheckController` (inheriting `ApiController`), so dual `"BrowserOrBearer"` auth, IP fixed-window rate limiting, security headers (`nosniff`, `no-store`, `DENY`, `no-referrer`), and Gateway `/api/checker/networks/interfaces` routing apply automatically with zero changes to `src/gateway/ocelot.checker.json`.
