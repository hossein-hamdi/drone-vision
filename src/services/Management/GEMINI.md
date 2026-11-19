# GEMINI.md — Backend Architecture, Database & Management API

## 1. Tech Stack & Workspace Overview
The backend service is **Management** (`src/services/Management`), a .NET 10 Clean Architecture API fronted by the **Ocelot API Gateway** (`src/gateway`), backed by SQL Server, and integrated into the root `pnpm` workspace.

- **Target Framework:** `.NET 10.0` (`net10.0`, C# with `ImplicitUsings` and `Nullable` enabled)
- **Architecture:** Clean Architecture (`Management.Domain`, `Management.Application`, `Management.Infrastructure`, `Management.API`)
- **Database & ORM:** Microsoft SQL Server via `Microsoft.EntityFrameworkCore` (`10.0.14`) and `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- **Authentication & Docs:** `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.14`, `Swashbuckle.AspNetCore` `10.2.3`
- **Upstream API Gateway (`src/gateway`):** ASP.NET Core + `Ocelot` `25.0.2` (routes `/api/management/{everything}` to `management:8080`, strips unknown `/api/*` paths, blocks `/swagger` outside Development, and overwrites `X-Forwarded-Proto` / `X-Forwarded-For`)
- **Package/Task Runners:** `dotnet` CLI, `dotnet-ef` (local tool in `.config/dotnet-tools.json`), and `pnpm` scripts in `src/services/Management/package.json`

### Key Scripts & Commands (`src/services/Management`)
| Command | Purpose |
| :--- | :--- |
| `pnpm dev` (`dotnet watch run --project Management.API --launch-profile https`) | Run Management API with hot reload (`https://localhost:7146`, `http://localhost:5206`) |
| `pnpm build` (`dotnet build Management.API/Management.API.csproj`) | Build Management solution |
| `pnpm start` (`dotnet run --project Management.API --launch-profile https`) | Run Management API without watch |
| `pnpm format` (`dotnet format whitespace . --folder`) | Format C# whitespace |
| `pnpm format:check` (`dotnet format whitespace . --folder --verify-no-changes`) | Verify C# whitespace formatting |
| `dotnet ef migrations add <Name> --project Management.Infrastructure --startup-project Management.API` | Generate a new EF Core migration |
| `pnpm docker:dev` / `pnpm docker:prod` (repo root) | Run full stack (SQL Server, Management, Frontend, Ocelot Gateway) via Docker Compose |

---

## 2. Directory Structure
```text
src/services/Management/
├── .config/
│   └── dotnet-tools.json                    # Local dotnet-ef CLI tool manifest
├── Management.Domain/                       # Entities & domain rules (zero project dependencies)
├── Management.Application/                  # Use-case abstractions, DTOs, permissions, ApiException
│   ├── Abstractions/
│   │   ├── Authentication/IAuthenticationService.cs
│   │   └── IManagementServices.cs           # IUserService, IRoleService, IPermissionService, IProfileImageService
│   ├── Authorization/Permissions.cs         # Canonical permission catalog & ClaimType
│   ├── Common/ApiException.cs               # Typed HTTP domain exceptions (400, 401, 403, 404, 409)
│   └── DTOs/                                # Requests, UserDto, AuthenticationResponse, ProfileImageData
├── Management.Infrastructure/               # EF Core, Identity, JWT, SQL Server adapters
│   ├── Authentication/                      # AuthService, JwtTokenService, JwtOptions, IdentityTokenProviders
│   ├── Authorization/PermissionService.cs     # Queries role claims for user permissions
│   ├── Identity/                            # ApplicationUser, ApplicationRole, ApplicationUserToken, etc.
│   ├── Persistence/
│   │   ├── ManagementDbContext.cs           # IdentityDbContext + ProfileImages
│   │   ├── Migrations/                      # Sequential EF Core SQL Server migrations
│   │   ├── Seed/IdentityBootstrap.cs        # Startup migration runner + Admin role/user bootstrap
│   │   └── Entities/ProfileImage.cs         # Binary profile image entity with RowVersion
│   ├── Services/                            # UserService, RoleService, ProfileImageService, ProfileImageStore
│   └── DependencyInjection.cs               # Registers EF, IdentityCore, JwtOptions, services
└── Management.API/                          # Composition root, controllers, auth handlers, Swagger
    ├── Authentication/BrowserSessionHandler.cs # Cookie auth scheme (__Secure-DroneVision.Refresh)
    ├── Authorization/PermissionAuthorizationHandler.cs # Dynamic per-request permission check
    ├── Controllers/                         # ApiController base + Auth, AuthMe, Users, Roles, Permissions, Profile, ProfileImages
    ├── ExceptionHandling/                   # Ordered IExceptionHandler chain + RFC 9457 ProblemDetails
    ├── OpenApi/SecurityOperationFilter.cs
    └── Program.cs                           # Middleware pipeline, Antiforgery, Dual Auth, RateLimiting, Swagger
```

---

## 3. Core Files & Responsibilities
- `Management.API/Program.cs`: Configures `UnmappedMemberHandling.Disallow` on JSON serialization (rejects unknown JSON fields), registers RFC 9457 `ProblemDetails` + 4 ordered `IExceptionHandler`s, configures `AddAntiforgery` (`__Host-DroneVision.Csrf` / `X-CSRF-TOKEN`), configures `"BrowserOrBearer"` policy scheme, registers `Permissions.All` authorization policies, configures `"credentials"` IP rate limiter (20 req/min default), and runs `IdentityBootstrap.InitializeAsync()` on startup.
- `Management.API/Authentication/BrowserSessionHandler.cs`: Validates `__Secure-DroneVision.Refresh` (must be 86-char Base64Url), hashes with SHA-256, joins `Refresh` token -> `Session` token -> `AspNetUsers`, verifies `IsBrowserSession == true`, non-revoked, non-expired, and not locked out. Deletes cookie on failure.
- `Management.Infrastructure/Authentication/AuthService.cs`: Implements `RegisterAsync`, `LoginAsync`, `RefreshAsync`, and `LogoutAsync` with transactional `UPDLOCK, ROWLOCK` session locking and replay detection.
- `Management.Infrastructure/Authentication/JwtTokenService.cs`: Issues `HS512` JWTs containing only `sub`, `sid`, `jti`, `iss`, `aud`, and timestamps (no roles/permissions embedded). Generates 512-bit (`64` random bytes) Base64Url refresh tokens and SHA-256 hex hashes.
- `Management.Infrastructure/Persistence/ManagementDbContext.cs`: Extends `IdentityDbContext` using integer keys and custom `ApplicationUserToken` (storing sessions & refresh hashes) plus `ProfileImages`.
- `Management.Infrastructure/DependencyInjection.cs`: Registers SQL Server `ManagementDbContext`, `IdentityCore` password/lockout rules, `JwtOptions` startup validation, and scoped application services.

---

## 4. Database Schema & Migration Workflow

### Database & Migration Rules
- **Engine:** SQL Server via EF Core (`UseSqlServer(configuration.GetConnectionString("DefaultConnection"))`).
- **Startup Execution:** `IdentityBootstrap.InitializeAsync()` runs on API startup, applying pending EF Core migrations and ensuring the bootstrap Admin role retains all permissions in `Permissions.All`.
- **Creating Migrations:** Never edit applied migrations in `Management.Infrastructure/Persistence/Migrations/`. Always generate a new migration from `src/services/Management`:
  ```powershell
  dotnet ef migrations add MigrationName --project Management.Infrastructure --startup-project Management.API
  ```

### Schema Invariants
- **Identity Tables Only for Auth:** Users, roles, and memberships use `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, and `AspNetRoleClaims` (`permission` claims). Sessions (`DroneVision.Session`) and refresh token hashes (`DroneVision.Refresh`) share `AspNetUserTokens` (`ApplicationUserToken`), with a unique filtered index `IX_AspNetUserTokens_RefreshHash` on `Name` where `[LoginProvider] = 'DroneVision.Refresh'`. **Do not** add separate session, refresh-token, or permission tables.
- **`ProfileImages` Table:** Stored in SQL Server (`varbinary(max) Content`, `ContentType` max 32, unique `Url` max 300, `RowVersion` concurrency token, unique FK `UserId` -> `AspNetUsers(Id)` with `DeleteBehavior.Cascade`). Images are never written to disk.

---

## 5. Code Snapshots & API Conventions

### Controller Base, Routing & Permission Policies
All controllers inherit `ApiController` (which applies `[ApiController]`, `[Authorize]`, `[Produces("application/json")]`, and standard `ProblemDetails` response types) and enforce positive integer route constraints (`{id:int:min(1)}`).
```csharp
// CORRECT — Controller & Policy Pattern (from UsersController.cs)
[Route("users")]
public sealed class UsersController(IUserService users) : ApiController
{
    [HttpGet("{id:int:min(1)}"), Authorize(Policy = Permissions.UsersView)]
    [ProducesResponseType<UserDto>(200)]
    public async Task<ActionResult<UserDto>> Get(int id, CancellationToken ct) =>
        Ok(await users.GetAsync(id, ct));

    [HttpPost, Authorize(Policy = Permissions.UsersCreate)]
    [ProducesResponseType<UserDto>(201)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }
}

// WRONG — Missing [Authorize(Policy = ...)] or missing :int:min(1) route constraint
[HttpGet("{id}")]
public async Task<ActionResult<UserDto>> Get(int id, CancellationToken ct) => ...
```

### Error Handling & RFC 9457 `ProblemDetails`
Never catch and return ad-hoc anonymous error objects. Throw `ApiException` from Application/Infrastructure layers; the ordered `IExceptionHandler` pipeline (`ValidationExceptionHandler` -> `ApiExceptionHandler` -> `DatabaseConflictExceptionHandler` -> `GlobalExceptionHandler`) formats RFC 9457 `ProblemDetails` with `traceId`.
```csharp
// CORRECT — Ordered IExceptionHandler (from ExceptionHandlers.cs)
if (exception is not ApiException { StatusCode: 400 } error) return false;
await ApiProblems.WriteAsync(context, new ValidationProblemDetails(error.Errors ?? new Dictionary<string, string[]>())
{
    Status = 400,
    Title = error.Title,
    Detail = error.Message
});
return true;
```

### Transactional Refresh Token Rotation (`UPDLOCK, ROWLOCK`)
All refresh rotations and logouts acquire an exclusive row update lock on the parent `DroneVision.Session` row in `AspNetUserTokens` to prevent concurrent rotation races and detect token replay.
```csharp
// CORRECT — Cross-instance safe session lock (from AuthService.cs)
await using var transaction = await db.Database.BeginTransactionAsync(ct);
var session = await db.UserTokens.FromSqlInterpolated(
    $"SELECT * FROM [AspNetUserTokens] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {sessionId} AND [LoginProvider] = {IdentityTokenProviders.Session}")
    .SingleOrDefaultAsync(ct) ?? throw ApiException.Unauthorized();

if (previous.RevokedAtUtc is not null)
{
    await RevokeSessionAsync(session.Id, now, ct);
    await transaction.CommitAsync(ct); // Persist replay revocation before returning 401
    throw ApiException.Unauthorized();
}
```

---

## 6. Authentication & Authorization Management (Backend)
- **Dual Transport (`BrowserOrBearer`):**
  - Requests with `Authorization: Bearer <jwt>` forward to `JwtBearerDefaults.AuthenticationScheme` (`HmacSha512`, `ClockSkew = TimeSpan.Zero`, `OnTokenValidated` verifies the `sid` session in `AspNetUserTokens` is active and the user is not locked out).
  - All other requests forward to `BrowserSessionHandler` (`__Secure-DroneVision.Refresh` cookie).
- **CSRF Enforcement:** Middleware in `Program.cs` validates `X-CSRF-TOKEN` + `__Host-DroneVision.Csrf` on all non-GET/HEAD/OPTIONS requests authenticated via `BrowserSession` (excluding `[AllowAnonymous]`, where `AuthController.ValidateTransportAsync()` enforces CSRF for browser transport and rejects `Origin`/`Sec-Fetch-Site`/cookie when `X-Auth-Transport: native` is set).
- **Transport Isolation:** Browser and native sessions cannot be mixed (`session.IsBrowserSession != browser` returns `401`).
- **Dynamic Permission Authorization:** `PermissionAuthorizationHandler` evaluates `IPermissionService.HasAsync(userId, requirement.Permission)` against current `AspNetUserRoles` + `AspNetRoleClaims` on every request—role/permission edits take effect immediately without re-login.
- **Lockout & Rate Limiting:** Identity enforces 5 failed logins -> 15-minute lockout. `POST /auth/register` and `POST /auth/login` enforce `[EnableRateLimiting("credentials")]` (20 req/min per IP by default, returning `429`).

---

## 7. Core Business Logic & Domain Invariants
- **Gateway Path Base:** When deployed behind the Ocelot API Gateway (`src/gateway`, routing `/api/management/*`), `Gateway:PathBase` configures `app.UsePathBase(...)` so generated URLs (e.g., `Created(...)`, Swagger servers) resolve under `/api/management`.
- **Strict JSON Deserialization:** `UnmappedMemberHandling.Disallow` is enabled globally—unknown properties (e.g., attempting to pass `roles` to `POST /auth/register`) fail with `400`.
- **Stable Permission Catalog:** `Permissions.All` defines the canonical permission list. `GET /permissions` assigns numeric IDs by catalog index—**always append new permissions to the end** without reordering existing entries.
- **Password & Key Strength:** Identity requires 12+ chars with digit, lower, upper, and non-alphanumeric. `Jwt:SigningKey` is validated on startup (`JwtOptions.HasStrongKey`) to be Base64 of at least 64 random bytes.
- **Profile Image Constraints:** Accepts JPEG, PNG, or WebP up to 5 MiB (`ProfileImageService`), enforces one image per user, and serves bytes anonymously via `GET /uploads/profiles/{fileName}`.
