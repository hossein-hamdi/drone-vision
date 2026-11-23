# Data Model: Multi-WLAN Drone Scanning & Streamlined Network Responses

**Feature**: `002-multi-wlan-scan` | **Date**: 2026-11-24

---

## 1. Domain & Internal Evaluation Models

### Existing Domain Value Objects & Enums (`Checker.Domain`)
Unchanged in behavior:
- `MacAddress` (`IParsable<MacAddress>`): Normalizes BSSIDs to canonical `AA:BB:CC:DD:EE:FF`, extracts 24/28/36-bit prefixes, detects LAA and multicast bits, rejects empty/all-zero/broadcast addresses.
- `SsidName`: Enforces `1..32` UTF-8 bytes and rejects ASCII control characters.
- `DetectionVerdict`: `NotRecognized`, `Possible`, `Probable`, `Confirmed`
- `StationRole`: `Unknown`, `AircraftAp`, `GroundController`, `FpvVtx`, `TelemetryModule`, `MultiRole`
- `MatchSource`, `ChipsetExclusivity`, `FrequencyBand`, `SignalRangeTier`: Retained internally for `VerdictScoringPolicy` and catalog integrity.

### Internal Evaluation Result (`Checker.Application.Services` / `DTOs`)
- **`DroneNetworkEvaluation`** (internal/service evaluation model produced by `VerdictScoringPolicy.EvaluateVerdict`):
  Retains complete evidence metadata (`IsDroneStation`, `Verdict`, `ConfidenceScore`, `MatchSource`, `NormalizedBssid`, `MatchedPrefix`, `IsLocallyAdministered`, `IsMulticast`, `ObservedSsid`, `MatchedSsidPattern`, `HardwareVendor`, `BrandMark`, `ChipsetExclusivity`, `StationRole`, `CandidateModels`, `FrequencyBands`, `Notes`) for internal evaluation tests and diagnostics, then projects cleanly onto the streamlined public response DTOs below.

---

## 2. Host Wireless Discovery & Scanning Entities (`Checker.Application.DTOs`)

### `WlanInterfaceInfo` (internal & DTO projection)
Represents a wireless (WLAN) network interface discovered on the host. Non-wireless interfaces (`lo`, Ethernet, Docker bridges, veth, VPN tunnels) are excluded.

| Field | Type | Required | Constraints / Notes |
| --- | --- | --- | --- |
| `Name` | `string` | Yes | Validated OS interface identifier (e.g., `wlan0`, `wlp3s0`, `Wi-Fi`), max 64 chars, no control chars or leading `-` |
| `State` | `string` | Yes | Normalized operational state (`Up`, `Down`, `Connected`, `Disconnected`, `Unavailable`, `Unknown`) |
| `IsUsableForScan` | `bool` | Yes | `true` when the interface is active/up/scan-capable; `false` when administratively down, RF-blocked, or unmanaged |
| `Description` | `string?` | No | Optional driver/adapter description (e.g., `Intel(R) Wi-Fi 6 AX201` on Windows or driver/wiphy note on Linux) |
| `MacAddress` | `string?` | No | Optional canonical adapter MAC address (`AA:BB:CC:DD:EE:FF`) when reported by OS |

### `WlanInterfaceDiscoveryResult`
Encapsulates the result of enumerating host WLAN capabilities without throwing when hardware is unavailable (e.g., Windows Docker).

| Field | Type | Required | Constraints / Notes |
| --- | --- | --- | --- |
| `Available` | `bool` | Yes | `true` when wireless scanning capability and at least one usable/discoverable WLAN subsystem are available; `false` in Windows Docker or hosts with zero accessible wireless hardware |
| `Reason` | `string?` | No | Sanitized human-readable explanation when `Available` is `false` (never exposes internal paths or stack traces) |
| `Interfaces` | `IReadOnlyList<WlanInterfaceInfo>` | Yes | Discovered WLAN interfaces (`[]` when `Available` is `false`) |

### `ScannedWiFiNetwork` (updated)
Represents a single raw wireless observation from a specific WLAN interface prior to deduplication.

