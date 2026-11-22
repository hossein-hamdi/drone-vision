# Complete CLI Commands Reference

A single-page cheatsheet for every build, run, Docker Compose, database migration, formatting, linting, typechecking, testing, and git-hook command in the repository.

---

## 1. Root Workspace & Docker Compose (`package.json`)

Run from repository root (`D:\projects\drone-analyze`):

| Command | Underlying Action | Purpose |
| :--- | :--- | :--- |
| `pnpm install` | Installs JS/workspace dependencies & runs `prepare` (`husky`) | Initial workspace setup |
| `pnpm docker:build:dev` | `docker compose -f compose.yaml -f compose.dev.yaml build` | Build `:development` images without starting containers |
| `pnpm docker:dev` | `docker compose -f compose.yaml -f compose.dev.yaml up --build -d && docker compose -f compose.yaml -f compose.dev.yaml watch --no-up` | Build dev images, start stack, attach hot-reload file watcher |
| `pnpm docker:build:prod` | `docker compose build` | Build `:production` images without starting containers |
| `pnpm docker:prod` | `docker compose up --no-build -d` | Start pre-built `:production` containers in background |

### Direct Docker Compose Operational Commands

```powershell
# Re-attach Compose Watch without rebuilding
docker compose -f compose.yaml -f compose.dev.yaml watch --no-up

# View container status & healthchecks
docker compose ps

# Follow logs across all services (or a single service)
docker compose logs -f
docker compose logs -f gateway management checker frontend database

# Restart a single service
docker compose restart management

# Start only SQL Server
docker compose up -d database

# Stop stack (preserves SQL Server volume `sql_data`)
docker compose down

# Stop stack AND permanently delete `sql_data` volume
docker compose down -v
```

---

## 2. API Gateway (`src/gateway`)

Workspace package name: `gateway` (`src/gateway/package.json`).

| Command (from `src/gateway`) | Filtered from Root | Underlying Action |
| :--- | :--- | :--- |
| `pnpm dev` | `pnpm --filter gateway dev` | `dotnet watch run --project Gateway.csproj` |
| `pnpm build` | `pnpm --filter gateway build` | `dotnet build Gateway.csproj` |
| `pnpm start` | `pnpm --filter gateway start` | `dotnet run --project Gateway.csproj` |
| `pnpm format` | `pnpm --filter gateway format` | `dotnet format whitespace . --folder` |
| `pnpm format:check` | `pnpm --filter gateway format:check` | `dotnet format whitespace . --folder --verify-no-changes` |

### TLS Dev Certificate Commands

```powershell
New-Item -ItemType Directory -Force .certs | Out-Null
dotnet dev-certs https -ep .certs/gateway.pfx -p '<GATEWAY_CERT_PASSWORD>'
dotnet dev-certs https --trust
```

---

## 3. Management Service (`src/services/Management`)

Workspace package name: `managementService` (`src/services/Management/package.json`).

| Command (from `src/services/Management`) | Filtered from Root | Underlying Action |
| :--- | :--- | :--- |
| `pnpm dev` | `pnpm --filter managementService dev` | `dotnet watch run --project Management.API --launch-profile https` (`https://localhost:7146`, `http://localhost:5206`) |
| `pnpm build` | `pnpm --filter managementService build` | `dotnet build Management.API/Management.API.csproj` |
| `pnpm start` | `pnpm --filter managementService start` | `dotnet run --project Management.API --launch-profile https` |
| `pnpm format` | `pnpm --filter managementService format` | `dotnet format whitespace . --folder` |
| `pnpm format:check` | `pnpm --filter managementService format:check` | `dotnet format whitespace . --folder --verify-no-changes` |

### Entity Framework Core Migrations (`src/services/Management`)

`dotnet-ef` `10.0.14` is pinned in `src/services/Management/.config/dotnet-tools.json`. Run from `src/services/Management`:

```powershell
# Restore local dotnet-ef tool
dotnet tool restore

# Add a new migration (never edit already-applied migrations)
dotnet ef migrations add <MigrationName> --project Management.Infrastructure --startup-project Management.API

# List migrations and check pending/applied status
dotnet ef migrations list --project Management.Infrastructure --startup-project Management.API

# Apply pending migrations manually (also runs automatically on app startup via IdentityBootstrap)
dotnet ef database update --project Management.Infrastructure --startup-project Management.API

# Generate idempotent SQL deployment script
dotnet ef migrations script --idempotent --project Management.Infrastructure --startup-project Management.API -o migrations.sql

# Remove the last unapplied migration
dotnet ef migrations remove --project Management.Infrastructure --startup-project Management.API
```

