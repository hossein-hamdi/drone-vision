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
docker compose up --build -d
```

Open https://localhost:8443 for the frontend and
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
publishes a port. SQL data persists in the existing volume.
Management waits for a successful database query before applying migrations
and creating the initial admin account. Internal HTTP requests carry the HTTPS
scheme from the gateway so secure antiforgery cookies work.

Set `APP_ENVIRONMENT=Production` to disable Swagger. Supply a trusted certificate
for deployments beyond localhost. `GATEWAY_PORT` controls the single host port.
The frontend image always runs an optimized production build.

Use `docker compose logs -f` for logs and `docker compose down` to stop.
Avoid `docker compose down -v` unless you intend to delete the database.

Additional API services use /api/<service>/* with a separate src/gateway/ocelot.<service>.json file and a Compose service. Use the Management route file as the template for each new service. Unknown /api/* services return 404.
