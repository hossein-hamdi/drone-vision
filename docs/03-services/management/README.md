# Management Service (`src/services/Management`)

The **Management** service is a `.NET 10.0` Clean Architecture microservice backed by **SQL Server** and **ASP.NET Core Identity**. It is the platform's central identity, authentication, session, RBAC role/permission, user, and profile management authority.

---

## 1. Tech Stack & Dependencies

| Component | Technology / Version | Notes |
| :--- | :--- | :--- |
| **Target Framework** | `.NET 10.0` (`net10.0`) | C# with `ImplicitUsings` and `Nullable` enabled |
| **Architecture** | Clean Architecture (4 projects) | `Management.Domain`, `Management.Application`, `Management.Infrastructure`, `Management.API` |
| **ORM & Database** | `Microsoft.EntityFrameworkCore.SqlServer` `10.0.14`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` `10.0.14` | SQL Server (`DroneVision` database) |
| **Authentication** | `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.14`, `System.IdentityModel.Tokens.Jwt` `8.14.0` | Dual `"BrowserOrBearer"` scheme (`BrowserSession` cookie + `HS512` JWT Bearer) |
| **API Docs** | `Swashbuckle.AspNetCore` `10.2.3` | Enabled in `Development` only, with custom `BrowserSession`/`Bearer` Swagger interceptors |
| **Workspace** | `pnpm` workspace member `src/services/Management` | Package name: `managementService` |
| **CLI Tooling** | `dotnet-ef` `10.0.14` (`.config/dotnet-tools.json`) | Design-time EF Core migrations |

---

## 2. Directory & File Structure

```text
src/services/Management/
├── .config/
│   └── dotnet-tools.json                                # Local dotnet-ef 10.0.14 manifest
├── .dockerignore                                        # Excludes bin, obj, .git, .env*, *.user
├── .lintstagedrc.mjs                                    # Runs `dotnet format whitespace` on staged *.cs
├── AGENTS.md                                            # Backend architecture & security contract
├── GEMINI.md                                            # Developer architecture guide
├── Dockerfile                                           # Multi-stage: development (dotnet watch), build, runtime ($APP_UID)
├── compose.yaml                                         # Production container config (:8080, depends_on database healthy)
├── package.json                                         # pnpm scripts: dev, build, start, format, format:check
├── Management.Domain/                                   # Domain project (zero project dependencies; currently placeholder)
│   └── Management.Domain.csproj
├── Management.Application/                              # Abstractions, DTOs, Permissions catalog, ApiException
│   ├── Management.Application.csproj
│   ├── Abstractions/
│   │   ├── Authentication/IAuthenticationService.cs     # RegisterAsync, LoginAsync, RefreshAsync, LogoutAsync
│   │   └── IManagementServices.cs                       # IUserService, IRoleService, IPermissionService, IProfileImageService
│   ├── Authorization/Permissions.cs                     # Canonical Permissions.All catalog & ClaimType ("permission")
│   ├── Common/ApiException.cs                           # Typed HTTP domain exceptions (400, 401, 404, 409)
│   └── DTOs/
│       ├── AuthenticatedUserDto.cs
│       ├── AuthenticationResponse.cs                    # AuthenticationResponse & TokenPair
│       ├── ProfileImageData.cs
│       ├── Requests.cs                                  # Register, Login, CreateUser, UpdateUser, UpdateProfile, Role, Filters, Refresh
│       └── UserDto.cs                                   # UserDto, ProfileDto, RoleDto, PermissionDto, PagedResult<T>
├── Management.Infrastructure/                           # EF Core, Identity, SQL Server, JWT, Image Store, Bootstrap
│   ├── Management.Infrastructure.csproj
│   ├── DependencyInjection.cs                           # AddInfrastructure(): DbContext, IdentityCore, JwtOptions validation, DI
│   ├── Authentication/
│   │   ├── AuthService.cs                               # Transactional UPDLOCK session/refresh rotation & replay detection
│   │   ├── IdentityTokenProviders.cs                    # "DroneVision.Session" & "DroneVision.Refresh"
│   │   ├── JwtOptions.cs                                # Jwt config model & HasStrongKey (>= 64 Base64 bytes)
│   │   └── JwtTokenService.cs                           # HS512 JWT issuance & 512-bit Base64Url / SHA-256 helpers
│   ├── Authorization/PermissionService.cs               # Queries AspNetUserRoles + AspNetRoleClaims
│   ├── Identity/
│   │   ├── ApplicationRole.cs                           # Role, UserRole, UserLogin, UserClaim, RoleClaim, UserToken entities
│   │   ├── ApplicationUser.cs                           # IdentityUser<int> + FullName, ProfileImage, timestamps
│   │   ├── ApplicationUserStore.cs                      # Custom key lookup overrides preserving integer PKs
│   │   └── IdentityErrors.cs                            # Maps IdentityResult failures to 409 Conflict / 400 Validation
│   ├── Persistence/
│   │   ├── DesignTimeDbContextFactory.cs                # EF CLI design-time factory
│   │   ├── Entities/ProfileImage.cs                     # Binary varbinary(max) image entity + RowVersion
│   │   ├── ITimestampedEntity.cs                        # CreatedAt / UpdatedAt contract
│   │   ├── ManagementDbContext.cs                       # IdentityDbContext + ProfileImages + UTC & timestamp SaveChanges
│   │   ├── ManagementDbContextFactory.cs                # Secondary design-time factory
│   │   ├── Migrations/                                  # 5 sequential EF Core migrations + snapshot
│   │   └── Seed/IdentityBootstrap.cs                    # sp_getapplock startup migrations + Admin role/user seeding
│   └── Services/
│       ├── ProfileImageService.cs                       # Get, Upload (replace), Delete profile images in DB
│       ├── ProfileImageStore.cs                         # 5 MiB stream cap & JPEG/PNG/WebP signature validation
│       ├── RoleService.cs                               # Role CRUD, bootstrap Admin protection, permission catalog
│       ├── TimestampFilterExtensions.cs                 # WhereCreatedBetween UTC date range filter
│       └── UserService.cs                               # User CRUD, filtering, pagination, role sync, profile
└── Management.API/                                      # HTTP host, controllers, auth handlers, Swagger, exception handlers
    ├── Management.API.csproj
    ├── Program.cs                                       # Pipeline, Antiforgery, Dual Auth, RateLimiting, Bootstrap, Swagger
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── Properties/launchSettings.json                   # Profiles: http (:5206), https (:7146; :5206)
    ├── Authentication/BrowserSessionHandler.cs          # Cookie scheme (__Secure-DroneVision.Refresh)
    ├── Authorization/PermissionAuthorizationHandler.cs  # Evaluates PermissionRequirement via IPermissionService
    ├── Controllers/
    │   ├── ApiController.cs                             # Base [ApiController], [Authorize], CurrentUserId ("sub")
    │   ├── AuthController.cs                            # /auth/{csrf,register,login,refresh,logout}
    │   ├── AuthMeController.cs                          # GET /auth/me
    │   ├── PermissionsController.cs                     # GET /permissions
    │   ├── ProfileController.cs                         # /profile, /profile-image, /users/{id}/profile-image
    │   ├── ProfileImagesController.cs                   # GET /uploads/profiles/{fileName}
    │   ├── RolesController.cs                           # /roles CRUD
    │   └── UsersController.cs                           # /users CRUD
    ├── ExceptionHandling/
    │   ├── ApiProblems.cs                               # RFC 9457 enrichment (traceId) & ApiProblemDetailsWriter
    │   └── ExceptionHandlers.cs                         # Validation -> Api -> DatabaseConflict -> Global handlers
    └── OpenApi/SecurityOperationFilter.cs               # Adds BrowserSession/Bearer security & X-CSRF-TOKEN docs
