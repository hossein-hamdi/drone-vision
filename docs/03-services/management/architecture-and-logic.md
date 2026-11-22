# Management Service — Architecture & Business Logic

---

## 1. Clean Architecture Layering

| Project | Dependencies | Responsibilities |
| :--- | :--- | :--- |
| **`Management.Domain`** | None | Domain layer placeholder (`Management.Domain.csproj`; currently contains no source files while Identity/persistence entities live in Infrastructure). |
| **`Management.Application`** | `Management.Domain` | Service contracts (`IAuthenticationService`, `IUserService`, `IRoleService`, `IPermissionService`, `IProfileImageService`), canonical `Permissions.All`, DTO records/requests with DataAnnotations, and `ApiException`. |
| **`Management.Infrastructure`** | `Management.Application`, `Management.Domain`, `Microsoft.AspNetCore.App` | `ManagementDbContext` (EF Core + SQL Server), Identity overrides (`ApplicationUser`, `ApplicationRole`, `ApplicationUserStore`, `IdentityErrors`), `AuthService` (`UPDLOCK` session rotation), `JwtTokenService`, `PermissionService`, `UserService`, `RoleService`, `ProfileImageService`/`ProfileImageStore`, `IdentityBootstrap`. |
| **`Management.API`** | `Management.Application`, `Management.Infrastructure` | Composition root (`Program.cs`), `BrowserSessionHandler`, `PermissionAuthorizationHandler`, REST Controllers, `IExceptionHandler` chain, Antiforgery, RateLimiting, OpenAPI/Swagger. |

---

## 2. Database Schema & Persistence (`ManagementDbContext`)

`ManagementDbContext` extends `IdentityDbContext<ApplicationUser, ApplicationRole, int, ApplicationUserClaim, ApplicationUserRole, ApplicationUserLogin, ApplicationRoleClaim, ApplicationUserToken>`.

### Tables & Key Customizations