| Field | Type | Required | Constraints / Notes |
| --- | --- | --- | --- |
| `Bssid` | `string` | Yes | Raw BSSID string parsed/normalized via `MacAddress.TryParse` |
| `Ssid` | `string?` | No | Observed SSID (`null` for hidden/empty SSIDs) |
| `SignalStrengthPercent` | `int?` | No | Clamped `0..100` (derived from RSSI on `iw` via `Math.Clamp(2 * (rssi + 100), 0, 100)` when omitted) |
| `RssiDbm` | `int?` | No | Measured or approximated RSSI in dBm (e.g., `-56`) |
| `Band` | `string?` | No | Normalized band label (`2.4 GHz`, `5 GHz`, `5.8 GHz`, `6 GHz`) |
| `FrequencyMhz` | `int?` | No | Center frequency in MHz (e.g., `2437`, `5745`) |
| `Channel` | `int?` | No | Wi-Fi channel number (derived from `FrequencyMhz` if omitted by OS tool) |
| `InterfaceName` | `string?` | No | Source WLAN interface name that captured this observation (e.g., `wlan0`, `Wi-Fi`) |

### `WiFiScanBatch` (returned by `IWiFiScanner.ScanAsync`)
Captures both raw observations and the list of interfaces scanned so `ScannedDroneNetworksResponse.ScannedInterfaces` reflects reality even when 0 drones are found.

| Field | Type | Required | Constraints / Notes |
| --- | --- | --- | --- |
| `ScannedInterfaces` | `IReadOnlyList<string>` | Yes | Ordered distinct names of WLAN interfaces successfully scanned |
| `Networks` | `IReadOnlyList<ScannedWiFiNetwork>` | Yes | Combined raw observations across all scanned interfaces |

---

## 3. Cross-Interface Deduplication Rules

When aggregating `WiFiScanBatch.Networks` across interfaces:
1. Validate `Bssid` with `MacAddress.TryParse`; discard unparseable/all-zero/broadcast rows.
2. Group valid rows by `mac.Canonical` (`AA:BB:CC:DD:EE:FF`).
3. Count `TotalNetworksScanned` as the number of **unique canonical BSSIDs** across all scanned interfaces.
4. For each unique BSSID group:
   - **Primary observation**: Choose the row with maximum `SignalStrengthPercent ?? int.MinValue`, breaking ties by `RssiDbm ?? int.MinValue`, then non-empty valid `Ssid`, then ordinal `InterfaceName`.
   - **Merged SSID**: Use primary row's valid `SsidName`; if null/empty, adopt the first valid `SsidName` from other observations of the same BSSID.
   - **Merged RF metadata**: Use primary row's `SignalStrengthPercent`, `RssiDbm`, `Band`, `FrequencyMhz`, `Channel`, backfilling any `null` field from secondary observations.
   - **Merged `InterfaceNames`**: Distinct non-empty `InterfaceName` values from all observations in the group, sorted ascending (`StringComparer.OrdinalIgnoreCase`).
5. Evaluate each consolidated observation once through `EvaluateSingle(mac, ssid)` and keep entries where `IsDroneStation || (includePossible && Verdict == DetectionVerdict.Possible)`.
6. Sort final drones by `ConfidenceScore` descending, then `SignalStrengthPercent ?? int.MinValue` descending, then `Bssid` ascending.

---

## 4. Streamlined Public API Response Contracts (`Checker.Application.DTOs`)

JSON serialization continues using `camelCase`, `JsonStringEnumConverter`, and `JsonIgnoreCondition.WhenWritingNull`.

### A. `GET /networks/check` -> `DroneNetworkCheckResponse` (9 fields)

| JSON Property | C# Type | Required | Notes |
| --- | --- | --- | --- |
| `isDroneStation` | `bool` | Yes | `true` for `Confirmed` or `Probable`; `false` for `Possible` or `NotRecognized` |
| `verdict` | `DetectionVerdict` | Yes | `"NotRecognized" \| "Possible" \| "Probable" \| "Confirmed"` |
| `confidenceScore` | `int` | Yes | `0..100` |
| `bssid` | `string` | Yes | Canonical uppercase colon-separated MAC (`AA:BB:CC:DD:EE:FF`) |
| `ssid` | `string?` | No | Observed SSID (omitted when null) |
| `brandMark` | `string?` | No | Brand or chipset mark (omitted when null) |
| `candidateModels` | `IReadOnlyList<string>` | Yes | Matching drone model families (`[]` when unrecognized) |
| `frequencyBands` | `IReadOnlyList<string>` | Yes | Operating bands (`[]` when unrecognized) |
| `stationRole` | `StationRole` | Yes | `"Unknown" \| "AircraftAp" \| "GroundController" \| "FpvVtx" \| "TelemetryModule" \| "MultiRole"` |

