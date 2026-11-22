# Troubleshooting & Operational Debugging Runbook

Practical diagnostic steps and resolutions for common infrastructure, certificate, database, authentication, Wi-Fi scanning, and testing issues observed in this repository.

---

## 1. Health Checks & Quick Diagnostics

### Check Running Containers & Logs

```powershell
# View status and healthchecks (especially `database`)
docker compose ps

# Follow logs across all services
docker compose logs -f

# Follow a single service
docker compose logs -f gateway
docker compose logs -f management
docker compose logs -f checker
docker compose logs -f frontend
docker compose logs -f database
```

### Live Health & Probe Commands

```powershell
# Checker liveness probe (anonymous, bypasses rate limit)
curl.exe -k https://localhost:8443/api/checker/health
# Expected: {"status":"Healthy"}

# Management CSRF probe (confirms Gateway -> Management -> HTTPS forwarding)
curl.exe -ik https://localhost:8443/api/management/auth/csrf
```

---

## 2. Common Startup & Configuration Failures

### A. Gateway Fails to Start: Missing or Wrong `.certs/gateway.pfx` Password
- **Symptom:** `gateway` container exits immediately with Kestrel certificate load/decrypt error (`CryptographicException` or missing `/certs/gateway.pfx`).
- **Cause:** `.certs/gateway.pfx` does not exist at repo root or `GATEWAY_CERT_PASSWORD` in `.env` does not match the PFX password.
- **Fix:**
  ```powershell
  New-Item -ItemType Directory -Force .certs | Out-Null
  dotnet dev-certs https -ep .certs/gateway.pfx -p '<EXACT_GATEWAY_CERT_PASSWORD_FROM_ENV>'
  dotnet dev-certs https --trust
  docker compose restart gateway
  ```

### B. `OptionsValidationException` on `Management` or `Checker` Startup
- **Symptom:** Container crashes on boot with `Jwt:SigningKey must be a Base64-encoded key of at least 64 random bytes` or `Management:BaseUrl must be an absolute HTTP or HTTPS URI`.
- **Cause:** `JWT_SIGNING_KEY` in `.env` (or User Secrets) is shorter than 64 decoded bytes, not valid Base64, or missing; or `Management__BaseUrl` is malformed.
- **Fix:** Generate a 64-byte Base64 key and ensure both `Management` and `Checker` share the **exact same** `JWT_SIGNING_KEY`:
  ```powershell
  $b = New-Object byte[] 64; [System.Security.Cryptography.RandomNumberGenerator]::Fill($b); [Convert]::ToBase64String($b)
  ```

### C. Management Startup Fails: `BootstrapAdmin:Password` or Password Complexity Error
- **Symptom:** `InvalidOperationException: First startup requires BootstrapAdmin:Password...` or `IdentityErrors` `400` validation failure during `IdentityBootstrap.InitializeAsync()`.
- **Cause:** `BOOTSTRAP_ADMIN_PASSWORD` is missing on first DB boot or violates Identity password rules (<12 chars, or missing digit, lowercase, uppercase, or symbol).
- **Fix:** Set `BOOTSTRAP_ADMIN_PASSWORD` in `.env` (or `BootstrapAdmin:Password` in User Secrets) to >=12 characters containing uppercase, lowercase, digit, and symbol, then restart `management`.

### D. SQL Server Authentication Failure After Changing `.env`
- **Symptom:** `management` fails to connect (`Login failed for user 'sa'`) after editing `MSSQL_SA_PASSWORD`.
- **Cause:** The persistent Docker volume `sql_data` retains the `sa` password set when the volume was first initialized.
- **Fix:** Either restore the original `MSSQL_SA_PASSWORD`, or wipe the local dev volume and re-initialize:
  ```powershell
  docker compose down -v
  pnpm docker:dev
  ```

### E. `IdentityBootstrap` Lock Timeout (`Unable to acquire the identity bootstrap lock`)
- **Symptom:** SQL error `51000` during startup migration/seeding.
- **Cause:** Multiple instances raced for `sp_getapplock` (`DroneVision.IdentityBootstrap`) for more than 60 seconds, or an earlier container crashed mid-transaction.
- **Fix:** Ensure `database` is healthy (`docker compose ps`), check `docker compose logs database`, and restart `management`.

---

## 3. Port, Network & Docker Watch Issues

### A. Port `8443` Already in Use
- **Symptom:** `gateway` fails to bind host port `8443`.
- **Fix:** Override `GATEWAY_PORT` in root `.env` (e.g., `GATEWAY_PORT=9443`) and restart Compose, then access `https://localhost:9443`.

### B. Source Edits Not Hot-Reloading in `pnpm docker:dev`
- **Symptom:** Editing files in `frontend/` or `src/` does not update running dev containers.
- **Cause:** Containers were started with `docker compose up` without attaching `watch`, or the Watch terminal was closed.
- **Fix:** Re-attach Compose Watch without rebuilding:
  ```powershell
  docker compose -f compose.yaml -f compose.dev.yaml watch --no-up
  ```
  If you added a new npm package to `frontend`, update root `pnpm-lock.yaml` via `pnpm install` and run `pnpm docker:build:dev` / `pnpm docker:dev` (Watch also triggers rebuild on `frontend/package.json` or `pnpm-lock.yaml` changes).

