# Checker Service — API Endpoints Reference

- **Base URL (via Gateway):** `https://localhost:8443/api/checker`
- **Direct Local Dev URL:** `https://localhost:7150`
- **Development Swagger UI:** `https://localhost:8443/api/checker/swagger/index.html` (returns `404` outside `Development`)
- **JSON Serialization:** Enums serialize as strings (`JsonStringEnumConverter`), null fields are omitted (`DefaultIgnoreCondition = WhenWritingNull`), and unknown properties are rejected (`UnmappedMemberHandling.Disallow`).
- **Security Headers on Every Response:** `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`.

### Authentication & Rate Limiting
- **Protected Endpoints (`/networks/*`):** Require either:
  - `Authorization: Bearer <jwt>` (Management-issued `HS512` JWT with valid `iss`, `aud`, non-expired `exp` with zero clock skew, positive integer `sub` & `sid`, and non-empty `jti`), **OR**
  - `__Secure-DroneVision.Refresh` HttpOnly cookie (validated via `Management` `/auth/me`).
- **Rate Limiting:** Global fixed-window per-IP limit of `60` requests/minute (`RateLimiting:LookupRequestsPerMinute`), returning `429 Too Many Requests`. **`GET /health` is exempt from both authentication and rate limiting.**

---

## Endpoints Summary

