# Management Service — Testing & Verification

---

## 1. Automated Test Status

> [!NOTE]
> `src/services/Management` currently has **no automated unit or integration test project** in the repository.

Static quality and compilation checks available via `src/services/Management/package.json`:

```powershell
# Compile Management solution/API
pnpm --filter managementService build

# Check C# whitespace formatting without modifying files
pnpm --filter managementService format:check

# Auto-format C# whitespace
pnpm --filter managementService format
```

---

## 2. Manual & Scripted API Verification

When running in Development (`pnpm docker:dev` or `pnpm --filter managementService dev`), you can verify endpoints via **Development Swagger UI** (`https://localhost:8443/api/management/swagger/index.html`) or `curl.exe`:

> [!TIP]
> Management's Swagger UI registers custom request & response interceptors in `Program.cs` that automatically fetch `/auth/csrf`, inject `X-CSRF-TOKEN` on non-GET requests, send same-origin cookies, and pre-authorize `Bearer` on native logins.

### A. Native Login + Authenticated Calls (`curl.exe`)

```powershell
# 1. Login with native transport to receive accessToken and refreshToken in JSON
curl.exe -k -X POST https://localhost:8443/api/management/auth/login `
  -H "Content-Type: application/json" `
  -H "X-Auth-Transport: native" `
  -d "{\"userName\":\"admin\",\"password\":\"<BOOTSTRAP_ADMIN_PASSWORD>\"}"

# 2. Call GET /auth/me using the returned accessToken
curl.exe -k https://localhost:8443/api/management/auth/me `
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# 3. Rotate tokens via POST /auth/refresh
curl.exe -k -X POST https://localhost:8443/api/management/auth/refresh `
  -H "Content-Type: application/json" `
  -H "X-Auth-Transport: native" `
  -d "{\"refreshToken\":\"<REFRESH_TOKEN>\"}"
```

### B. Browser Cookie + CSRF Flow (`curl.exe` with cookie jar)

```powershell
# 1. Fetch CSRF cookie + requestToken
curl.exe -k -c cookies.txt https://localhost:8443/api/management/auth/csrf

# 2. Login with CSRF cookie + X-CSRF-TOKEN header
curl.exe -k -b cookies.txt -c cookies.txt -X POST https://localhost:8443/api/management/auth/login `
  -H "Content-Type: application/json" `
  -H "X-CSRF-TOKEN: <REQUEST_TOKEN>" `
  -d "{\"userName\":\"admin\",\"password\":\"<BOOTSTRAP_ADMIN_PASSWORD>\"}"

# 3. Authenticated GET using cookie jar
curl.exe -k -b cookies.txt https://localhost:8443/api/management/users
```

### C. Key Invariants to Verify During Manual / Smoke Testing
- Sending an unknown JSON property (e.g., `"roles": ["Admin"]` to `POST /auth/register`) returns `400 ValidationProblemDetails` (`UnmappedMemberHandling.Disallow`).
- Reusing a rotated refresh token triggers `401 Unauthorized` and immediately revokes the entire session (subsequent `/auth/me` with its access JWT also returns `401`).
- Exceeding 20 requests/minute on `POST /auth/login` or `POST /auth/register` returns `429 Too Many Requests` `ProblemDetails`.
- Attempting to delete or rename the bootstrap `Admin` role returns `409 Conflict`.