### C. Native Gateway Fails with DNS Resolution Errors
- **Symptom:** Running `pnpm --filter gateway dev` natively returns `502` / host resolution failures for `frontend`, `management`, or `checker`.
- **Cause:** `src/gateway/ocelot.*.json` routes to Docker service names (`frontend:3000`, `management:8080`, `checker:8080`).
- **Fix:** Run the integrated stack via `pnpm docker:dev`, or test individual backend APIs directly via their native `launchSettings.json` HTTPS ports (`https://localhost:7146` for Management, `https://localhost:7150` for Checker).

---

## 4. Authentication, CSRF, Gateway & API Errors

### A. `400 Validation Failed`: `A valid CSRF cookie and X-CSRF-TOKEN header are required.`
- **Cause:** Browser cookie write (`POST`/`PUT`/`PATCH`/`DELETE`) or `/auth/login`, `/auth/refresh`, `/auth/logout` omitted `X-CSRF-TOKEN` or `__Host-DroneVision.Csrf`, or `X-Forwarded-Proto: https` was missing (so `Secure` cookie wasn't accepted).
- **Fix:**
  - Always access via HTTPS (`https://localhost:8443`, or `https://localhost:7146` directly).
  - Fetch `GET /api/management/auth/csrf` with `credentials: 'same-origin'` before writes and send `X-CSRF-TOKEN`.
  - For CLI/scripts without cookies, pass header `X-Auth-Transport: native` and omit `Origin`/`Sec-Fetch-Site`/cookies.

### B. `400` When Sending JSON Body: Unmapped Property Error
- **Cause:** Both `Management.API` and `Checker.API` enable `UnmappedMemberHandling.Disallow`, and `Checker` controllers run `RejectUnexpectedQueryKeys`.
- **Fix:** Remove unrecognized JSON fields (e.g., do not send `roles` to `POST /auth/register`) and unknown query parameters.

### C. `401 Unauthorized` Suddenly Revokes Entire Session
- **Cause:** Refresh token replay detection (`AuthService.RefreshAsync`) saw a previously rotated token and revoked the entire `DroneVision.Session` row, OR user was locked out (5 failed logins = 15 min lockout).
- **Fix:** Ensure browser/native clients serialize refresh calls (never fire concurrent `/auth/refresh` requests with the same token) and re-authenticate.

### D. `404 Not Found` on `/api/*/swagger/index.html` or New Service Routes
- **Cause 1:** Running in Production (`pnpm docker:prod`), where Gateway blocks `<servicePrefix>/swagger*` with `404` and backend APIs do not map Swagger.
- **Cause 2:** Calling `/api/<unknown>/*` without a matching `ocelot.<service>.json` file (Gateway rejects unknown `/api/*` prefixes with `404`).
- **Fix:** Switch to `pnpm docker:dev` for Swagger, or add `src/gateway/ocelot.<service>.json` with `Priority: 10` and `UpstreamPathTemplate: "/api/<service>/{everything}"`.

### E. `429 Too Many Requests`
- **Cause:** Client IP hit `RateLimiting:AuthenticationRequestsPerMinute` (`20`/min on Management `/auth/register` & `/auth/login`) or `RateLimiting:LookupRequestsPerMinute` (`60`/min on Checker `/networks/*`).
- **Fix:** Wait for the 1-minute fixed window reset, or increase the config value during load/QA testing.

---

## 5. Checker Wi-Fi Scan `503 Service Unavailable` in Windows Docker

- **Symptom:** `GET /api/checker/networks/scan` returns `503 Service Unavailable` (`Unable to execute wireless scan on the Linux host.` or `No active wireless network interface is available`).
- **Cause:** Docker Desktop on Windows runs Linux containers inside a WSL2 VM with virtualized Ethernet (`eth0`/`lo`) and **no PCIe Wi-Fi adapter passthrough**, so `nmcli` inside the `checker` container sees zero wireless interfaces.
- **Resolution:**
  - Use `GET /api/checker/networks/check?bssid=...&ssid=...` inside Docker (does not require host Wi-Fi hardware).
  - For live host Wi-Fi scanning on Windows, run Checker natively (`pnpm --filter checkerService dev` on `https://localhost:7150`), where `PlatformWiFiScanner` invokes `%SystemRoot%\System32\netsh.exe wlan show networks mode=bssid`, or deploy the container on a Linux host with a wireless interface and NetworkManager (`nmcli`).

---

## 6. Frontend Vitest / Playwright Browser Test Issues

- **Symptom:** `pnpm --filter frontend exec vitest run --project browser` fails launching Chromium.
- **Fix:** Install the Playwright browser binary for the workspace:
  ```powershell
  pnpm --filter frontend exec playwright install chromium
  ```
- **Quick Static Verification Before Committing:**
  Remember `.husky/pre-commit` runs `lint-staged` (ESLint `--max-warnings=-1`, Prettier, `tsc --noEmit`, and `dotnet format whitespace`), and `.husky/commit-msg` enforces Conventional Commits (`feat:`, `fix:`, `docs:`, etc.). Run `pnpm --filter frontend check` before committing frontend edits.
