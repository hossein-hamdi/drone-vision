# Checker Service — Architecture & Business Logic

---

## 1. Clean Architecture Layering

| Project | Dependencies | Responsibilities |
| :--- | :--- | :--- |
| **`Checker.Domain`** | None | `MacAddress` and `SsidName` value objects, `DroneSignatureEntry`, `SsidPatternRule`, `FrequencyProfile`, and 6 domain enums. |
| **`Checker.Application`** | `Checker.Domain` | `DroneNetworkCheckerService`, `MacPrefixEvidenceEvaluator`, `SsidEvidenceEvaluator`, `VerdictScoringPolicy`, DTOs, abstractions (`IDroneSignatureCatalog`, `IWiFiScanner`), `ApiException`. |
| **`Checker.Infrastructure`** | `Checker.Application`, `Checker.Domain`, `Microsoft.AspNetCore.App` | `EmbeddedDroneSignatureCatalog` (`drone-signatures.json` embedded resource), `PlatformWiFiScanner`, `NetshWiFiScanParser`, `NmcliWiFiScanParser`, `WiFiChannelConverter`, `JwtOptions`, `ManagementSessionOptions`. |
| **`Checker.API`** | `Checker.Application`, `Checker.Infrastructure` | `Program.cs`, `NetworkCheckController`, `BrowserSessionHandler`, `ManagementSessionValidator`, `IExceptionHandler` pipeline, rate limiter, health check, Swagger. |

---

## 2. Value Objects & Input Validation (`Checker.Domain`)

### `MacAddress` (`IParsable<MacAddress>`)
- Accepts 4 formats (case-insensitive, trimmed):
  1. Colon-separated (`XX:XX:XX:XX:XX:XX`, 17 chars)
  2. Hyphen-separated (`XX-XX-XX-XX-XX-XX`, 17 chars)
  3. Cisco dot notation (`XXXX.XXXX.XXXX`, 14 chars)
  4. Bare 12 hex characters (`XXXXXXXXXXXX`, 12 chars)
- Rejects `null`/whitespace, mixed separators, non-hex digits, wrong lengths, all-zero (`00:00:00:00:00:00`), and broadcast (`FF:FF:FF:FF:FF:FF`).
- Extracts:
  - `Canonical` (`XX:XX:XX:XX:XX:XX`), `CompactHex` (12 uppercase hex)
  - `Oui24` (`XX:XX:XX`), `Prefix28` (7 hex chars), `Prefix36` (9 hex chars)
  - `IsMulticast`: `(bytes[0] & 0x01) != 0`
  - `IsLocallyAdministered` (LAA bit): `(bytes[0] & 0x02) != 0`

### `SsidName`
- Rejects `null`, empty, or whitespace-only when the `ssid` parameter is explicitly supplied (`SsidWasProvided`).
- Trims whitespace and enforces **`<= 32` UTF-8 bytes** (`MaxUtf8Bytes = 32`) per IEEE 802.11.
- Rejects ASCII control characters (`ch < 0x20 || ch == 0x7F`).

---

## 3. Embedded Signature Catalog (`EmbeddedDroneSignatureCatalog`)

`Checker.Infrastructure/Catalog/Data/drone-signatures.json` is compiled directly into `Checker.Infrastructure.dll` as an `<EmbeddedResource>`—requiring **zero database connectivity**.

At singleton construction:
1. **Longest-Prefix Indexing (`FrozenDictionary`):**
   - Loads `prefixEntries` into three immutable `FrozenDictionary<string, DroneSignatureEntry>` tables:
     - `_prefixes36` (9 hex chars / 36-bit MA-S)
     - `_prefixes28` (7 hex chars / 28-bit MA-M): Autel Robotics (`EC:5B:CD:E`), Hubsan (`98:AA:FC:7`), Yuneec (`E0:B6:F5:8`)
     - `_prefixes24` (6 hex chars / 24-bit MA-L):
       - Dedicated vendors: DJI (10 prefixes), Parrot (4 prefixes), Skydio (`38:1D:14`)
       - Shared consumer Wi-Fi chipsets: Shenzhen Bilian (`20:F4:1B`), Espressif (6 prefixes), Realtek (`00:E0:4C`)
   - `FindBestPrefixMatch()` searches `36-bit → 28-bit → 24-bit` in `O(1)` time so more specific blocks always override broader allocations.
2. **ReDoS-Safe SSID Rules (`RegexOptions.NonBacktracking`):**
   - Compiles all `19` `ssidRules` (covering Ryze/DJI Tello, DJI aircraft, Syma FPV, MJX Bugs, generic `WiFi_UFO_*`, Parrot Anafi/Bebop, Autel EVO, Hubsan Zino, Yuneec Typhoon/Breeze/H520, Skydio, Holy Stone, Potensic, Eachine, JJRC, Cheerson, Walkera, 3DR SoloLink, HoverAir/ZeroZero, ExpressLRS/OpenHD/ArduPilot) with `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking` and a `50ms` match timeout.

---

## 4. Evidence Evaluation & `VerdictScoringPolicy`

