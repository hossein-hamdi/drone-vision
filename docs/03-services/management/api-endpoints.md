# Management Service — API Endpoints Reference

- **Base URL (via Gateway):** `https://localhost:8443/api/management`
- **Direct Local Dev URL:** `https://localhost:7146`
- **Development Swagger UI:** `https://localhost:8443/api/management/swagger/index.html` (disabled in `Production`)
- **Content Type:** `application/json` (errors: `application/problem+json`; image uploads: `multipart/form-data`; image downloads: `image/jpeg`, `image/png`, `image/webp`)
- **Strict JSON Rule:** `UnmappedMemberHandling.Disallow` is enabled globally—sending unknown JSON properties returns `400 Bad Request`.

### Authentication & CSRF Requirements
- **Dual Transport (`BrowserOrBearer`):** Send either `Authorization: Bearer <accessToken>` OR the `__Secure-DroneVision.Refresh` HttpOnly cookie.
- **CSRF Header (`X-CSRF-TOKEN`):** Required alongside the `__Host-DroneVision.Csrf` cookie on:
  - Browser `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` (unless `X-Auth-Transport: native` is set).
  - All cookie-authenticated protected writes (`POST`, `PUT`, `PATCH`, `DELETE`).
  - Not required on `GET`/`HEAD`/`OPTIONS`, `POST /auth/register`, or requests authenticated via `Authorization: Bearer <jwt>`.

---

## Endpoints Summary

| Group | Method | Path | Access / Policy | Rate Limit | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Auth** | `GET` | `/auth/csrf` | Public | — | Issue CSRF cookie + `requestToken` |
| **Auth** | `POST` | `/auth/register` | Public | `"credentials"` (20/min) | Self-register account (no roles) |
| **Auth** | `POST` | `/auth/login` | Public (CSRF or `native`) | `"credentials"` (20/min) | Authenticate and start session |
| **Auth** | `POST` | `/auth/refresh` | Public (Cookie+CSRF or `native`) | — | Rotate refresh token & issue new JWT |
| **Auth** | `POST` | `/auth/logout` | Public (Cookie+CSRF or `native`) | — | Revoke session & clear cookie |
| **Auth** | `GET` | `/auth/me` | Authenticated | — | Current user identity, roles & permissions |
| **Users** | `GET` | `/users` | `Users.View` | — | Paginated & filtered user list |
| **Users** | `GET` | `/users/{id:int:min(1)}` | `Users.View` | — | Get user details by ID |
| **Users** | `POST` | `/users` | `Users.Create` | — | Create user with assigned roles |
| **Users** | `PUT` | `/users/{id:int:min(1)}` | `Users.Update` | — | Update user details & sync roles |
| **Users** | `DELETE` | `/users/{id:int:min(1)}` | `Users.Delete` | — | Delete user, sessions & profile image |
| **Roles** | `GET` | `/roles` | `Roles.View` | — | Paginated & filtered role list |
| **Roles** | `GET` | `/roles/{id:int:min(1)}` | `Roles.View` | — | Get role details by ID |
| **Roles** | `POST` | `/roles` | `Roles.Create` | — | Create role with permissions |
| **Roles** | `PUT` | `/roles/{id:int:min(1)}` | `Roles.Update` | — | Update role name & permissions |
| **Roles** | `DELETE` | `/roles/{id:int:min(1)}` | `Roles.Delete` | — | Delete role (Admin protected) |
| **Permissions** | `GET` | `/permissions` | `Permissions.View` | — | List predefined permissions catalog |
| **Profile** | `GET` | `/profile` | Authenticated | — | Current user's public profile & roles |
| **Profile** | `PUT` | `/profile` | Authenticated | — | Update current user's name/username |
| **Profile** | `POST` | `/profile-image` | Authenticated | — | Upload current user's image (<= 5 MiB) |
| **Profile** | `DELETE` | `/profile-image` | Authenticated | — | Delete current user's profile image |
| **Profile** | `POST` | `/users/{id:int:min(1)}/profile-image` | `Users.Update` | — | Upload target user's image (<= 5 MiB) |
| **Profile** | `DELETE` | `/users/{id:int:min(1)}/profile-image` | `Users.Update` | — | Delete target user's profile image |
| **Uploads** | `GET` | `/uploads/profiles/{fileName}` | Public | — | Serve stored profile image bytes |

---

## 1. Auth Endpoints (`AuthController` & `AuthMeController`)

All `AuthController` responses set `Cache-Control: no-store`.

