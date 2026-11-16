# Run the whole project in Docker

From the repository root, copy `.env.example` to `.env`. Set unique SQL Server,
bootstrap admin and certificate passwords, and a JWT signing key (Base64 of at
least 64 random bytes). Admin passwords require 12 characters, upper and lower
case letters, a digit and a symbol. Keep these values stable across restarts.

```powershell
Copy-Item .env.example .env
New-Item -ItemType Directory -Force .certs | Out-Null
dotnet dev-certs https -ep .certs/gateway.pfx -p 'your-GATEWAY_CERT_PASSWORD'
dotnet dev-certs https --trust
```

Choose a mode from the repository root:

| Command | Behavior |
| --- | --- |
| `pnpm docker:build:dev` | Build development images only |
| `pnpm docker:dev` | Build current development images, then run with hot reload |
| `pnpm docker:build:prod` | Build production images only |
| `pnpm docker:prod` | Run existing production images in the background |

`pnpm docker:build` also builds production images. Building images does not start
containers. The development run command builds before starting containers so
dependency changes made while Watch was stopped are included. Production starts
with `--no-build` and requires production images to exist. Development then
attaches Compose Watch with `--no-up`, which skips its initial build/start step.
Development and production images have separate tags, so building one mode does
not overwrite the other. Rebuild production images when shipping source changes.

Development uses `compose.yaml` plus `compose.dev.yaml`. The frontend runs
`next dev` with `NODE_ENV=development`; the .NET services run `dotnet watch`
in Debug with `ASPNETCORE_ENVIRONMENT=Development`. Compose Watch synchronizes
source changes and rebuilds the frontend when dependency files change.

Production uses `compose.yaml` alone. The frontend serves its optimized standalone
build with `NODE_ENV=production`; .NET services run Release publishes in runtime
images with `ASPNETCORE_ENVIRONMENT=Production`. Swagger is disabled.

Equivalent Docker commands:

```powershell
# Development: build current images, then run with hot reload
docker compose -f compose.yaml -f compose.dev.yaml up --build -d
docker compose -f compose.yaml -f compose.dev.yaml watch --no-up

# Production: build once, then run in the background
docker compose build
docker compose up --no-build -d
```

Both modes use the same Compose project and SQL data volume. They run one mode at
a time and preserve database data when switching. Compose Watch requires a recent
Docker Compose release supporting `develop.watch` and `initial_sync`.
Source edits hot reload without rebuilding images. Dependency-file and Dockerfile
edits trigger the configured Watch rebuilds. Stopping Watch leaves the containers
running; use `docker compose down` to stop them.
Keep the `pnpm docker:dev` terminal open while editing. It should show `Watch
enabled`, followed by sync messages when files change. Starting containers alone
does not synchronize source files. If Watch stops, reattach without rebuilding:

```powershell
docker compose -f compose.yaml -f compose.dev.yaml watch --no-up
```

The frontend Docker build uses its standalone `frontend/pnpm-lock.yaml`. After
adding dependencies through the root workspace, refresh that lockfile before
rebuilding the frontend:

```powershell
pnpm --dir frontend install --lockfile-only --ignore-workspace --ignore-scripts
```

Open https://localhost:8443 for the frontend. In development, open
https://localhost:8443/api/management/swagger/index.html for Development Swagger.
Both use Ocelot on the same host and port. Management routes start with `/api/management`, including
`/api/management/auth`, `/api/management/users`, `/api/management/roles`, `/api/management/permissions`, `/api/management/profile`, `/api/management/profile-image`, and
`/api/management/uploads`, are forwarded to Management. Use relative URLs in browser API calls.
The frontend login is currently a scaffold; Docker does not implement sign-in.

The root `compose.yaml` includes each project's deployment settings:

- `frontend/compose.yaml`
- `src/gateway/compose.yaml`
- `src/services/Management/compose.yaml`
- `src/deploy/sqlserver/compose.yaml`

Run Compose from the repository root so the shared `.env` configures all included
services. Each project owns its build context and service settings. Only the gateway
publishes a host port in both modes. Access the app through https://localhost:8443;
frontend port 3000 is internal to the Docker network.
SQL data persists in the existing volume.
Management waits for a successful database query before applying migrations
and creating the initial admin account. Internal HTTP requests carry the HTTPS
scheme from the gateway so secure antiforgery cookies work.

Supply a trusted certificate for deployments beyond localhost. `GATEWAY_PORT`
controls the gateway's host port (default `8443`). Environment modes are selected
by the Compose configuration; `APP_ENVIRONMENT` is no longer used.

Use `docker compose logs -f` for logs and `docker compose down` to stop.
Avoid `docker compose down -v` unless you intend to delete the database.

Additional API services use /api/<service>/* with a separate src/gateway/ocelot.<service>.json file and a Compose service. Use the Management route file as the template for each new service. Unknown /api/* services return 404.
