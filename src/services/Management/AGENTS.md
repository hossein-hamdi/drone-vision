# Management

Clean Architecture Management API, targeting .NET 10, with ASP.NET Core Identity,
JWT authentication, rotating refresh tokens, and permission-based authorization.

## Structure

```text
Management/
|-- Management.API/
|   |-- Contracts/Authentication/
|   |-- Controllers/
|   |-- Middleware/
|   |-- Properties/
|   |-- Program.cs
|   `-- appsettings*.json
|-- Management.Application/
|   |-- Abstractions/
|   |-- Common/Models/
|   |-- DTOs/
|   `-- Features/
|-- Management.Domain/
|   |-- Common/
|   |-- Entities/
|   |-- Exceptions/
|   `-- ValueObjects/
`-- Management.Infrastructure/
|   |-- Authentication/
|   |-- Identity/
|   `-- Persistence/
```

## Layer Responsibilities

- **Domain:** business entities, value objects, and domain rules; no project dependencies.
- **Application:** use cases and infrastructure abstractions; references Domain.
- **Infrastructure:** persistence and identity adapters; references Application and Domain.
- **API:** HTTP contracts and the composition root; references Application and Infrastructure.

Keep framework and persistence dependencies outside Domain and Application.
The `Commands` and `Queries` folders organize future use cases; no mediator or
other third-party package is installed.

## Authentication

Use only Identity's existing tables for authentication and authorization. Users, roles,
and memberships use `AspNetUsers`, `AspNetRoles`, and `AspNetUserRoles`.
Permission definitions live in `Management.Application/Authorization/Permissions.cs`;
each role's selected permissions are `permission` claims in `AspNetRoleClaims`.
`GET /permissions` reads the local catalog; its numeric IDs are stable catalog positions,
not database keys. Append new permission definitions without reordering existing entries.
Session records and refresh-token hashes use the derived `ApplicationUserToken` in
`AspNetUserTokens`, distinguished by `DroneVision.Session` and `DroneVision.Refresh`
providers. Refresh-token hashes are stored in the token name; raw refresh tokens are
never stored. Do not introduce separate permission, role-permission, session, or
refresh-token tables.

Browser clients use HTTPS on the API's origin (proxy `/auth`, API routes, and uploads if a frontend is hosted separately). The API sets a Secure, HttpOnly, SameSite=Strict cookie scoped to `/auth`; refresh secrets never appear in browser JSON responses. There is no permissive CORS configuration. The cookie is sent only to authentication routes.

1. `GET /auth/csrf` obtains the anonymous antiforgery cookie and `{ "requestToken": "..." }`.
2. `POST /auth/login` sends username/password JSON and that token in `X-CSRF-TOKEN`.
3. `POST /auth/refresh` sends `{}`, the cookie, and `X-CSRF-TOKEN`. It returns a new access token, `expiresIn`, `tokenType`, and `refreshTokenExpiresAt` in UTC, and rotates the cookie.
4. `POST /auth/logout` uses the same transport; it revokes the entire session and deletes the cookie, even if the access JWT has expired.

Keep the dedicated authentication HTTP client free of bearer headers. Obtain the CSRF token anonymously, and reuse it with anonymous refresh/logout requests. Public registration is `POST /auth/register` with fullName, userName, and password; it never grants roles or creates a refresh cookie. Unknown JSON properties (including attempted registration roles) are rejected.

Native/CLI clients explicitly set `X-Auth-Transport: native` on login/refresh/logout. They must omit browser Origin/Sec-Fetch-Site headers and the refresh cookie. Login and refresh include `refreshToken` in JSON; refresh/logout accept `{ "refreshToken": "..." }`. Native and browser sessions cannot switch transports. Store native refresh secrets in an OS credential store/keychain, transmit only over HTTPS to `/auth/refresh` or `/auth/logout`, and avoid request-body logging. Do not store refresh tokens in localStorage or URLs.

Each raw refresh token has 512 random bits. Only its SHA-256 hash is stored, with a unique index. Rotation and logout lock the integer session row with SQL Server `UPDLOCK` inside a transaction. A reused rotated token revokes the family before returning 401; all descendants and access JWTs from that session then fail. Concurrent use of the same refresh token permits one rotation and treats subsequent uses as replay. Use one shared refresh coordinator per browser application. Access tokens carry subject, session ID, random JWT ID, issuer, audience, and validity timestamps; roles and permissions are not copied into JWTs. Every authenticated request validates the session and user lockout state, so logout, replay, and user deletion invalidate existing JWTs immediately.

Requests trigger renewal; the backend cannot proactively renew browser tokens. Clients should refresh shortly before access-token expiry, coordinate concurrent refresh requests, retry an expired-token request once, and clear authentication state if refresh fails.

## Endpoints and policies

`GET /auth/me` returns the authenticated user's identity, roles, and current permissions.
`GET /profile` returns profile fields and roles without permissions.
Users responses (`GET /users`, `GET /users/{id}`, `POST /users`, `PUT /users/{id}`)
include a sorted, distinct `permissions` array inherited from the user's current roles.
Create/update requests assign roles; they do not accept direct user permission grants.
The permission catalog remains local, and role selections remain in `AspNetRoleClaims`.

Profile images are stored separately in `ProfileImages`, with an integer ID, a unique
`UserId` foreign key to `AspNetUsers`, generated `Url`, validated `ContentType`, binary
`Content`, and a SQL Server `RowVersion` for concurrent updates. User deletion cascades
to the image. `AspNetUsers` does not contain image bytes, content type, or URL columns;
public user/profile DTOs project `profileImageUrl` from the related image.
Uploads accept JPEG, PNG, or WebP up to 5 MiB and replace the previous database image.
`GET /uploads/profiles/{fileName}` serves the image bytes from the database using its
generated public URL. Upload/delete operations update the image row atomically and
enforce one image per user. The migration preserves existing database images and their
URLs when moving them out of `AspNetUsers`. Images are never written to local files.
`GET /auth/me` is grouped with the other Auth endpoints in Swagger and requires authentication.

| Endpoints | Access |
| --- | --- |
| `POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`; `GET /auth/csrf` | Anonymous, transport/CSRF validation where applicable |
| `GET /users`, `/users/{id}` | `Users.View` |
| `POST /users` | `Users.Create` |
| `PUT /users/{id}` | `Users.Update` |
| `DELETE /users/{id}` | `Users.Delete` |
| `GET /roles`, `/roles/{id}` | `Roles.View` |
| `POST /roles` | `Roles.Create` |
| `PUT /roles/{id}` | `Roles.Update` |
| `DELETE /roles/{id}` | `Roles.Delete` |
| `GET /permissions` | `Permissions.View` |
| `GET /auth/me`, `/profile`; `POST`, `DELETE /profile-image` | Authenticated user |
| `GET /uploads/profiles/{fileName}` | Anonymous, generated image URL |
| `POST`, `DELETE /users/{id}/profile-image` | `Users.Update` |

All route IDs are positive integers. `GET /users?role=Operator&name=ali&page=1&pageSize=10` supports role and full-name/username filtering; page size is 1–100. User creation accepts fullName, userName, password, and roles; updating accepts fullName, userName, and roles. Image URLs are managed exclusively through image upload/delete operations. Roles accept name and a permissions array. Role and user updates are transactional and use Identity concurrency stamps. Permission policies query current role assignments on every request, so changes apply without another login. Administrative user creation/updates can assign roles, as explicitly permitted by `Users.Create`/`Users.Update`; grant those permissions only to trusted administrators.

All public DTOs omit passwords and Identity internals. Errors use RFC 9457 ProblemDetails with traceId; validation adds an errors dictionary. Expected statuses include 400, 401, 403, 404, and 409. Expired access JWTs return 401; the client handles renewal. Ordered `IExceptionHandler` implementations use `IProblemDetailsService`, with a logged, generic 500 fallback. The implementation follows the modern handler chain in the [requested reference](https://milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-from-middleware-to-modern-handlers). Login uses Identity lockout (five failures, 15 minutes), and login/registration have a per-IP rate limit (20 requests/minute, 429). Authentication rate limiting is configurable through `RateLimiting:AuthenticationRequestsPerMinute`.


## Build and Run

From this directory, with the .NET 10 SDK installed:

```powershell
dotnet build Management.API/Management.API.csproj
dotnet run --project Management.API --launch-profile https
```

The API listens on `https://localhost:7146` and `http://localhost:5206` (redirects to HTTPS).
Development Swagger is available at `https://localhost:7146/swagger/index.html`.
Swagger automatically authorizes protected requests after login/refresh and clears
authorization on logout. Anonymous authentication requests omit bearer headers;
`GET /auth/me` requires the bearer token.

Set `Jwt:SigningKey` (Base64 for at least 64 random bytes) and the first-start
`BootstrapAdmin:Password` through User Secrets or environment variables. Startup
applies EF migrations and ensures the bootstrap Admin role retains all permissions.
Generate migrations with:

```powershell
dotnet ef migrations add MigrationName --project Management.Infrastructure --startup-project Management.API
```