```

---

## 3. Ports & URLs

| Mode | URL / Port | Gateway Prefix | Swagger URL |
| :--- | :--- | :--- | :--- |
| **Docker (Dev & Prod)** | `http://+:8080` (internal network) | `/api/management` (`Gateway__PathBase`) | `https://localhost:8443/api/management/swagger/index.html` (Dev only) |
| **Native (`https` profile)** | `https://localhost:7146`, `http://localhost:5206` | `/` (empty `Gateway:PathBase`) | `https://localhost:7146/swagger/index.html` (Dev only) |
| **Native (`http` profile)** | `http://localhost:5206` | `/` | `http://localhost:5206/swagger/index.html` (Dev only) |

---

## 4. Environment Variables & Configuration Keys

| Configuration Key | Env Var Syntax | Default (`appsettings.json` / Compose) | Description |
| :--- | :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Native: `Server=localhost;Database=DroneVision;Trusted_Connection=True;TrustServerCertificate=True;`<br/>Compose: `Server=database;Database=DroneVision;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True` | SQL Server connection string |
| `Jwt:Issuer` | `Jwt__Issuer` | `DroneVision` | JWT `iss` claim & validation |
| `Jwt:Audience` | `Jwt__Audience` | `DroneVision.Client` | JWT `aud` claim & validation |
| `Jwt:SigningKey` | `Jwt__SigningKey` | *Required* (`${JWT_SIGNING_KEY}`) | Base64-encoded key >= 64 random bytes (validated on startup) |
| `Jwt:AccessTokenMinutes` | `Jwt__AccessTokenMinutes` | `15` (range `1..60`) | Access JWT lifetime in minutes |
| `Jwt:RefreshTokenDays` | `Jwt__RefreshTokenDays` | `7` (range `1..30`) | Refresh session lifetime in days |
| `BootstrapAdmin:UserName` | `BootstrapAdmin__UserName` | `admin` | Initial admin username seeded by `IdentityBootstrap` |
| `BootstrapAdmin:FullName` | `BootstrapAdmin__FullName` | `System Administrator` | Initial admin display name |
| `BootstrapAdmin:Password` | `BootstrapAdmin__Password` | *Required on first boot* (`${BOOTSTRAP_ADMIN_PASSWORD}`) | Must pass Identity complexity (>=12 chars, digit, upper, lower, symbol) |
| `Gateway:PathBase` | `Gateway__PathBase` | Compose: `/api/management`; Native: `null` | Base path passed to `app.UsePathBase(...)` and prefixed to `profileImageUrl` |
| `RateLimiting:AuthenticationRequestsPerMinute` | `RateLimiting__AuthenticationRequestsPerMinute` | `20` (clamped `1..10000`) | Fixed-window per-IP limit for `"credentials"` policy (`/auth/register`, `/auth/login`) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Compose: `true` | Honors `X-Forwarded-Proto` and `X-Forwarded-For` from Ocelot |
| `ASPNETCORE_URLS` | `ASPNETCORE_URLS` | Compose: `http://+:8080` | Kestrel listening address inside container |
| `UserSecretsId` | — | `DroneVision-Management-API` | Local development secrets store ID |

---

## 5. Run, Build, Migration & Format Commands

Run from `src/services/Management` (or via `pnpm --filter managementService <script>` from root):

```powershell
# Build solution
pnpm --filter managementService build

# Run natively with hot reload (https://localhost:7146)
pnpm --filter managementService dev

# Run natively without watch
pnpm --filter managementService start

# Format C# whitespace / verify
pnpm --filter managementService format
pnpm --filter managementService format:check
```

### EF Core Database Migrations (from `src/services/Management`)

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project Management.Infrastructure --startup-project Management.API
dotnet ef migrations list --project Management.Infrastructure --startup-project Management.API
dotnet ef database update --project Management.Infrastructure --startup-project Management.API
```