| Table | Entity | Key Customizations & Indexes |
| :--- | :--- | :--- |
| `AspNetUsers` | `ApplicationUser` | `int` identity PK, `FullName` (`nvarchar(200)` required), `CreatedAt`/`UpdatedAt` UTC timestamps, optional 1:1 `ProfileImage` |
| `AspNetRoles` | `ApplicationRole` | `int` identity PK, `IsBootstrapAdmin` (`bit`), filtered unique index where `[IsBootstrapAdmin] = 1`, timestamps |
| `AspNetUserRoles` | `ApplicationUserRole` | `int Id` PK + unique index on `(UserId, RoleId)` (queried via custom `ApplicationUserStore`), timestamps |
| `AspNetUserLogins` | `ApplicationUserLogin` | `int Id` PK + unique index on `(LoginProvider, ProviderKey)` (`128` max len), timestamps |
| `AspNetUserClaims` | `ApplicationUserClaim` | Standard Identity claim columns + timestamps |
| `AspNetRoleClaims` | `ApplicationRoleClaim` | Stores RBAC grants (`ClaimType = "permission"`, `ClaimValue = <permission>`) + timestamps |
| `AspNetUserTokens` | `ApplicationUserToken` | Stores both sessions (`LoginProvider = "DroneVision.Session"`) and refresh hashes (`LoginProvider = "DroneVision.Refresh"`). Extra columns: `Id` (int PK), `SessionId` (indexed), `IsBrowserSession`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`. Filtered unique index `IX_AspNetUserTokens_RefreshHash` on `Name` where `[LoginProvider] = 'DroneVision.Refresh'`. |
| `ProfileImages` | `ProfileImage` | `Id` (int PK), `UserId` (unique FK -> `AspNetUsers.Id` with `DeleteBehavior.Cascade`), `Url` (unique `nvarchar(300)`), `ContentType` (`nvarchar(32)`), `Content` (`varbinary(max)`), `RowVersion` (`rowversion` concurrency token), timestamps |

### Automatic UTC Timestamps (`ITimestampedEntity`)
- All 8 tables implement `ITimestampedEntity` (`CreatedAt`, `UpdatedAt`).
- Every `SaveChanges` / `SaveChangesAsync` call intercepts `Added` and `Modified` entities to stamp `CreatedAt`/`UpdatedAt` from injected `TimeProvider` and marks `CreatedAt` unmodified on updates.
- `UtcDateTimeConverter` ensures DateTimes read from SQL Server carry `DateTimeKind.Utc`, and SQL columns default to `SYSUTCDATETIME()`.

### EF Core Migration History (`Management.Infrastructure/Persistence/Migrations/`)
1. `20261107174108_InitialIdentity` — Initial Identity tables + legacy `Permissions`, `RolePermissions`, `RefreshTokenSessions`, `RefreshTokens`.
2. `20261107191013_UseIdentityAuthenticationStorage` — Migrates role permissions into `AspNetRoleClaims` and sessions/refresh tokens into `AspNetUserTokens` (preserving session IDs via `IDENTITY_INSERT`), then drops legacy custom auth/permission tables.
3. `20261107193317_StoreProfileImagesInIdentity` — Temporarily added image columns onto `AspNetUsers`.
4. `20261107194850_SeparateProfileImages` — Creates `ProfileImages` table, migrates existing images from `AspNetUsers`, and removes image columns from `AspNetUsers`.
5. `20261121212601_AddTimestampsToAllTables` — Adds `CreatedAt` and `UpdatedAt` (`SYSUTCDATETIME()`) across all 8 tables.

---

## 3. Startup Bootstrap (`IdentityBootstrap.InitializeAsync`)

Executed at the end of `Program.cs` before `app.RunAsync()`:
1. Applies pending EF Core migrations (`await db.Database.MigrateAsync()`).
2. Opens a SQL Server transaction and acquires an exclusive application lock (`EXEC sys.sp_getapplock @Resource = N'DroneVision.IdentityBootstrap', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000`) so concurrent container/instance startups never race.
3. Ensures the `"Admin"` role exists with `IsBootstrapAdmin = true` and possesses every permission in `Permissions.All`.
4. Looks up `BootstrapAdmin:UserName` (default `"admin"`). If missing, creates the user with `BootstrapAdmin:Password` (throws `InvalidOperationException` if `BootstrapAdmin:Password` is missing on first startup) and adds the user to `"Admin"`.

---

## 4. Authentication, Sessions & Security Invariants

### Identity Rules (`DependencyInjection.cs`)
- **Password Policy:** Minimum `12` chars, requires digit, lowercase, uppercase, and non-alphanumeric character.
- **Account Lockout:** `5` failed login attempts lock out the user for `15` minutes (`SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`).

### Dual-Transport & Session Lifecycle (`AuthService` + `BrowserSessionHandler` + `Program.cs`)
- **JwtOptions Startup Validation:** Validated on startup via `ValidateDataAnnotations().Validate(JwtOptions.HasStrongKey).ValidateOnStart()` (requires Base64 >= 64 decoded bytes).
- **Bearer Validation (`JwtBearerEvents.OnTokenValidated`):** Parses integer `sub` and `sid`, then queries SQL Server (`ManagementDbContext`) to verify the `DroneVision.Session` row is unrevoked, unexpired, and the user is not locked out.
- **Cookie Validation (`BrowserSessionHandler`):** Reads `__Secure-DroneVision.Refresh`, verifies it is exactly `86` Base64Url characters, hashes via SHA-256, and joins `Refresh` -> `Session` (`IsBrowserSession == true`) -> `AspNetUsers`. On invalid/revoked cookies, sets `Response.OnStarting` to delete `__Secure-DroneVision.Refresh` and returns `AuthenticateResult.Fail`.
- **CSRF Middleware (`Program.cs`):** Any authenticated request using `BrowserSession` (`AuthenticationType == "BrowserSession"`) that targets a non-`[AllowAnonymous]` endpoint with a non-`GET`/`HEAD`/`OPTIONS` method must pass `IAntiforgery.ValidateRequestAsync()` (`__Host-DroneVision.Csrf` cookie + `X-CSRF-TOKEN` header).
- **Transactional Token Rotation (`UPDLOCK, ROWLOCK`):** `AuthService.RefreshAsync` and `LogoutAsync` lock the parent `DroneVision.Session` row in `AspNetUserTokens` inside a DB transaction. If a previously revoked refresh token is presented (`previous.RevokedAtUtc is not null`), `RevokeSessionAsync` marks the parent session revoked and commits before throwing `401 Unauthorized`, immediately invalidating all descendant refresh tokens and access JWTs.

---

## 5. Business Rules Across Services

- **User Management (`UserService`):**
  - `CreateAsync` and `UpdateAsync` validate that all requested role names exist (`ResolveRolesAsync`); `UpdateAsync` diffs current vs target roles inside a transaction.
  - Queries project `ProfileImageUrl` as `(Gateway:PathBase ?? "") + ProfileImage.Url` and load distinct sorted `permissions` across all assigned roles (`IPermissionService.GetForUsersAsync` / `GetAsync`).
  - `ListAsync` supports `Role` (exact normalized role), `Name` (`FullName` or `UserName` `Contains`), UTC `CreatedAtFrom`/`CreatedAtTo` (`TimestampFilterExtensions`), and pagination (`PageSize` 1–100).
- **Role Management (`RoleService`):**
  - `Permissions` must be members of `Permissions.All` (`ResolvePermissions`).
  - `IsBootstrapAdmin` roles cannot be deleted (`409 Conflict`), cannot be renamed away from `"Admin"`, and cannot have any permission in `Permissions.All` removed (`409 Conflict`).
  - `PermissionsAsync` returns `Permissions.All` with 1-based catalog index IDs (never stored in SQL Server—always append new permissions at the end of `Permissions.All`).
- **Profile Images (`ProfileImageService` & `ProfileImageStore`):**
  - Enforces one image per user; uploading replaces the existing `ProfileImage` row with a new random 24-byte hex filename (`/uploads/profiles/<48-hex>.<ext>`), validated strictly via magic bytes (JPEG/PNG/WebP) up to `5 MiB`. Images are stored solely in SQL Server `varbinary(max)`, never on local disk.

---

## 6. Error Handling Chain (`ExceptionHandlers.cs`)

1. `ValidationExceptionHandler` (`ApiException { StatusCode: 400 }` → `400 ValidationProblemDetails` with `errors`)
2. `ApiExceptionHandler` (other `ApiException` → `401`/`404`/`409` `ProblemDetails`)
3. `DatabaseConflictExceptionHandler` (`DbUpdateConcurrencyException` or SQL Server codes `2601`, `2627`, `547`, `1205` → `409 Conflict`)
4. `GlobalExceptionHandler` (`OperationCanceledException` on client abort → `499`; `BadHttpRequestException` → `badRequest.StatusCode`; unhandled → logged error + generic `500` `ProblemDetails` with `traceId`)