---

## 4. Checker Service (`src/services/Checker`)

Workspace package name: `checkerService` (`src/services/Checker/package.json`).

| Command (from `src/services/Checker`) | Filtered from Root | Underlying Action |
| :--- | :--- | :--- |
| `pnpm dev` | `pnpm --filter checkerService dev` | `dotnet watch run --project Checker.API --launch-profile https` (`https://localhost:7150`, `http://localhost:5210`) |
| `pnpm build` | `pnpm --filter checkerService build` | `dotnet build Checker.API/Checker.API.csproj` |
| `pnpm start` | `pnpm --filter checkerService start` | `dotnet run --project Checker.API --launch-profile https` |
| `pnpm test` | `pnpm --filter checkerService test` | `dotnet test Checker.slnx` |
| `pnpm format` | `pnpm --filter checkerService format` | `dotnet format whitespace . --folder` |
| `pnpm format:check` | `pnpm --filter checkerService format:check` | `dotnet format whitespace . --folder --verify-no-changes` |

### Targeted xUnit Test Commands (`src/services/Checker`)

```powershell
# Run full solution test suite
dotnet test Checker.slnx

# Run only Unit tests
dotnet test Checker.slnx --filter "FullyQualifiedName~Checker.Tests.Unit"

# Run only Integration tests
dotnet test Checker.slnx --filter "FullyQualifiedName~Checker.Tests.Integration"

# Run with verbose console output
dotnet test Checker.slnx --logger "console;verbosity=detailed"
```

---

## 5. Frontend Application (`frontend`)

Workspace package name: `frontend` (`frontend/package.json`).

| Command (from `frontend`) | Filtered from Root | Underlying Action |
| :--- | :--- | :--- |
| `pnpm dev` | `pnpm --filter frontend dev` | `next dev` (port `3000`) |
| `pnpm build` | `pnpm --filter frontend build` | `next build` (standalone production output) |
| `pnpm start` | `pnpm --filter frontend start` | `next start` |
| `pnpm preview` | `pnpm --filter frontend preview` | `pnpm build && pnpm start` |
| `pnpm typecheck` | `pnpm --filter frontend typecheck` | `tsc --noEmit` |
| `pnpm lint` | `pnpm --filter frontend lint` | `eslint . --ext .js,.jsx,.ts,.tsx,.mjs` |
| `pnpm lint:fix` | `pnpm --filter frontend lint:fix` | `eslint . --ext .js,.jsx,.ts,.tsx,.mjs --fix` |
| `pnpm format` | `pnpm --filter frontend format` | `prettier --write "**/*.{js,jsx,ts,tsx,mjs,json,css,md}"` |
| `pnpm format:check` | `pnpm --filter frontend format:check` | `prettier --check "**/*.{js,jsx,ts,tsx,mjs,json,css,md}"` |
| `pnpm check` | `pnpm --filter frontend check` | `pnpm lint && pnpm format:check && pnpm typecheck` |
| `pnpm test` | `pnpm --filter frontend test` | `vitest` (watch mode across `unit` and `browser` projects) |

### Non-Interactive & Project-Specific Vitest Commands

```powershell
# First-time browser test dependency (installs Playwright Chromium binary)
pnpm --filter frontend exec playwright install chromium

# Run all Vitest projects once (unit + browser)
pnpm --filter frontend exec vitest run

# Run only Node unit tests (*.test.ts)
pnpm --filter frontend exec vitest run --project unit

# Run only Chromium browser component tests (*.test.tsx)
pnpm --filter frontend exec vitest run --project browser
```

---

## 6. Git Hooks & Quality Gates

Configured at repository root via Husky (`.husky/`):

| Hook | Script | Enforced Rules |
| :--- | :--- | :--- |
| **Pre-commit** | `pnpm lint-staged` | - Root `.lintstagedrc.mjs`: checks `*.{json,jsonc,md,mdx,yml,yaml,css,scss}` (`ls`)<br/>- `frontend/.lintstagedrc.mjs`: runs `eslint --fix --max-warnings=-1`, `prettier --write`, `prettier --write` on json/css/md, and `pnpm typecheck` on `*.{ts,tsx}`<br/>- `src/gateway/.lintstagedrc.mjs`, `src/services/Management/.lintstagedrc.mjs`, `src/services/Checker/.lintstagedrc.mjs`: runs `pnpm format --include` (`dotnet format whitespace`) on `*.cs` |
| **Commit-msg** | `pnpm commitlint --edit $1` | Enforces `@commitlint/config-conventional` (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, etc.) |
