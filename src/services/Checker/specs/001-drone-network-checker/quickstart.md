# Quickstart & End-to-End Validation Guide: Drone Network Checker

**Feature**: `001-drone-network-checker` | **Date**: 2026-11-22

---

## Prerequisites

- `.NET 10.0 SDK` installed locally (`dotnet --version` >= `10.0`)
- Docker Desktop / Docker Compose (for full-stack Gateway + Management + Checker verification)
- Root `.env` configured at `D:\projects\drone-analyze\.env` (with `JWT_SIGNING_KEY` Base64 >= 64 random bytes, `GATEWAY_CERT_PASSWORD`, `MSSQL_SA_PASSWORD`, `BOOTSTRAP_ADMIN_PASSWORD`) and `.certs/gateway.pfx` per [DOCKER.md](../../../../DOCKER.md)

---

## Scenario 1: Automated Unit & Integration Tests (Local)

Run from `src/services/Checker`:

```powershell
dotnet test Checker.slnx
```

**Expected Outcome**: All domain unit tests (MAC normalization, LAA bit detection, variable-length OUI/MA-M/MA-S lookups, ReDoS-safe SSID evaluation, brand coverage across DJI, Syma, MJX, Bugs, Parrot, Autel, Hubsan, Yuneec, Skydio, Tello, Holy Stone, Potensic, shared chipsets) and API integration tests (JWT 401 enforcement, 400 validation, 429 rate limiting, security headers, Development vs Production Swagger lockout) pass.

---

## Scenario 2: Full-Stack Docker Development Run + Gateway & Swagger Validation

From the repository root (`D:\projects\drone-analyze`):

```powershell
pnpm docker:dev
```

1. **Verify Service Health**:
   - `GET https://localhost:8443/api/checker/health` -> `200 OK` (`{"status":"Healthy"}`) without requiring authentication.
2. **Verify Development Swagger UI**:
   - Open `https://localhost:8443/api/checker/swagger/index.html` in a browser.
   - Confirm `/networks/check` appears with `bssid` (required), `ssid` (optional), and the `Bearer` JWT security button.
3. **Verify Unauthenticated Rejection**:
   - Call `GET https://localhost:8443/api/checker/networks/check?bssid=60:60:1F:12:34:56` with no `Authorization` header.
   - Expected: `401 Unauthorized` with `application/problem+json` body, `WWW-Authenticate: Bearer`, and no internal stack/token diagnostics.
4. **Obtain a Management JWT Access Token**:
   - Authenticate via `POST https://localhost:8443/api/management/auth/login` with header `X-Auth-Transport: native` and valid credentials (`admin` / `$BOOTSTRAP_ADMIN_PASSWORD`) to receive `accessToken`.
5. **Authenticated Lookup Scenarios** (pass `Authorization: Bearer <accessToken>`):
   - **Dedicated Drone OUI (DJI)**: `GET https://localhost:8443/api/checker/networks/check?bssid=60:60:1F:AA:BB:CC` -> `200 OK`, `isDroneStation: true`, `verdict: "Confirmed"`, `brandMark: "DJI"`, `frequencyBands: ["2.4 GHz", "5.8 GHz"]`.
   - **MA-M 28-bit Drone Prefix (Autel)**: `GET https://localhost:8443/api/checker/networks/check?bssid=EC:5B:CD:E1:22:33` -> `200 OK`, `isDroneStation: true`, `verdict: "Confirmed"`, `brandMark: "Autel Robotics"`.
   - **Shared Chipset + Famous Drone SSID (Syma / MJX Bugs)**: `GET https://localhost:8443/api/checker/networks/check?bssid=20:F4:1B:11:22:33&ssid=Bugs_7421` -> `200 OK`, `isDroneStation: true`, `verdict: "Confirmed"`, `matchSource: "OuiAndSsid"`, `brandMark: "MJX"`.
   - **Shared Chipset Without SSID (Espressif)**: `GET https://localhost:8443/api/checker/networks/check?bssid=24:0A:C4:11:22:33` -> `200 OK`, `isDroneStation: false`, `verdict: "Possible"`, noting SSID correlation required.
   - **Non-Drone BSSID**: `GET https://localhost:8443/api/checker/networks/check?bssid=00:2A:6A:01:02:03` -> `200 OK`, `isDroneStation: false`, `verdict: "NotRecognized"`.
   - **Invalid BSSID / Oversized SSID**: `GET https://localhost:8443/api/checker/networks/check?bssid=not-a-mac` -> `400 Bad Request` (`application/problem+json` `ValidationProblemDetails`).
   - **Rate Limiting Burst**: Exceed configured requests-per-minute threshold -> `429 Too Many Requests` (`application/problem+json`).

---

## Scenario 3: Production Container & Swagger Lockout Verification

From the repository root (`D:\projects\drone-analyze`):

```powershell
docker compose down
pnpm docker:build:prod
pnpm docker:prod
```

- Verify `GET https://localhost:8443/api/checker/swagger/index.html` returns `404 Not Found`.
- Verify `GET https://localhost:8443/api/checker/swagger/v1/swagger.json` returns `404 Not Found`.
- Verify `docker compose exec checker id` reports non-root UID (`$APP_UID`, non-zero).
- Verify authenticated `GET https://localhost:8443/api/checker/networks/check?bssid=60:60:1F:AA:BB:CC` returns `200 OK` with `X-Content-Type-Options: nosniff` and `Cache-Control: no-store`.

---

## Scenario 4: Fail-Fast Startup Security Check

Run Checker with an intentionally weak or empty `Jwt__SigningKey` (fewer than 64 random bytes Base64):
- Expected: Container/process fails immediately on startup validation (`OptionsValidationException`) rather than serving requests.

---

## Reference Links
- API Schema: [contracts/openapi.yaml](contracts/openapi.yaml)
- Gateway & Compose Wiring: [contracts/gateway-route.md](contracts/gateway-route.md)
- Entities & Scoring Matrix: [data-model.md](data-model.md)
