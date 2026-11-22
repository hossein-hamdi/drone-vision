# API Gateway — Routing & Boundary Endpoints

- **Public Base URL (Docker):** `https://localhost:${GATEWAY_PORT:-8443}`
- **Protocol:** HTTPS (TLS 1.2/1.3 via Kestrel `.certs/gateway.pfx`), forwarding over internal HTTP to downstream containers.
- **Authentication:** Pass-through (`Authorization: Bearer <jwt>`, `__Secure-DroneVision.Refresh`, `__Host-DroneVision.Csrf`, and `X-CSRF-TOKEN` are forwarded transparently to downstream services; the Gateway performs no token validation itself).

---

## 1. Configured Route Table

All `ocelot.*.json` files are loaded and merged into memory at startup (`AddOcelot(builder.Environment, MergeOcelotJson.ToMemory)`).

| Route File | Upstream Path Template | Upstream HTTP Methods | Priority | Downstream Scheme & Host:Port | Downstream Path Template |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `ocelot.management.json` | `/api/management/{everything}` | All (`[]`) | `10` | `http://management:8080` | `/api/management/{everything}` |
| `ocelot.checker.json` | `/api/checker/{everything}` | All (`[]`) | `10` | `http://checker:8080` | `/api/checker/{everything}` |
| `ocelot.frontend.json` | `/` | All (`[]`) | `0` | `http://frontend:3000` | `/` |
| `ocelot.frontend.json` | `/{everything}` | All (`[]`) | `0` | `http://frontend:3000` | `/{everything}` |

---

## 2. Prefix Allowlisting & Boundary Guards

Before calling `app.UseOcelot()`, [`Program.cs`](../../../src/gateway/Program.cs) builds `servicePrefixes` dynamically from `Routes[*].UpstreamPathTemplate` where the prefix before `/{` starts with `/api/` (case-insensitive):
- Extracted prefixes in the current codebase: **`/api/management`**, **`/api/checker`**.

### Boundary Enforcement Matrix

| Request Pattern | Environment | Gateway Behavior | Status Code |
| :--- | :--- | :--- | :---: |
| `/api/management/*` (except `/swagger*`) | Any | Forwarded to `management:8080` | Downstream status |
| `/api/checker/*` (except `/swagger*`) | Any | Forwarded to `checker:8080` | Downstream status |
| `/api/management/swagger*` or `/api/checker/swagger*` | `Development` | Forwarded to downstream Swagger UI / OpenAPI JSON | `200 OK` (or downstream) |
| `/api/management/swagger*` or `/api/checker/swagger*` | `Production` / non-Dev | **Blocked by Gateway middleware** before `UseOcelot()` | `404 Not Found` (empty body) |
| `/api/unknown/*` or `/api` | Any | **Blocked by Gateway middleware** (`!servicePrefix.HasValue`); never falls through to `frontend` | `404 Not Found` (empty body) |
| `/*` (non-`/api` paths) | Any | Forwarded to `frontend:3000` (`Priority: 0`) | Downstream status |

---

## 3. Header Handling Behavior

For every request passing the boundary guard:

| Header | Direction | Action Taken by Gateway (`Program.cs`) | Reason |
| :--- | :--- | :--- | :--- |
| `X-Forwarded-Proto` | Client → Downstream | **Unconditionally overwritten** with `context.Request.Scheme` (`"https"`) | Prevents spoofing while informing `Management` and `Checker` that external transport is HTTPS so `Secure` cookies (`__Secure-`, `__Host-`) and `UseHttpsRedirection()` work behind HTTP internal links |
| `X-Forwarded-For` | Client → Downstream | **Unconditionally overwritten** with `context.Connection.RemoteIpAddress?.ToString()` | Prevents client IP spoofing against `Management` (`"credentials"` 20 req/min) and `Checker` (global 60 req/min) IP rate limiters |
| `Cookie`, `Authorization`, `X-CSRF-TOKEN`, `X-Auth-Transport`, `Origin`, `Sec-Fetch-Site` | Bidirectional | Passed through unchanged | Preserves same-origin cookies, antiforgery headers, and Bearer tokens |
| `Set-Cookie` | Downstream → Client | Passed through unchanged | Delivers `__Host-DroneVision.Csrf` and `__Secure-DroneVision.Refresh` on `:8443` origin |

---

## 4. Downstream Endpoint Entry URLs (via Gateway)

- **Frontend UI:** `https://localhost:8443/`
- **Management API Base:** `https://localhost:8443/api/management/...`
- **Management Swagger UI (Dev only):** `https://localhost:8443/api/management/swagger/index.html`
- **Checker API Base:** `https://localhost:8443/api/checker/...`
- **Checker Health Check:** `https://localhost:8443/api/checker/health`
- **Checker Swagger UI (Dev only):** `https://localhost:8443/api/checker/swagger/index.html`