### `GET /auth/csrf`
- **Auth:** Public (`[AllowAnonymous]`)
- **Behavior:** Generates an antiforgery token pair, sets `__Host-DroneVision.Csrf` (`Secure`, `HttpOnly`, `SameSite=Strict`, `Path=/`), and returns `requestToken`.
- **Response (`200 OK`):**
```json
{
  "requestToken": "CfDJ8N..."
}
```

### `POST /auth/register`
- **Auth:** Public (`[AllowAnonymous]`) · **Rate Limit:** `"credentials"` (`20` req/min per IP)
- **Request Body (`RegisterRequest`):**
```json
{
  "fullName": "Jane Doe",
  "userName": "janedoe",
  "password": "StrongPassword123!"
}
```
  - `fullName`: required, max `200` chars
  - `userName`: required, max `256` chars
  - `password`: required, `12..128` chars (Identity requires digit, lower, upper, non-alphanumeric)
- **Success Response (`201 Created`):** `Location: <PathBase>/users/<id>`
```json
{
  "id": 2,
  "fullName": "Jane Doe",
  "userName": "janedoe",
  "roles": [],
  "permissions": [],
  "createdAt": "2026-11-22T12:00:00Z",
  "updatedAt": "2026-11-22T12:00:00Z"
}
```
  *(Note: `DefaultIgnoreCondition = WhenWritingNull` omits `profileImageUrl` when `null`.)*
- **Errors:** `400` (validation/password complexity/unmapped fields like `roles`), `409` (duplicate username), `429` (rate limit).

### `POST /auth/login`
- **Auth:** Public (`[AllowAnonymous]`) · **Rate Limit:** `"credentials"` (`20` req/min per IP)
- **Headers:**
  - Browser mode: `X-CSRF-TOKEN: <token>` + `__Host-DroneVision.Csrf` cookie
  - Native mode: `X-Auth-Transport: native` (must NOT include `Origin`, `Sec-Fetch-Site`, or `__Secure-DroneVision.Refresh` cookie)
- **Request Body (`LoginRequest`):**
```json
{
  "userName": "admin",
  "password": "Admin-Password123!"
}
```
- **Success Response (`200 OK`):**
  - Browser mode sets `Set-Cookie: __Secure-DroneVision.Refresh=<86-char-token>; HttpOnly; Secure; SameSite=Strict; Path=/` and omits `refreshToken` from JSON.
  - Native mode includes `"refreshToken": "<86-char-token>"`.
```json
{
  "accessToken": "eyJhbGciOiJIUzUxMiIs...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "refreshTokenExpiresAt": "2026-11-29T12:00:00Z",
  "refreshToken": null
}
```
- **Errors:** `400` (CSRF missing/invalid, invalid `X-Auth-Transport`, native request with browser headers), `401` (invalid credentials or locked out after 5 failures for 15 min), `429`.

### `POST /auth/refresh`
- **Auth:** Public (`[AllowAnonymous]`, validated via refresh token + transport rules)
- **Headers:** Browser: `X-CSRF-TOKEN` + `__Host-DroneVision.Csrf` + `__Secure-DroneVision.Refresh`; Native: `X-Auth-Transport: native`.
- **Request Body (`RefreshRequest?`, optional/empty for browser):**
  - Browser: `{}` (or empty body; sending `refreshToken` in JSON returns `400`).
  - Native: `{ "refreshToken": "<86-char-token>" }`.
- **Success Response (`200 OK`):** Rotates refresh token (`AuthenticationResponse`), updates `__Secure-DroneVision.Refresh` cookie (browser) or returns new `refreshToken` (native).
- **Errors:** `400` (transport/CSRF violation), `401` (missing, malformed, expired, revoked, cross-transport, or replayed token—replay revokes entire session; browser mode also deletes `__Secure-DroneVision.Refresh` cookie).

### `POST /auth/logout`
- **Auth:** Public (`[AllowAnonymous]`, works even with an expired access JWT)
- **Headers & Body:** Same transport/token rules as `POST /auth/refresh`.
- **Success Response (`204 No Content`):** Revokes session (`RevokedAtUtc = now`) and clears `__Secure-DroneVision.Refresh` cookie in browser mode (`finally`).
- **Errors:** `400` (transport/CSRF), `401` (invalid/already-revoked/expired token; cookie still cleared in browser mode).

### `GET /auth/me`
- **Auth:** Required (`BrowserSession` cookie or `Bearer` JWT)
- **Success Response (`200 OK` — `AuthenticatedUserDto`):**
```json
{
  "id": 1,
  "fullName": "System Administrator",
  "userName": "admin",
  "roles": ["Admin"],
  "permissions": [
    "Permissions.View",
    "Roles.Create",
    "Roles.Delete",
    "Roles.Update",
    "Roles.View",
    "Users.Create",
    "Users.Delete",
    "Users.Update",
    "Users.View"
  ],
  "createdAt": "2026-11-22T12:00:00Z",
  "updatedAt": "2026-11-22T12:00:00Z"
}
```
- **Errors:** `401 Unauthorized`.