*Removed from `DroneNetworkCheckResponse`*: `matchSource`, `ouiPrefix`, `matchedPrefix`, `isLocallyAdministered`, `isMulticast`, `matchedSsidPattern`, `hardwareVendor`, `chipsetExclusivity`, `frequencyRangesMhz`, `channelSummary`, `radioProtocols`, `signalRangeTier`, `knownSsidPatterns`, `notes`. (And `normalizedBssid`/`observedSsid` streamlined to `bssid`/`ssid`.)

### B. `GET /networks/scan` -> `ScannedDroneNetworksResponse` & `ScannedDroneNetworkDto` (15 fields per drone)

**`ScannedDroneNetworksResponse`**:

| JSON Property | C# Type | Required | Notes |
| --- | --- | --- | --- |
| `scannedAtUtc` | `DateTimeOffset` | Yes | UTC scan timestamp |
| `scannedInterfaces` | `IReadOnlyList<string>` | Yes | WLAN interfaces scanned (e.g., `["wlan0", "wlan1"]`) |
| `totalNetworksScanned` | `int` | Yes | Count of unique valid BSSIDs observed across all scanned interfaces |
| `droneCount` | `int` | Yes | Count of returned drone entries |
| `drones` | `IReadOnlyList<ScannedDroneNetworkDto>` | Yes | Filtered, deduplicated drone entries |

**`ScannedDroneNetworkDto`** (15 fields):

| JSON Property | C# Type | Required | Notes |
| --- | --- | --- | --- |
| `bssid` | `string` | Yes | Canonical uppercase colon-separated MAC |
| `ssid` | `string?` | No | Observed SSID (omitted when null) |
| `isDroneStation` | `bool` | Yes | `true` for confirmed/probable drones |
| `verdict` | `DetectionVerdict` | Yes | `"NotRecognized" \| "Possible" \| "Probable" \| "Confirmed"` |
| `confidenceScore` | `int` | Yes | `0..100` |
| `brandMark` | `string?` | No | Brand or chipset mark (omitted when null) |
| `candidateModels` | `IReadOnlyList<string>` | Yes | Candidate drone model families |
| `frequencyBands` | `IReadOnlyList<string>` | Yes | Known operating bands (unified name replacing `supportedFrequencyBands`) |
| `stationRole` | `StationRole` | Yes | Station role enum |
| `signalStrengthPercent` | `int?` | No | Best observed signal `0..100` (omitted when null) |
| `rssiDbm` | `int?` | No | Best observed RSSI in dBm (omitted when null) |
| `band` | `string?` | No | Observed live operating band (omitted when null) |
| `frequencyMhz` | `int?` | No | Observed center frequency in MHz (omitted when null) |
| `channel` | `int?` | No | Observed Wi-Fi channel (omitted when null) |
| `interfaceNames` | `IReadOnlyList<string>` | Yes | Deduplicated WLAN interface(s) that observed this station |

*Removed from `ScannedDroneNetworkDto`*: `radioType`, `authentication`, `matchSource`, `hardwareVendor`, `chipsetExclusivity`, `frequencyRangesMhz`, `radioProtocols`, `signalRangeTier`, `matchedPrefix`, `matchedSsidPattern`, `isLocallyAdministered`, `notes` (`supportedFrequencyBands` unified to `frequencyBands`).

### C. `GET /networks/interfaces` -> `WlanInterfacesResponse` & `WlanInterfaceDto` (NEW)

**`WlanInterfacesResponse`**:

| JSON Property | C# Type | Required | Notes |
| --- | --- | --- | --- |
| `available` | `bool` | Yes | `true` when WLAN scanning capability/adapters are available; `false` in Windows Docker or hardware-unavailable environments |
| `reason` | `string?` | No | Sanitized explanation when `available` is `false` (omitted when null) |
| `interfaces` | `IReadOnlyList<WlanInterfaceDto>` | Yes | Discovered WLAN interfaces (`[]` when `available` is `false`) |

**`WlanInterfaceDto`**:

| JSON Property | C# Type | Required | Notes |
| --- | --- | --- | --- |
| `name` | `string` | Yes | Interface name (e.g., `wlan0`, `Wi-Fi`) |
| `state` | `string` | Yes | Operational state (e.g., `Up`, `Down`, `Connected`, `Disconnected`) |
| `isUsableForScan` | `bool` | Yes | Whether the adapter can be used in live scans |
| `description` | `string?` | No | Adapter/driver label (omitted when null) |
| `macAddress` | `string?` | No | Canonical MAC of the WLAN adapter (omitted when null) |
