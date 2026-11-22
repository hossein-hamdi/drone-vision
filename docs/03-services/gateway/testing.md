# API Gateway — Testing & Verification

---

## 1. Automated Test Status

> [!NOTE]
> `src/gateway` currently contains **no automated unit or integration test project** in the repository.

Quality checks available in `src/gateway/package.json` are build verification and C# whitespace formatting checks:

```powershell
# Compile Gateway
pnpm --filter gateway build

# Check C# formatting without modifying files
pnpm --filter gateway format:check

# Auto-fix C# whitespace formatting
pnpm --filter gateway format
```

---

## 2. Boundary & Routing Verification Recipes

After starting the Docker stack (`pnpm docker:dev` for Development, or `pnpm docker:prod` for Production), verify the Gateway's routing and security invariants using `curl.exe` (or PowerShell `Invoke-WebRequest`):

### A. Unknown `/api/*` Prefix Returns `404`
```powershell
curl.exe -ik https://localhost:8443/api/nonexistent/test
# Expected: HTTP/1.1 404 Not Found (does NOT render Next.js 404 HTML page)
```

### B. Swagger Availability by Environment
```powershell
# In Development (pnpm docker:dev):
curl.exe -ik https://localhost:8443/api/management/swagger/index.html
curl.exe -ik https://localhost:8443/api/checker/swagger/index.html
# Expected: HTTP/1.1 200 OK

# In Production (pnpm docker:prod):
curl.exe -ik https://localhost:8443/api/management/swagger/index.html
curl.exe -ik https://localhost:8443/api/checker/swagger/index.html
# Expected: HTTP/1.1 404 Not Found (blocked at Gateway boundary)
```

### C. Downstream Service Proxy Verification
```powershell
# Checker health endpoint through Gateway
curl.exe -k https://localhost:8443/api/checker/health
# Expected: {"status":"Healthy"}

# Management CSRF endpoint through Gateway (verifies Set-Cookie __Host-DroneVision.Csrf over HTTPS)
curl.exe -ik https://localhost:8443/api/management/auth/csrf
# Expected: HTTP/1.1 200 OK + Set-Cookie: __Host-DroneVision.Csrf=...; path=/; secure; samesite=strict; httponly
```

### D. Frontend Catch-All Proxy Verification
```powershell
curl.exe -ik https://localhost:8443/
# Expected: HTTP/1.1 307 Temporary Redirect -> /login?rp=%2F (from frontend proxy.ts when unauthenticated)
```
