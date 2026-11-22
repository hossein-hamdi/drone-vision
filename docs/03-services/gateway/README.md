# API Gateway (`src/gateway`)

The **API Gateway** is an ASP.NET Core `.NET 10.0` reverse proxy powered by **Ocelot** (`25.0.2`). It serves as the single public HTTPS ingress boundary (`:8443`) for the **Skyeye** Next.js frontend and all backend microservices (`Management` and `Checker`), enforcing `/api/*` route allowlisting, production Swagger lockout, and forwarding header sanitization.

---

## 1. Tech Stack & Dependencies

| Component | Technology / Version | Notes |
| :--- | :--- | :--- |
| **Runtime** | `.NET 10.0` (`net10.0`) | C# with `ImplicitUsings` and `Nullable` enabled |
| **Reverse Proxy** | `Ocelot` `25.0.2` (`Gateway.csproj`) | Merged modular route configs (`ocelot.*.json`) |
| **TLS Termination** | Kestrel HTTPS (`gateway.pfx`) | Mounted read-only at `/certs/gateway.pfx` |
| **Workspace** | `pnpm` workspace member `src/gateway` | Package name: `gateway` (`pnpm@12.16.1`) |
| **Container Base** | `sdk:10.0` (dev/build) → `aspnet:10.0` (runtime) | Multi-stage Dockerfile with `dotnet watch` in dev |

---

## 2. Directory & File Structure

```text
src/gateway/
├── .dockerignore                  # Excludes bin, obj, .env*, .certs from Docker context
├── .lintstagedrc.mjs              # Runs `dotnet format whitespace` on staged *.cs files
├── compose.yaml                   # Production service definition (HTTPS :8443, PFX mount, depends_on)
├── Dockerfile                     # Multi-stage image: development (dotnet watch), build, runtime
├── Gateway.csproj                 # .NET 10 Web SDK + Ocelot 25.0.2
├── GEMINI.md                      # Gateway architectural invariants guide
├── ocelot.checker.json            # Priority 10 routes: /api/checker/{everything} -> checker:8080
├── ocelot.frontend.json           # Priority 0 catch-all routes: / and /{everything} -> frontend:3000
├── ocelot.management.json         # Priority 10 routes: /api/management/{everything} -> management:8080
├── package.json                   # pnpm scripts: dev, build, start, format, format:check
└── Program.cs                     # Ocelot JSON merge, boundary guard, header sanitization, UseOcelot()
```

---

## 3. Ports & Networking

| Mode | Public Port | Internal Downstream Targets | Notes |
| :--- | :--- | :--- | :--- |
| **Docker (Dev & Prod)** | `${GATEWAY_PORT:-8443}:8443` (HTTPS) | `frontend:3000`, `management:8080`, `checker:8080` (HTTP over Docker network) | Only service in `compose.yaml` publishing a host port |
| **Native (`dotnet run`)** | Default Kestrel (`http://localhost:5000` unless `--urls`/`ASPNETCORE_URLS` set) | Expects Docker hostnames (`frontend`, `management`, `checker`) | No `launchSettings.json`; see native caveat below |

---

## 4. Environment Variables & Configuration Keys

Defined in `src/gateway/compose.yaml`, `src/gateway/Dockerfile`, and root `compose.dev.yaml`:

| Key | Source | Default / Value | Purpose |
| :--- | :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Compose (`Production` in `compose.yaml`; `Development` in `compose.dev.yaml`) | `Production` (compose) | Controls whether `/api/<service>/swagger*` routes are allowed (`Development`) or blocked with `404` (`Production`) |
| `ASPNETCORE_URLS` | `src/gateway/compose.yaml` | `https://+:8443` | Binds Kestrel HTTPS listener on port `8443` |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | `src/gateway/compose.yaml` | `/certs/gateway.pfx` | Container path to mounted TLS certificate |
| `ASPNETCORE_Kestrel__Certificates__Default__Password` | `src/gateway/compose.yaml` (`${GATEWAY_CERT_PASSWORD}`) | *Required* | Password for `.certs/gateway.pfx` from root `.env` |
| `GATEWAY_PORT` | `src/gateway/compose.yaml` | `8443` | Host port mapped to container `8443` |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `src/gateway/Dockerfile` (dev stage) | `1` | Reliable file watching inside Linux containers |
| `DOTNET_WATCH_RESTART_ON_RUDE_EDIT` | `src/gateway/Dockerfile` (dev stage) | `true` | Auto-restarts `dotnet watch` on non-hot-reloadable edits |

---

## 5. Run, Build, Docker & Format Commands

### From `src/gateway` (or via `pnpm --filter gateway <script>` from root)

```powershell
# Build the .NET project
pnpm --filter gateway build

# Run with dotnet watch hot reload
pnpm --filter gateway dev

# Run without watch
pnpm --filter gateway start

# Format C# whitespace / verify formatting
pnpm --filter gateway format
pnpm --filter gateway format:check
```

### Docker Compose (from Repository Root)

```powershell
# Generate required dev TLS certificate before first Docker start
New-Item -ItemType Directory -Force .certs | Out-Null
dotnet dev-certs https -ep .certs/gateway.pfx -p '<GATEWAY_CERT_PASSWORD>'
dotnet dev-certs https --trust

# Development (with Compose Watch hot reload)
pnpm docker:dev

# Production
pnpm docker:build:prod
pnpm docker:prod

# Tail Gateway logs
docker compose logs -f gateway
```

> [!NOTE]
> `ocelot.*.json` files hardcode Docker service names (`frontend`, `management`, `checker`). Running the Gateway natively on the host requires Docker DNS or overriding `ocelot.*.json` downstream hosts/ports to `localhost`.
