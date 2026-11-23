# Quickstart & End-to-End Validation Guide: Multi-WLAN Drone Scanning

**Feature**: `002-multi-wlan-scan` | **Date**: 2026-11-24

---

## Prerequisites

- `.NET 10.0 SDK` installed locally (`dotnet --version` >= `10.0`)
- Docker Desktop / Docker Compose (for Windows Docker and Linux Docker verification)
- Valid Management JWT token or browser session cookie (`__Secure-DroneVision.Refresh`)

---

## Scenario 1: Automated Unit & Integration Tests

Run from `src/services/Checker`:

```powershell
dotnet test Checker.slnx
```

**Expected Outcome**:
- Multi-interface parser unit tests pass (`NetshWlanInterfacesParser`, `NetshWiFiScanParser` interface attribution, `IwWiFiScanParser`, `NmcliWiFiScanParser`, `WiFiChannelConverter` frequency-to-channel and RSSI-to-percent mapping).
- Deduplication unit tests prove duplicate BSSIDs observed across multiple WLAN interfaces merge into one entry keeping strongest signal, captured SSID, and sorted `interfaceNames`.
- Partial-failure unit tests prove one failing/down WLAN interface does not fail the scan when another healthy interface succeeds.
- API integration tests verify:
  - `GET /networks/interfaces` enforces auth (`401`), rejects unexpected query params (`400`), returns `available: true` + interfaces when present, and returns `200 { available: false, interfaces: [] }` when hardware is unavailable.
  - `GET /networks/scan` returns `503` (`application/problem+json`) when hardware is unavailable, and returns deduplicated streamlined 15-field drone objects + `scannedInterfaces` when hardware is present.
  - `GET /networks/check` returns the streamlined 9-field payload without removed diagnostic keys.

---

## Scenario 2: Windows Docker Safe-Fallback Verification

From repository root (`D:\projects\drone-analyze`) on Windows Docker Desktop:

```powershell
pnpm docker:dev
```

1. **Authenticate** and obtain a Management JWT access token.
2. **Check WLAN Interfaces**:
   - `GET https://localhost:8443/api/checker/networks/interfaces`
   - **Expected**: `200 OK` with `{ "available": false, "reason": "...", "interfaces": [] }`.
3. **Attempt Live Scan**:
   - `GET https://localhost:8443/api/checker/networks/scan`
   - **Expected**: `503 Service Unavailable` (`application/problem+json` `ProblemDetails`) with sanitized explanation and no container crash or hang.
4. **Verify Streamlined Single-Station Check Continues Working**:
   - `GET https://localhost:8443/api/checker/networks/check?bssid=60:60:1F:AA:BB:CC`
   - **Expected**: `200 OK` returning only the 9 essential fields (`isDroneStation`, `verdict`, `confidenceScore`, `bssid`, `brandMark`, `candidateModels`, `frequencyBands`, `stationRole`; plus `ssid` when supplied).

---

## Scenario 3: Native Windows Multi-WLAN Verification (Local `pnpm dev`)

From `src/services/Checker` on a Windows machine with one or more Wi-Fi adapters:

```powershell
pnpm dev
```

1. `GET /networks/interfaces` -> `200 OK` with `available: true` and discovered Windows WLAN adapter(s) (e.g., `Wi-Fi`, `Wi-Fi 2`).
2. `GET /networks/scan` -> `200 OK` returning `scannedInterfaces`, deduplicated `totalNetworksScanned`, and filtered drone stations with `interfaceNames`.

---

## Scenario 4: Linux Docker Container Build & Multi-WLAN Verification

Build the Linux production image from `src/services/Checker`:

```bash
docker build --target runtime -t drone-analyze-checker:test .
docker run --rm --entrypoint sh drone-analyze-checker:test -c "command -v iw && command -v nmcli && id"
```

**Expected Outcome**:
- Both `iw` and `nmcli` are present inside the Linux container image.
- `id` confirms non-root execution (`uid != 0`).
- On a Linux host with WLAN adapters (using either host D-Bus mount for `nmcli` or `network_mode: host` + `cap_add: [NET_ADMIN, NET_RAW]` for `iw`):
  - `GET /networks/interfaces` returns `available: true` with only 802.11 WLAN interfaces (`eth0`/`lo`/`docker0` excluded).
  - `GET /networks/scan` scans all usable WLAN interfaces, merges duplicate BSSID observations across interfaces, and returns only detected drone stations in the streamlined format.

---

## Reference Links
- API Contract: [contracts/openapi.yaml](contracts/openapi.yaml)
- Container & Gateway Deployment Contract: [contracts/gateway-route.md](contracts/gateway-route.md)
- Data Model & Deduplication Rules: [data-model.md](data-model.md)