`DroneNetworkCheckerService.EvaluateSingle()` runs registered `INetworkEvidenceEvaluator`s (`MacPrefixEvidenceEvaluator` and `SsidEvidenceEvaluator`) into an `EvidenceAccumulator`, then passes the result to `VerdictScoringPolicy.EvaluateVerdict()`:

| Case | Condition | `isDroneStation` | `Verdict` | `ConfidenceScore` | `MatchSource` | Notes / Rules |
| :---: | :--- | :---: | :--- | :---: | :--- | :--- |
| **1a** | `mac.IsLocallyAdministered` + SSID rule matched | `true` | `Probable` | `Clamp(Weight - 15, 65, 78)` | `Ssid` | Never confirms via OUI when LAA bit is set; attributes via SSID |
| **1b** | `mac.IsLocallyAdministered` + no SSID rule | `false` | `NotRecognized` | `0` | `None` | Cannot reliably attribute randomized MAC |
| **2** | MAC Prefix + SSID rule both matched | `true` | `Confirmed` | Dedicated & same ecosystem: `Clamp(Max(95, Weight+5), 95, 100)`; Dedicated diff ecosystem: `92`; Shared chipset: `Clamp(Max(85, Weight), 85, 95)` | `OuiAndSsid` | Correlates shared chipsets (e.g., Espressif + `Bugs_7741`) to `Confirmed` |
| **3a** | `DedicatedDroneVendor` MAC + no SSID provided | `true` | `Confirmed` | `90` | `Oui` | Confirmed by dedicated IEEE allocation |
| **3b** | `DedicatedDroneVendor` MAC + unmatched SSID provided | `true` | `Probable` | `76` | `Oui` | May be a custom-renamed aircraft AP |
| **4** | `SharedConsumerChipset` MAC + no matching drone SSID | `false` | `Possible` | `30` | `Oui` | Conservative non-confirmation to avoid flagging smart plugs/PCs/routers |
| **5** | Unknown MAC + SSID rule matched | `true` | `Probable` | `Clamp(Weight, 75, 88)` | `Ssid` | Drone SSID naming detected without known drone OUI |
| **6** | Neither MAC prefix nor SSID matched | `false` | `NotRecognized` | `0` | `None` | Standard non-drone Wi-Fi station |

---

## 5. Live Wi-Fi Scanning (`PlatformWiFiScanner` & Parsers)

- **Windows (`OperatingSystem.IsWindows()`):** Executes `%SystemRoot%\System32\netsh.exe wlan show networks mode=bssid` (`8s` timeout) and parses blocks via `NetshWiFiScanParser` (`SSID n`, `Authentication`, `BSSID n`, `Signal %`, `Radio type`, `Band`, `Channel`). Throws `503` on non-zero exit code or `"There is no wireless interface"` / `"is not running"`.
- **Linux (`OperatingSystem.IsLinux()`):** Executes `nmcli -t -f SSID,BSSID,SIGNAL,CHAN,FREQ,SECURITY dev wifi list --rescan yes` (`8s` timeout) and parses terse escaped colons (`\:`) via `NmcliWiFiScanParser`. Catches `Win32Exception` (`nmcli` missing) or non-zero exit code and throws `503`.
- **`WiFiChannelConverter`:**
  - Converts channel numbers (`1..14` → `2.4 GHz`; `32..177` → `5 GHz` or `5.8 GHz` at `>= 5725 MHz`; `6 GHz` channels → `5950 + ch * 5`) and explicit frequencies to normalized band labels.
  - Approximates RSSI from signal percentage: `(Math.Clamp(signalPercent, 0, 100) / 2) - 100` dBm.
- **Docker Container Note:** `src/services/Checker/Dockerfile` installs `network-manager` (`nmcli`) in the runtime image (`USER $APP_UID`). Under **Docker Desktop on Windows (WSL2)**, Linux containers lack direct PCIe Wi-Fi adapter passthrough (`eth0`/`lo` only), so `GET /networks/scan` returns `503 Service Unavailable` in Windows Docker unless run natively on the host (`pnpm --filter checkerService dev`) or on Linux with wireless hardware access.

---

## 6. Authentication & Error Handling (`Checker.API`)

- **Bearer JWT:** Validated locally (`HS512`, `Issuer`, `Audience`, `ClockSkew = Zero`, positive integer `sub` & `sid`, non-empty `jti`). Does **not** call `Management` on Bearer requests.
- **Browser Cookie (`BrowserSessionHandler`):** Validates 86-char Base64Url format of `__Secure-DroneVision.Refresh`, then delegates to `ManagementSessionValidator` (`GET ${Management:BaseUrl}/auth/me`, `AllowAutoRedirect = false`, `X-Forwarded-Proto: https`, 3s timeout). Invalid cookies are deleted via `Set-Cookie` on `401`.
- **Exception Chain:** `ValidationExceptionHandler` (`400`) → `ApiExceptionHandler` (`401`/`403`/`404`/`409`/`503`) → `GlobalExceptionHandler` (`499`/`BadHttpRequest`/`500` sanitized with `traceId`).
