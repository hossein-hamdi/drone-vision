# Data Model: Drone Network Checker Service

**Feature**: `001-drone-network-checker` | **Date**: 2026-11-22

---

## 1. Domain Value Objects (`Checker.Domain.ValueObjects`)

### `MacAddress` (`IParsable<MacAddress>`)
Represents a validated, normalized IEEE 802 48-bit MAC / BSSID address.

| Property / Member | Type | Derivation / Validation Rules |
| --- | --- | --- |
| `RawBytes` | `byte[]` (6 bytes / `ulong` 48-bit representation) | Parsed from `XX:XX:XX:XX:XX:XX`, `XX-XX-XX-XX-XX-XX`, `XXXX.XXXX.XXXX`, or `XXXXXXXXXXXX` (case-insensitive, trimmed). Must contain exactly 12 hex digits. Rejects empty, `00:00:00:00:00:00` (all-zeros), and `FF:FF:FF:FF:FF:FF` (broadcast). |
| `Canonical` | `string` | Uppercase colon-separated format `AA:BB:CC:DD:EE:FF`. |
| `CompactHex` | `string` | Uppercase 12-character hex `AABBCCDDEEFF` (used for fast prefix slicing). |
| `Oui24` | `string` | First 3 octets (`AA:BB:CC`). |
| `Prefix28` | `string` | First 7 hex chars (`AA:BB:CC:D`, normalized). |
| `Prefix36` | `string` | First 9 hex chars (`AA:BB:CC:DD:E`, normalized). |
| `IsLocallyAdministered` | `bool` | `(firstByte & 0x02) != 0` — flags randomized/locally administered MACs. |
| `IsMulticast` | `bool` | `(firstByte & 0x01) != 0` — recorded for metadata/diagnostic transparency (standard Wi-Fi BSSIDs are unicast; multicast BSSIDs are anomalous/flagged or rejected if group bit invalid — note: reject broadcast `FF:FF:FF:FF:FF:FF` and all-zero `00:00:00:00:00:00`; surface multicast flag if bit 0 is set on a non-broadcast MAC, or reject multicast BSSIDs? Wait: Wi-Fi AP BSSIDs must be unicast; flagging `IsMulticast` or rejecting group-addressed BSSIDs — let's document rejecting all-zero/broadcast and flagging `IsLocallyAdministered` and `IsMulticast` cleanly). |

### `SsidName`
Represents an optional validated IEEE 802.11 SSID string.

| Property / Member | Type | Validation Rules |
| --- | --- | --- |
| `Value` | `string` | Optional (`null` if omitted or whitespace-only when optional? Treat empty/whitespace string as validation error if explicitly supplied empty, or `null` if omitted). Length in UTF-8 bytes must be `1..32` (`Encoding.UTF8.GetByteCount <= 32`). Must contain no ASCII control characters (`ch < 0x20 || ch == 0x7F`). |

---

## 2. Domain Enums (`Checker.Domain.Enums`)

- **`DetectionVerdict`**: `NotRecognized`, `Possible`, `Probable`, `Confirmed`
- **`MatchSource`**: `None`, `Oui`, `Ssid`, `OuiAndSsid`
- **`ChipsetExclusivity`**: `DedicatedDroneVendor`, `SharedConsumerChipset`
- **`StationRole`**: `Unknown`, `AircraftAp`, `GroundController`, `FpvVtx`, `TelemetryModule`, `MultiRole`
- **`FrequencyBand`**: `Band433MHz`, `Band900MHz`, `Band24GHz`, `Band5GHz`, `Band58GHz`
- **`SignalRangeTier`**: `Unknown`, `ShortRangeWiFi` (~50–300 m), `MediumRangeWiFi` (~300 m–2 km), `ExtendedRangeDigital` (2+ km, e.g., OcuSync/Lightbridge/SkyLink)

---

## 3. Signature Catalog Entities (`Checker.Domain.Entities` & `drone-signatures.json`)

### `DroneSignatureEntry`
Represents an IEEE MAC prefix mapping in the curated catalog.

| Field | Type | Required | Constraints / Notes |
| --- | --- | --- | --- |
| `Id` | `string` | Yes | Unique slug (e.g., `dji-mavic-phantom`, `autel-mam`, `espressif-shared`) |
| `Prefixes` | `IReadOnlyList<string>` | Yes | Normalized uppercase hex prefixes of length 6 (24-bit MA-L), 7 (28-bit MA-M), or 9 (36-bit MA-S), e.g., `60601F`, `EC5BCDE` |
| `VendorName` | `string` | Yes | Official IEEE registrant name (e.g., `SZ DJI Technology Co., Ltd.`) |
| `BrandMark` | `string` | Yes | Drone mark/brand or chipset name (e.g., `DJI`, `Parrot`, `Autel Robotics`, `Espressif (Shared Chipset)`) |
| `Exclusivity` | `ChipsetExclusivity` | Yes | `DedicatedDroneVendor` or `SharedConsumerChipset` |
| `CandidateModels` | `IReadOnlyList<string>` | Yes | Known drone models/families or typical drone ecosystems using the chipset |
| `StationRole` | `StationRole` | Yes | Aircraft AP, Ground Controller, MultiRole, TelemetryModule, etc. |
| `FrequencyProfile` | `FrequencyProfile` | Yes | Operating bands, frequency ranges (MHz), and channel notes |
| `RadioProtocols` | `IReadOnlyList<string>` | Yes | e.g., `["Wi-Fi 802.11a/b/g/n/ac", "OcuSync", "Lightbridge"]` |
| `SignalRangeTier` | `SignalRangeTier` | Yes | Documented transmission range/power tier |
| `AssociatedSsidPatterns` | `IReadOnlyList<string>` | Yes | Human-readable example/typical SSID patterns (e.g., `DJI-*`, `Mavic-*`) |
| `Notes` | `string?` | No | Context (e.g., shared chipset caveat or controller vs aircraft notes) |

### `FrequencyProfile`
| Field | Type | Notes |
| --- | --- | --- |
| `Bands` | `IReadOnlyList<FrequencyBand>` | Display labels e.g. `2.4 GHz`, `5 GHz`, `5.8 GHz` |
| `FrequencyRangesMhz` | `IReadOnlyList<string>` | e.g., `["2400-2483.5 MHz", "5725-5850 MHz"]` |
| `ChannelSummary` | `string` | e.g., `"2.4 GHz ISM (Ch 1–13) / 5.8 GHz ISM (Ch 149–165)"` |

### `SsidPatternRule`
Represents a pattern rule linking an SSID to a drone ecosystem.

| Field | Type | Required | Constraints / Notes |
| --- | --- | --- | --- |
| `Id` | `string` | Yes | Unique rule identifier (e.g., `syma-fpv`, `mjx-bugs`, `ryze-tello`) |
| `BrandMark` | `string` | Yes | Brand mark (e.g., `Syma`, `MJX`, `Ryze / DJI`, `Holy Stone`, `Parrot`) |
| `Pattern` | `string` | Yes | Bounded regex pattern compiled with `RegexOptions.NonBacktracking` and 50 ms timeout |
| `DisplayPattern` | `string` | Yes | Human-readable pattern label (e.g., `SYMA_FPV_*`, `Bugs_*`) |
| `CandidateModels` | `IReadOnlyList<string>` | Yes | Associated drone models/series |
| `StationRole` | `StationRole` | Yes | Typical station role |
| `FrequencyProfile` | `FrequencyProfile` | Yes | Documented operating frequency bands/ranges |
| `RadioProtocols` | `IReadOnlyList<string>` | Yes | e.g., `["Wi-Fi 802.11b/g/n"]` |
| `SignalRangeTier` | `SignalRangeTier` | Yes | Expected transmission range tier |
| `ConfidenceWeight` | `int` | Yes | `1..100` (e.g., `90` for unambiguous brand SSID like `TELLO-*`/`ANAFI-*`, `75` for multi-brand `WIFI_UFO_*`) |
| `AssociatedChipsetVendors` | `IReadOnlyList<string>` | No | Optional known chipset makers (e.g., `Espressif`, `Shenzhen Bilian`) |
| `Notes` | `string?` | No | Disambiguation details |

---

## 4. Verdict & Confidence Scoring Matrix (`VerdictScoringPolicy`)

| MAC Prefix Match | SSID Pattern Match | Locally Administered (LAA) | `IsDroneStation` | `Verdict` | `MatchSource` | Confidence Score | Behavior Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Dedicated Drone Vendor | Matching Same Brand | `false` | `true` | `Confirmed` | `OuiAndSsid` | `95–100` | Highest certainty; narrows models/bands to SSID match when more specific |
| Dedicated Drone Vendor | Not Provided (`null`) | `false` | `true` | `Confirmed` | `Oui` | `85–92` | Confirmed manufacturer; returns all candidate series & bands for that OUI |
| Dedicated Drone Vendor | Mismatched / Unrecognized SSID | `false` | `true` | `Probable` | `Oui` | `70–80` | Custom/renamed SSID on drone hardware; notes SSID did not match standard prefix |
| Shared Chipset (`Espressif`, `Bilian`, etc.) | Matching Drone SSID | `false` | `true` | `Confirmed` | `OuiAndSsid` | `85–95` | Chipset + famous drone SSID (e.g. Syma, MJX Bugs, Holy Stone) confirms drone |
| None (Unknown / Other Vendor) | Strong Brand Drone SSID | `false` | `true` | `Probable` | `Ssid` | `75–88` | Unknown chipset OUI (or uncataloged SoC), strong drone SSID signature |
| Shared Chipset (`Espressif`, `Bilian`, etc.) | Not Provided or Unmatched | `false` | `false` | `Possible` | `Oui` | `25–35` | Conservative non-confirmation; notes shared IoT/drone chipset requiring SSID |
| Any | Matching Drone SSID | `true` (LAA bit set) | `true` | `Probable` | `Ssid` | `65–78` | Flags LAA/randomized MAC; bases attribution on SSID pattern |
| None | Not Provided or Unmatched | `true` | `false` | `NotRecognized` | `None` | `0` | Flags LAA/randomized MAC; explains BSSID cannot be attributed |
| None | Not Provided or Unmatched | `false` | `false` | `NotRecognized` | `None` | `0` | Clean negative result |

---

## 5. API Query & Response Contracts (`Checker.Application.DTOs`)

### Input Query (`DroneNetworkCheckQuery`)
Bound from query parameters:
- `bssid` (`string`, required): Raw MAC/BSSID string
- `ssid` (`string?`, optional): Observed Wi-Fi SSID string
- Reject any unknown query parameters outside `bssid` and `ssid` (`400` ValidationProblemDetails).

### Response Payload (`DroneNetworkCheckResponse`)
JSON (`camelCase`, `DefaultIgnoreCondition = WhenWritingNull` matching `Management.API`):
- `isDroneStation` (`bool`)
- `verdict` (`string`: `"Confirmed" | "Probable" | "Possible" | "NotRecognized"`)
- `confidenceScore` (`int`: `0..100`)
- `matchSource` (`string`: `"None" | "Oui" | "Ssid" | "OuiAndSsid"`)
- `normalizedBssid` (`string`, e.g., `"60:60:1F:AA:BB:CC"`)
- `ouiPrefix` (`string`, e.g., `"60:60:1F"`)
- `matchedPrefix` (`string?`, e.g., `"EC:5B:CD:E"` or `"60:60:1F"`)
- `isLocallyAdministered` (`bool`)
- `observedSsid` (`string?`)
- `matchedSsidPattern` (`string?`)
- `hardwareVendor` (`string?`, IEEE registrant or chipset vendor)
- `brandMark` (`string?`, e.g., `"DJI"`, `"Syma"`, `"MJX"`, `"Parrot"`, `"Autel Robotics"`)
- `chipsetExclusivity` (`string?`: `"DedicatedDroneVendor" | "SharedConsumerChipset"`)
- `stationRole` (`string`: `"Unknown" | "AircraftAp" | "GroundController" | "FpvVtx" | "TelemetryModule" | "MultiRole"`)
- `candidateModels` (`string[]`)
- `frequencyBands` (`string[]`, e.g., `["2.4 GHz", "5.8 GHz"]`)
- `frequencyRangesMhz` (`string[]`, e.g., `["2400-2483.5 MHz", "5725-5850 MHz"]`)
- `channelSummary` (`string?`)
- `radioProtocols` (`string[]`, e.g., `["Wi-Fi 802.11n/ac", "OcuSync"]`)
- `signalRangeTier` (`string`: `"Unknown" | "ShortRangeWiFi" | "MediumRangeWiFi" | "ExtendedRangeDigital"`)
- `knownSsidPatterns` (`string[]`)
- `notes` (`string[]`)