---

## 2. Users Endpoints (`UsersController`)

### `GET /users`
- **Permission:** `Users.View`
- **Query Parameters (`UserFilter`):**
  - `role` (`string?`, max 256) — exact normalized role match
  - `name` (`string?`, max 256) — substring match on `FullName` OR `UserName`
  - `createdAtFrom` (`DateTime?`) — UTC inclusive start
  - `createdAtTo` (`DateTime?`) — UTC inclusive end (if time-of-day is `00:00:00`, expands to end of day `23:59:59.9999999`)
  - `page` (`int`, default `1`, min `1`)
  - `pageSize` (`int`, default `10`, range `1..100`)
- **Success Response (`200 OK` — `PagedResult<UserDto>`):**
```json
{
  "items": [
    {
      "id": 1,
      "fullName": "System Administrator",
      "userName": "admin",
      "profileImageUrl": "/api/management/uploads/profiles/ab12cd...jpg",
      "roles": ["Admin"],
      "permissions": ["Permissions.View", "Roles.Create", "..."],
      "createdAt": "2026-11-22T12:00:00Z",
      "updatedAt": "2026-11-22T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```
- **Errors:** `400` (`page`/`pageSize` out of range or `createdAtFrom > createdAtTo`), `401`, `403`.

### `GET /users/{id:int:min(1)}`
- **Permission:** `Users.View`
- **Success Response (`200 OK`):** `UserDto`
- **Errors:** `401`, `403`, `404 Not Found` (`User was not found.`).

### `POST /users`
- **Permission:** `Users.Create` · *(Requires `X-CSRF-TOKEN` for browser cookie auth)*
- **Request Body (`CreateUserRequest`):**
```json
{
  "fullName": "Ali Operator",
  "userName": "ali.op",
  "password": "OperatorPass123!",
  "roles": ["Operator"]
}
```
  - `roles`: required array (max 100 items); every role name must exist in `AspNetRoles`.
- **Success Response (`201 Created`):** `UserDto` (includes distinct sorted inherited `permissions`).
- **Errors:** `400` (CSRF, invalid/unknown role names, password complexity), `401`, `403`, `409` (duplicate username).

### `PUT /users/{id:int:min(1)}`
- **Permission:** `Users.Update` · *(Requires `X-CSRF-TOKEN` for browser cookie auth)*
- **Request Body (`UpdateUserRequest`):**
```json
{
  "fullName": "Ali Operator Updated",
  "userName": "ali.op",
  "roles": ["Operator", "Viewer"]
}
```
- **Success Response (`200 OK`):** Updated `UserDto` (passwords unchanged; role assignments diffed & synchronized).
- **Errors:** `400`, `401`, `403`, `404`, `409` (duplicate username or concurrency conflict).

### `DELETE /users/{id:int:min(1)}`
- **Permission:** `Users.Delete` · *(Requires `X-CSRF-TOKEN` for browser cookie auth)*
- **Success Response (`204 No Content`):** Deletes user, cascades to `AspNetUserTokens` (sessions/refresh hashes), `AspNetUserRoles`, and `ProfileImages`.
- **Errors:** `400` (CSRF), `401`, `403`, `404`, `409`.

---

## 3. Roles & Permissions Endpoints (`RolesController` & `PermissionsController`)

