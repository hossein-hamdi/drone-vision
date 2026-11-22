# Setup & Run Guide

This guide covers prerequisites, environment variables, TLS certificate creation, and all supported execution modes (Docker Compose Development with Watch, Docker Compose Production, and Native Local Execution).

---

## 1. Prerequisites

| Tool | Required Version / Notes | Used By |
| :--- | :--- | :--- |
| **Node.js** | `24.x` (Docker images use `node:24-alpine`; `@types/node` `^20`) | Root workspace, `frontend` |
| **pnpm** | `12.21.0` (configured in root `package.json` `packageManager` & `devEngines`; `src/gateway` & `frontend` list `12.16.1` locally) | Monorepo workspace & scripts |
| **.NET SDK** | `.NET 10.0` (`net10.0`) | `gateway`, `Management`, `Checker`, `dotnet dev-certs`, `dotnet-ef` |
| **Docker Desktop / Docker Engine** | Docker Compose v2 with `include` and `develop.watch` (`initial_sync`) support | Full-stack container execution |
| **SQL Server** | SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest` in Docker, or local SQL Server / LocalDB for native run) | `Management` database (`DroneVision`) |
| **Playwright Chromium** | Installed via `pnpm --filter frontend exec playwright install chromium` | `frontend` Vitest browser tests |
| **Wireless Interface** *(optional)* | Windows `netsh.exe` WLAN service or Linux `nmcli` (`network-manager`) with Wi-Fi hardware | `Checker` live `/networks/scan` |

### Install JS/Workspace Dependencies

From the repository root:

```powershell
pnpm install
```

Restore the local .NET EF Core tool inside `src/services/Management` (needed only if generating or running EF Core CLI migrations natively):

```powershell
dotnet tool restore --tool-manifest src/services/Management/.config/dotnet-tools.json
```

---

## 2. Environment Variables & Secrets

### Root `.env` (Shared by Docker Compose)

Copy `.env.example` to `.env` at the repository root:

```powershell
Copy-Item .env.example .env
```

`.env` is gitignored (`/.env` in `.gitignore`) and configures all Compose-included services (`frontend`, `gateway`, `management`, `checker`, `database`).

| Variable | Required | Default | Constraints & Purpose | Example |
| :--- | :--- | :--- | :--- | :--- |
| `GATEWAY_CERT_PASSWORD` | Yes | — | Password protecting `.certs/gateway.pfx` for Kestrel HTTPS on `:8443` | `Cert-StrongPassword123!` |
| `MSSQL_SA_PASSWORD` | Yes | — | SQL Server `sa` password (must satisfy SQL Server complexity rules) | `Sql-StrongPassword123!` |
| `JWT_SIGNING_KEY` | Yes | — | Base64-encoded key of **at least 64 random bytes** (512 bits) for `HS512`; validated on startup by both `Management` and `Checker` | Generated 64-byte Base64 string |
| `BOOTSTRAP_ADMIN_PASSWORD` | Yes (1st boot) | — | Initial `admin` account password; Identity requires >=12 chars, upper, lower, digit, and non-alphanumeric symbol | `Admin-StrongPass123!` |
| `GATEWAY_PORT` | No | `8443` | Host port mapped to container port `8443` in `src/gateway/compose.yaml` | `8443` |
| `JWT_ISSUER` | No | `DroneVision` | Optional override consumed by `src/services/Checker/compose.yaml` | `DroneVision` |
| `JWT_AUDIENCE` | No | `DroneVision.Client` | Optional override consumed by `src/services/Checker/compose.yaml` | `DroneVision.Client` |

Generate a cryptographically strong 64-byte Base64 `JWT_SIGNING_KEY` in PowerShell:

```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }) -as [byte[]])
# Or cryptographically secure via .NET RNG:
$bytes = New-Object byte[] 64; [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes); [Convert]::ToBase64String($bytes)
```

> [!IMPORTANT]
> Keep `MSSQL_SA_PASSWORD`, `JWT_SIGNING_KEY`, and `BOOTSTRAP_ADMIN_PASSWORD` stable across container restarts. Changing `JWT_SIGNING_KEY` invalidates existing access tokens; changing `MSSQL_SA_PASSWORD` after the `sql_data` volume is initialized will cause SQL authentication failures unless you also update the `sa` password inside SQL Server or recreate the volume.

---

## 3. Generate & Trust Gateway HTTPS Certificate

The API Gateway terminates TLS on port `8443` using `./.certs/gateway.pfx` (mounted read-only to `/certs/gateway.pfx` in `src/gateway/compose.yaml`).

```powershell
New-Item -ItemType Directory -Force .certs | Out-Null
dotnet dev-certs https -ep .certs/gateway.pfx -p 'your-GATEWAY_CERT_PASSWORD'
dotnet dev-certs https --trust
```

Replace `'your-GATEWAY_CERT_PASSWORD'` with the exact value of `GATEWAY_CERT_PASSWORD` in `.env`. For non-localhost deployments, replace `.certs/gateway.pfx` with a trusted CA-issued PFX certificate.

---

## 4. Execution Mode A: Docker Compose Development (Recommended)

Development composes [`compose.yaml`](../../compose.yaml) + [`compose.dev.yaml`](../../compose.dev.yaml):
- **Frontend:** `target: development` (`NODE_ENV=development`, runs `pnpm dev --hostname 0.0.0.0`, i.e., `next dev`).
- **Gateway, Management, Checker:** `target: development` (`ASPNETCORE_ENVIRONMENT=Development`, `DOTNET_USE_POLLING_FILE_WATCHER=1`, `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true`, runs `dotnet watch --non-interactive ... run --no-launch-profile`).
- **Database:** SQL Server 2022 with healthcheck (`sqlcmd SELECT 1`). `management` waits for `condition: service_healthy`.

### Start & Attach Compose Watch

From the repository root:

```powershell
pnpm docker:dev
```

This runs:
```powershell
docker compose -f compose.yaml -f compose.dev.yaml up --build -d && docker compose -f compose.yaml -f compose.dev.yaml watch --no-up
```

Keep the terminal open while editing so Compose Watch displays `Watch enabled` and syncs source edits:
- Changes in `./frontend` sync to `/app/frontend` (rebuilding when `frontend/package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`, or `frontend/Dockerfile` change).
- Changes in `./src/gateway` sync to `/src` (rebuilding on `Dockerfile` change).
- Changes in `./src/services/Management` sync to `/src` (rebuilding on `Dockerfile` change).
- Changes in `./src/services/Checker` sync to `/src` (rebuilding on `Dockerfile` change).

> [!NOTE]
> Unlike an older note in `DOCKER.md` referencing `frontend/pnpm-lock.yaml`, [`frontend/Dockerfile`](../../frontend/Dockerfile) builds from the **repository root** (`context: ..` in [`frontend/compose.yaml`](../../frontend/compose.yaml)) and copies the root `package.json`, `pnpm-lock.yaml`, and `pnpm-workspace.yaml` with `pnpm --filter frontend install --frozen-lockfile`. When adding frontend packages, run `pnpm --filter frontend add <pkg>` from the root (or update root `pnpm-lock.yaml`) before rebuilding.

### Build Development Images Without Starting

```powershell
pnpm docker:build:dev
```

### Re-attach Compose Watch Without Rebuilding

If you closed the Watch terminal while containers are still running:

```powershell
docker compose -f compose.yaml -f compose.dev.yaml watch --no-up
```

---

## 5. Execution Mode B: Docker Compose Production

Production uses [`compose.yaml`](../../compose.yaml) alone:
- **Frontend:** Multi-stage Next.js `output: 'standalone'` (`node frontend/server.js`, `NODE_ENV=production`, non-root user `node`).
- **Gateway, Management, Checker:** `dotnet publish -c Release` on `mcr.microsoft.com/dotnet/aspnet:10.0` (`ASPNETCORE_ENVIRONMENT=Production`). Management and Checker run as non-root `USER $APP_UID`.
- **Swagger:** Disabled in `Management`, `Checker`, and blocked at `Gateway` (`404` for `/api/*/swagger*`).

```powershell
# 1. Build production images (tagged :production)
pnpm docker:build:prod

# 2. Start production containers in detached mode (--no-build)
pnpm docker:prod
```

Development (`:development`) and production (`:production`) image tags are separate, and both modes share the same `sql_data` Docker volume so database state persists when switching modes.

### Stopping Containers

```powershell
docker compose down          # Stops and removes containers; preserves sql_data volume
docker compose down -v       # WARNING: Also deletes sql_data volume (wipes database!)
```

---

## 6. Execution Mode C: Native Local Execution (Without Full Docker Stack)

You can run individual services directly on the host for debugging or when `Checker` needs direct access to a Windows/Linux Wi-Fi adapter.

### Prerequisites for Native Execution

1. **Database:** Either run only the SQL Server container from Compose:
   ```powershell
   docker compose up -d database
   ```
   *(Note: [`src/deploy/sqlserver/compose.yaml`](../../src/deploy/sqlserver/compose.yaml) does not publish port `1433` to the host by default; to connect natively from Windows to the containerized SQL Server, either add a temporary `ports: ["1433:1433"]` override or use a local SQL Server instance matching `ConnectionStrings:DefaultConnection` in `appsettings.json`.)*
2. **Service Secrets:** Configure `Jwt:SigningKey` and `BootstrapAdmin:Password` via `dotnet user-secrets` or environment variables:

```powershell
# Management User Secrets (UserSecretsId: DroneVision-Management-API)
cd src/services/Management
dotnet user-secrets set "Jwt:SigningKey" "<64-byte-base64-key>" --project Management.API
dotnet user-secrets set "BootstrapAdmin:Password" "<strong-admin-password>" --project Management.API

# Checker User Secrets (UserSecretsId: DroneVision-Checker-API)
cd ../Checker
dotnet user-secrets set "Jwt:SigningKey" "<same-64-byte-base64-key>" --project Checker.API
dotnet user-secrets set "Management:BaseUrl" "https://localhost:7146" --project Checker.API
```

### Native Launch Ports & Commands

| Service | Command (from service folder or root filter) | URLs |
| :--- | :--- | :--- |
| **Management** | `pnpm --filter managementService dev` | `https://localhost:7146`, `http://localhost:5206` |
| **Checker** | `pnpm --filter checkerService dev` | `https://localhost:7150`, `http://localhost:5210` |
| **Frontend** | `pnpm --filter frontend dev` | `http://localhost:3000` |
| **Gateway** | `pnpm --filter gateway dev` | Default Kestrel (`http://localhost:5000`); see caveat below |

> [!WARNING]
> **Native Gateway Caveat:** `src/gateway/ocelot.frontend.json`, `ocelot.management.json`, and `ocelot.checker.json` specify Docker Compose DNS hostnames (`frontend:3000`, `management:8080`, `checker:8080`). Because `src/gateway` has no `appsettings.Development.json` or `ocelot.*.Development.json`, running the Gateway natively on the host without overriding Ocelot downstream host/ports cannot resolve `frontend`, `management`, or `checker`. Additionally, `frontend` uses relative `/api/management/*` URLs and requires HTTPS (`__Secure-` and `__Host-` cookies). For full-stack local development with browser auth, use `pnpm docker:dev`.

---

## 7. Verification Endpoints

Once running in **Docker Development** (`pnpm docker:dev`):

| Target | URL | Expected Result |
| :--- | :--- | :--- |
| **Frontend (Skyeye Login / Dashboard)** | `https://localhost:8443` | Redirects unauthenticated browsers to `/login` (`proxy.ts`) |
| **Management Swagger UI** | `https://localhost:8443/api/management/swagger/index.html` | Interactive OpenAPI v1 UI (Development only; `404` in Production) |
| **Checker Swagger UI** | `https://localhost:8443/api/checker/swagger/index.html` | Interactive OpenAPI v1 UI (Development only; `404` in Production) |
| **Checker Health Probe** | `https://localhost:8443/api/checker/health` | `200 OK` `{ "status": "Healthy" }` (anonymous, bypasses rate limit) |
| **Management CSRF Token** | `https://localhost:8443/api/management/auth/csrf` | `200 OK` `{ "requestToken": "..." }` + `Set-Cookie: __Host-DroneVision.Csrf=...` |