| Method | Path | Auth | Query Parameters | Description |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/networks/check` | Required (`BrowserSession` or `Bearer`) | `bssid` (required), `ssid` (optional) | Inspect a single wireless station BSSID/SSID against drone signatures |
| `GET` | `/networks/scan` | Required (`BrowserSession` or `Bearer`) | `includePossible` (optional `bool`, default `false`) | Scan nearby host Wi-Fi networks and return filtered drone stations |
| `GET` | `/health` | Public (`[AllowAnonymous]`) | None | Liveness/readiness health probe (`{"status":"Healthy"}`) |

---

## 1. `GET /networks/check`

Evaluates a wireless station BSSID (and optional SSID) against curated IEEE drone OUI/MA-M blocks, shared consumer Wi-Fi chipsets, and drone SSID naming conventions.

### Query Parameters (Strict Allowlist)
Any query parameter other than `bssid` or `ssid` is rejected by `RejectUnexpectedQueryKeys` with `400 Bad Request`.

| Parameter | Type | Required | Validation Rules | Examples |
| :--- | :--- | :--- | :--- | :--- |
| `bssid` | `string` | Yes | 48-bit MAC in colon (`60:60:1F:AA:BB:CC`), hyphen (`60-60-1F-AA-BB-CC`), Cisco dot (`6060.1FAA.BBCC`), or bare 12-hex (`60601FAABBCC`) format. Rejects empty, all-zero (`00:00:00:00:00:00`), and broadcast (`FF:FF:FF:FF:FF:FF`). | `60:60:1F:AA:BB:CC` |
| `ssid` | `string` | No | If the `ssid` query key is present, it must not be empty/whitespace, must be `1..32` UTF-8 bytes, and must contain zero ASCII control chars (`< 0x20` or `0x7F`). | `Mavic-3-Pro` |

### Success Responses (`200 OK` — `DroneNetworkCheckResponse`)

*(Note: nullable properties equal to `null` are omitted by `JsonIgnoreCondition.WhenWritingNull`.)*

#### Example A: Dedicated Drone Vendor OUI (`60:60:1F:AA:BB:CC`)
```json
{
  "isDroneStation": true,
  "verdict": "Confirmed",
  "confidenceScore": 90,
  "matchSource": "Oui",
  "normalizedBssid": "60:60:1F:AA:BB:CC",
  "ouiPrefix": "60:60:1F",
  "matchedPrefix": "60:60:1F",
  "isLocallyAdministered": false,
  "isMulticast": false,
  "hardwareVendor": "SZ DJI Technology Co., Ltd.",
  "brandMark": "DJI",
  "chipsetExclusivity": "DedicatedDroneVendor",
  "stationRole": "MultiRole",
  "candidateModels": [
    "Mavic Series (Mavic 2 / 3 / Pro / Enterprise)",
    "Air Series (Mavic Air / Air 2 / 2S / 3)",
    "Mini Series (Mini 2 / 3 / 4 Pro)",
    "Phantom Series (Phantom 3 / 4)",
    "Inspire Series (Inspire 1 / 2 / 3)",
    "Matrice Series (M200 / M300 / M350 RTK)",
    "Avata / DJI FPV / Spark",
    "DJI Smart / RC / RC Pro Controllers"
  ],
  "frequencyBands": ["2.4 GHz", "5 GHz", "5.8 GHz"],
  "frequencyRangesMhz": ["2400-2483.5 MHz", "5150-5250 MHz", "5725-5850 MHz"],
  "channelSummary": "2.4 GHz ISM (Ch 1-13) / 5.2 & 5.8 GHz (OcuSync / Lightbridge / Wi-Fi)",
  "radioProtocols": ["Wi-Fi 802.11a/b/g/n/ac", "DJI OcuSync (O2/O3/O4)", "DJI Lightbridge"],
  "signalRangeTier": "ExtendedRangeDigital",
  "knownSsidPatterns": ["DJI-*", "Mavic-*", "Phantom*", "Inspire*", "Spark-*", "TELLO-*"],
  "notes": [
    "Matched dedicated drone manufacturer IEEE block (DJI).",
    "Verified SZ DJI Technology IEEE MA-L allocations covering aircraft Wi-Fi APs, controllers, and telemetry links."
  ]
}
```

#### Example B: Shared Chipset Without Corroborating SSID (`24:0A:C4:11:22:33`)
```json
{
  "isDroneStation": false,
  "verdict": "Possible",
  "confidenceScore": 30,
  "matchSource": "Oui",
  "normalizedBssid": "24:0A:C4:11:22:33",
  "ouiPrefix": "24:0A:C4",
  "matchedPrefix": "24:0A:C4",
  "isLocallyAdministered": false,
  "isMulticast": false,
  "hardwareVendor": "Espressif Inc.",
  "brandMark": "Espressif (Shared Wi-Fi Chipset)",
  "chipsetExclusivity": "SharedConsumerChipset",
  "stationRole": "TelemetryModule",
  "candidateModels": [
    "ExpressLRS Wi-Fi TX/RX Modules",
    "DIY ESP32/ESP8266 Micro-Drones",
    "Budget Wi-Fi FPV Toy Drones (MJX / Syma / Cheerson)"
  ],
  "frequencyBands": ["2.4 GHz"],
  "frequencyRangesMhz": ["2400-2483.5 MHz"],
  "channelSummary": "2.4 GHz ISM (Ch 1-13)",
  "radioProtocols": ["Wi-Fi 802.11b/g/n"],
  "signalRangeTier": "ShortRangeWiFi",
  "knownSsidPatterns": ["ExpressLRS TX*", "ExpressLRS RX*", "Bugs_*", "SYMA_*", "WiFi_UFO_*"],
  "notes": [
    "Matched shared consumer Wi-Fi chipset (Espressif Inc.) commonly used in both general-purpose IoT hardware and budget Wi-Fi drones; supply a corroborating SSID to confirm.",
    "Common ESP8266/ESP32 Wi-Fi SoC used across IoT devices, DIY drones, and ExpressLRS Wi-Fi interfaces."
  ]
}
```

### Response Field & Enum Reference

| Field | Type | Values / Description |
| :--- | :--- | :--- |
| `isDroneStation` | `boolean` | `true` for `Confirmed` and `Probable`; `false` for `Possible` and `NotRecognized` |
| `verdict` | `DetectionVerdict` | `NotRecognized`, `Possible`, `Probable`, `Confirmed` |
| `confidenceScore` | `int` (`0..100`) | Deterministic score from `VerdictScoringPolicy` |
| `matchSource` | `MatchSource` | `None`, `Oui`, `Ssid`, `OuiAndSsid` |
| `normalizedBssid` | `string` | Canonical uppercase colon format (`XX:XX:XX:XX:XX:XX`) |
| `ouiPrefix` | `string` | First 24 bits (`XX:XX:XX`) |
| `matchedPrefix` | `string?` | Formatted 24-bit (`XX:XX:XX`), 28-bit (`XX:XX:XX:X`), or 36-bit (`XX:XX:XX:XX:X`) matched prefix |
| `isLocallyAdministered` | `boolean` | Bit `0x02` of first octet set (randomized / LAA MAC) |
| `isMulticast` | `boolean` | Bit `0x01` of first octet set |
| `chipsetExclusivity` | `ChipsetExclusivity?` | `DedicatedDroneVendor`, `SharedConsumerChipset` |
| `stationRole` | `StationRole` | `Unknown`, `AircraftAp`, `GroundController`, `FpvVtx`, `TelemetryModule`, `MultiRole` |
| `signalRangeTier` | `SignalRangeTier` | `Unknown`, `ShortRangeWiFi`, `MediumRangeWiFi`, `ExtendedRangeDigital` |

---

## 2. `GET /networks/scan`

Triggers a live Wi-Fi scan on the host wireless interface (`netsh.exe` on Windows, `nmcli` on Linux), evaluates every discovered BSSID/SSID, filters out non-drone routers/devices, and returns matching drone stations sorted by `confidenceScore` desc, `signalStrengthPercent` desc, and `bssid` asc.

### Query Parameters (Strict Allowlist)
Any query key other than `includePossible` returns `400 Bad Request`.

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `includePossible` | `bool` | `false` | When `false`, excludes uncorroborated `SharedConsumerChipset` stations (`verdict: "Possible"`). Set `true` to include them. |

### Success Response (`200 OK` — `ScannedDroneNetworksResponse`)
```json
{
  "scannedAtUtc": "2026-11-22T20:15:00Z",
  "totalNetworksScanned": 14,
  "droneCount": 1,
  "drones": [
    {
      "ssid": "Mavic-3-8811",
      "bssid": "60:60:1F:AA:BB:CC",
      "signalStrengthPercent": 92,
      "rssiDbm": -54,
      "band": "5.8 GHz",
      "frequencyMhz": 5745,
      "channel": 149,
      "radioType": "802.11ac",
      "authentication": "WPA2-Personal",
      "isDroneStation": true,
      "verdict": "Confirmed",
      "confidenceScore": 97,
      "matchSource": "OuiAndSsid",
      "brandMark": "DJI",
      "hardwareVendor": "SZ DJI Technology Co., Ltd.",
      "chipsetExclusivity": "DedicatedDroneVendor",
      "stationRole": "AircraftAp",
      "candidateModels": ["Mavic Series", "..."],
      "supportedFrequencyBands": ["2.4 GHz", "5.8 GHz"],
      "frequencyRangesMhz": ["2400-2483.5 MHz", "5725-5850 MHz"],
      "radioProtocols": ["Wi-Fi 802.11a/b/g/n/ac", "OcuSync"],
      "signalRangeTier": "ExtendedRangeDigital",
      "matchedPrefix": "60:60:1F",
      "matchedSsidPattern": "DJI-* / Mavic-* / Phantom* / Inspire* / Spark-*",
      "isLocallyAdministered": false,
      "notes": ["..."]
    }
  ]
}
```

### Error Responses (`/networks/check` & `/networks/scan`)

| Status | Cause | Payload Type (`application/problem+json`) |
| :--- | :--- | :--- |
| `400` | Invalid `bssid`, invalid `ssid` (>32 UTF-8 bytes / empty / control chars), invalid `includePossible` bool, or unknown query parameter | `ValidationProblemDetails` with `errors` dictionary and `traceId` |
| `401` | Missing/invalid/expired Bearer JWT, or invalid/revoked `__Secure-DroneVision.Refresh` cookie | `ProblemDetails` + `WWW-Authenticate: Bearer` (+ deletes invalid cookie) |
| `429` | Client IP exceeds `RateLimiting:LookupRequestsPerMinute` (`60` req/min) | `ProblemDetails` |
| `500` | Unexpected server error | Sanitized `ProblemDetails` with `traceId` |
| `503` | `GET /networks/scan` only: no wireless adapter, unsupported OS, `nmcli` missing, or 8-second scan timeout | `ProblemDetails` (`Title: "Service Unavailable"`) |

---

## 3. `GET /health`

- **Auth:** Public (`[AllowAnonymous]`) · **Rate Limit:** Disabled (`DisableRateLimiting()`)
- **Success Response (`200 OK`, `application/json`):**
```json
{
  "status": "Healthy"
}
```