### `GET /roles`
- **Permission:** `Roles.View`
- **Query Parameters (`RoleFilter`):** `roleName` (`string?`, max 256, substring match), `createdAtFrom` (`DateTime?`), `createdAtTo` (`DateTime?`), `page` (`int`, default `1`), `pageSize` (`int`, default `10`, range `1..100`).
- **Success Response (`200 OK` — `PagedResult<RoleDto>`):**
```json
{
  "items": [
    {
      "id": 1,
      "name": "Admin",
      "permissions": ["Permissions.View", "Roles.Create", "..."],
      "createdAt": "2026-11-22T12:00:00Z",
      "updatedAt": "2026-11-22T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

### `GET /roles/{id:int:min(1)}`
- **Permission:** `Roles.View`
- **Success Response (`200 OK`):** `RoleDto` · **Errors:** `401`, `403`, `404`.

### `POST /roles`
- **Permission:** `Roles.Create` · *(CSRF required for cookie auth)*
- **Request Body (`RoleRequest`):**
```json
{
  "name": "Operator",
  "permissions": ["Users.View", "Roles.View"]
}
```
  - Every string in `permissions` must exist in `Permissions.All` (duplicates are deduplicated).
- **Success Response (`201 Created`):** `RoleDto` · **Errors:** `400` (invalid permission name/CSRF), `401`, `403`, `409` (duplicate role name).

### `PUT /roles/{id:int:min(1)}`
- **Permission:** `Roles.Update` · *(CSRF required for cookie auth)*
- **Request Body (`RoleRequest`):** `{ "name": "...", "permissions": [...] }`
- **Success Response (`200 OK`):** `RoleDto`
- **Errors:** `400`, `401`, `403`, `404`, `409` (duplicate name, concurrency failure, OR attempting to rename bootstrap `Admin` role or remove any permission from it).

### `DELETE /roles/{id:int:min(1)}`
- **Permission:** `Roles.Delete` · *(CSRF required for cookie auth)*
- **Success Response (`204 No Content`)**
- **Errors:** `400` (CSRF), `401`, `403`, `404`, `409` (`The bootstrap Admin role cannot be deleted.`).

### `GET /permissions`
- **Permission:** `Permissions.View`
- **Success Response (`200 OK` — `IReadOnlyList<PermissionDto>`):**
```json
[
  { "id": 1, "name": "Users.View" },
  { "id": 2, "name": "Users.Create" },
  { "id": 3, "name": "Users.Update" },
  { "id": 4, "name": "Users.Delete" },
  { "id": 5, "name": "Roles.View" },
  { "id": 6, "name": "Roles.Create" },
  { "id": 7, "name": "Roles.Update" },
  { "id": 8, "name": "Roles.Delete" },
  { "id": 9, "name": "Permissions.View" }
]
```

---

## 4. Profile & Profile Image Endpoints (`ProfileController` & `ProfileImagesController`)

### `GET /profile`
- **Auth:** Authenticated user (`CurrentUserId`)
- **Success Response (`200 OK` — `ProfileDto`):** Same as `UserDto` without `permissions`:
```json
{
  "id": 1,
  "fullName": "System Administrator",
  "userName": "admin",
  "profileImageUrl": "/api/management/uploads/profiles/...",
  "roles": ["Admin"],
  "createdAt": "2026-11-22T12:00:00Z",
  "updatedAt": "2026-11-22T12:00:00Z"
}
```

### `PUT /profile`
- **Auth:** Authenticated user · *(CSRF required for cookie auth)*
- **Request Body (`UpdateProfileRequest`):**
```json
{
  "fullName": "Updated Name",
  "userName": "updated_username"
}
```
- **Success Response (`200 OK`):** `ProfileDto` · **Errors:** `400`, `401`, `404`, `409` (duplicate username).

### `POST /profile-image` and `POST /users/{id:int:min(1)}/profile-image`
- **Auth:** Self: Authenticated user; Admin target: `Users.Update` · *(CSRF required for cookie auth)*
- **Content-Type:** `multipart/form-data` with required file field `Image` (case-insensitive form binding).
- **Limits & Magic-Byte Validation (`ProfileImageStore`):**
  - Request limit: `5 MiB + 64 KiB` (`5,308,416` bytes)
  - File size: `1 byte .. 5 MiB` (`5,242,880` bytes)
  - Validated strictly by binary magic headers:
    - **JPEG:** starts `FF D8 FF` and ends `FF D9`
    - **PNG:** 8-byte PNG signature + `IHDR` + `IEND` chunk markers
    - **WebP:** `RIFF` + valid chunk size + `WEBP` + (`VP8 `, `VP8L`, or `VP8X`)
- **Success Response (`200 OK`):**
```json
{
  "profileImageUrl": "/api/management/uploads/profiles/a1b2c3d4...png"
}
```
- **Errors:** `400` (missing file, >5 MiB, empty, invalid magic bytes, CSRF), `401`, `403` (for `/users/{id}/profile-image`), `404` (user not found).

### `DELETE /profile-image` and `DELETE /users/{id:int:min(1)}/profile-image`
- **Auth:** Self: Authenticated user; Admin target: `Users.Update` · *(CSRF required for cookie auth)*
- **Success Response (`204 No Content`)** (idempotent if user exists and has no image).
- **Errors:** `400` (CSRF), `401`, `403`, `404` (user not found).

### `GET /uploads/profiles/{fileName}`
- **Auth:** Public (`[AllowAnonymous]`)
- **Success Response (`200 OK`):** Raw binary image bytes with `Content-Type` (`image/jpeg`, `image/png`, or `image/webp`).
- **Errors:** `404 Not Found` (`Profile image was not found.`).
