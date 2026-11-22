# Frontend Application (`frontend`) — Skyeye

**Skyeye** (`frontend/`) is a **Next.js 16.4.6** (App Router, React `19.3.0`) web application styled with **Tailwind CSS v4** and **shadcn/ui (`base-nova` on `@base-ui/react`)**. It serves as the operator dashboard for authentication, profile management, RBAC role & permission administration, and user management via same-origin calls through the Ocelot API Gateway (`/api/management/*`).

---

## 1. Tech Stack & Key Packages

| Category | Technology / Version | Purpose |
| :--- | :--- | :--- |
| **Framework & Runtime** | `next` `16.4.6`, `react` `19.3.0`, `react-dom` `19.3.0`, `typescript` `^5` | App Router, `output: 'standalone'`, `cacheComponents: true`, `partialPrefetching: true` |
| **UI & Styling** | `tailwindcss` `^4` (`@tailwindcss/turbopack`), `shadcn` `^4.23.0` (`base-nova`), `@base-ui/react` `^1.9.0`, `lucide-react` `^1.66.0`, `next-themes` `^0.4.6`, `tw-animate-css` `^1.4.0` | Accessible Base UI primitives (`render` prop composition), OKLCH light/dark CSS variables |
| **Data Fetching & API** | `@tanstack/react-query` `^5.105.1`, `zod` `^4.6.7` | Singleton `QueryClient` with declarative endpoint registry (`client.registerEndpoint`) & CSRF pre-flight |
| **Tables & URL State** | `@tanstack/react-table` `9.3.1`, `nuqs` `^2.10.3`, `cmdk` `^1.1.1`, `@dnd-kit/*`, `react-day-picker` `^10.0.2` | Server/client DataTable with URL-synced pagination, sorting, and filtering |
| **Forms & State** | `react-hook-form` `^7.91.0`, `@hookform/resolvers` `^7.0.1`, `zustand` `^5.1.0` | Schema-validated forms and vanilla Zustand `toastStore` |
| **Testing & Quality** | `vitest` `^5.1.1`, `@vitest/browser-playwright` `^5.1.1`, `playwright` `^1.65.1`, `eslint` `^9`, `prettier` `^3.9.10` | Dual-project Vitest (`unit` in Node + `browser` in Playwright Chromium), ESLint, Prettier import/Tailwind sorting |

---

## 2. Directory & File Structure

```text
frontend/
├── Dockerfile                           # Multi-stage node:24-alpine build (dependencies -> development / build -> runtime)
├── compose.yaml                         # Production service (context: .., exposes internal :3000)
├── package.json                         # pnpm scripts: dev, build, start, preview, typecheck, test, lint, format, check
├── next.config.ts                       # output: 'standalone', cacheComponents, partialPrefetching, turbopack CSS loader
├── proxy.ts                             # Next.js 16 server edge proxy gating /login vs protected routes via refresh cookie
├── tsconfig.json                        # Strict TS + path aliases (@api/*, @components/*, @core/*, @features/*, @hooks/*, @schemas, @stores/*, @types, @/*)
├── vitest.config.mjs                    # Dual projects: `unit` (node, *.test.ts) & `browser` (playwright chromium, *.test.tsx)
├── components.json                      # shadcn CLI config (base-nova, olive baseColor, @diceui registry)
├── eslint.config.mjs                    # Next Core Web Vitals + TS + Prettier + react-hooks rules
├── .prettierrc.mjs                      # Import order (@trivago/prettier-plugin-sort-imports) + prettier-plugin-tailwindcss
├── .lintstagedrc.mjs                    # Staged ESLint, Prettier, and `pnpm typecheck`
├── @schemas/                            # Zod validation schemas & unit tests (auth, profile, roles, users)
├── @types/                              # Shared TypeScript interfaces (`AuthUser`)
├── __tests__/test-utils.tsx             # Custom React 19 render(), makeTestQueryClient(), cleanup()
├── api/                                 # 18 registered React Query / Mutation endpoint definitions
├── app/
│   ├── _shell/                          # RootShell (<html>, Roboto & Share Tech Mono fonts, Providers), AppleSplashScreens
│   └── (app)/                           # Main route group
│       ├── layout.tsx                   # Metadata, viewport, delegates to RootShell
│       ├── (authentication)/            # Login route group (`/login`)
│       └── (dashboard)/                 # DashboardLayout routes (`/`, `/users`, `/roles`, `/profile`)
├── components/
│   ├── breadcrumb/                      # DashboardBreadcrumb (auto URL segments + back button + title/actions)
│   ├── dashboard-layout/                # SidebarProvider + AppSidebar + SidebarInset + Navbar
│   ├── data-table/                      # Full TanStack Table v9 suite (components/, lib/, @types/, __tests__/)
│   ├── full-page-load/                  # Centered Skyeye spinner overlay
│   ├── theme-toggle/                    # Light/dark theme button
│   ├── toast/                           # ToastContainer & Toaster bridge to toastStore
│   └── ui/                              # 25 shadcn/Base UI & DiceUI components
├── configs/auth.config.ts               # sessionCookie (__Secure-DroneVision.Refresh), defaultRedirect (/), route sets
├── constant/sidebar.ts                  # Sidebar navigation items + permission requirements
├── core/
│   ├── auth/                            # PERMISSIONS, PERMISSION_GROUPS, hasPermission, hasAnyPermission, hasAllPermissions
│   ├── client/                          # Client, EndpointCaller (CSRF + Zod + 401 dispatch), auth-handler, getQueryClient
│   └── errors/                          # AppError, ApiError, ValidationError, ensureError
├── features/
│   ├── guards/auth-guard/               # Client AuthGuard (verifies /auth/me & RBAC permissions)
│   ├── navigation/                      # AppSidebar, Navbar, AccountMenu
│   ├── pages/                           # Login, Profile, Roles, Users feature modules
│   └── providers/                       # Providers (Theme, QueryClient, Nuqs, Tooltip, Toaster) & UnauthorizedListener
├── hooks/                               # useAuth, useDataTable, useDeleteConfirmation, useModalAbort, useUpdateProfile, useIsMobile
├── lib/utils.ts                         # Re-exports cn
├── public/                              # Icons (192/512/maskable), favicons, 49 iOS splash screens, login MQ-9 Reaper video/poster
├── stores/toast.ts                      # Vanilla Zustand toastStore & useToast()
└── styles/globals.css                   # Tailwind v4 @theme inline + :root / .dark OKLCH semantic tokens
```

---

## 3. Environment Variables, Ports & Configuration

| Variable / Config | Value / Default | Source | Description |
| :--- | :--- | :--- | :--- |
| `PORT` | `3000` | `frontend/Dockerfile` (`runtime`) | Internal listening port (`0.0.0.0:3000`, proxied behind Ocelot at `:8443`) |
| `HOSTNAME` | `0.0.0.0` | `frontend/Dockerfile` (`runtime`) | Standalone Next.js bind host |
| `NODE_ENV` | `development` / `production` | `compose.dev.yaml` / `frontend/compose.yaml` | Controls Next.js runtime mode |
| `NEXT_TELEMETRY_DISABLED` | `1` | `frontend/Dockerfile` | Disables Next.js telemetry in container builds |

Frontend consumes **zero** `NEXT_PUBLIC_*` API base URL env vars—every API endpoint in `frontend/api/*` uses same-origin relative URLs (`/api/management/...`).

---

## 4. Registered API Endpoints (`frontend/api/`)

All endpoints register with `client.registerEndpoint()` on the `getQueryClient()` singleton and execute through `EndpointCaller` (which fetches `GET /api/management/auth/csrf` and attaches `X-CSRF-TOKEN` + `credentials: 'same-origin'` on every call):

| Module | Type | Method & URL | Query / Mutation Key | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `getMeQuery.ts` | Query | `GET /api/management/auth/me` | `['auth', 'me']` | `staleTime: 5m`, `refetchOnWindowFocus: true`, `skipUnauthorizedHandling: true` |
| `loginMutation.ts` | Mutation | `POST /api/management/auth/login` | `['auth', 'login']` | Maps `{ username, password }` → `{ userName, password }`; `skipUnauthorizedHandling: true` |
| `logoutMutation.ts` | Mutation | `POST /api/management/auth/logout` | `['auth', 'logout']` | Revokes session & clears cookie |
| `getUsersQuery.ts` | Query | `GET /api/management/users` | `['users', 'list', params]` | Params: `Role?`, `Name?`, `CreatedAtFrom?`, `CreatedAtTo?`, `Page`, `PageSize` |
| `getUserQuery.ts` | Query | `GET /api/management/users/{id}` | `['users', 'detail', { id }]` | `omitFromBody: ['id']` |
| `createUserMutation.ts` | Mutation | `POST /api/management/users` | `['users', 'create']` | Validated by `createUserSchema` |
| `updateUserMutation.ts` | Mutation | `PUT /api/management/users/{id}` | `['users', 'update']` | `omitFromBody: ['id']` |
| `deleteUserMutation.ts` | Mutation | `DELETE /api/management/users/{id}` | `['users', 'delete']` | `omitFromBody: ['id']` |
| `getRolesQuery.ts` | Query | `GET /api/management/roles` | `['roles', 'list', params]` | Params: `RoleName?`, `CreatedAtFrom?`, `CreatedAtTo?`, `Page`, `PageSize` |
| `getRoleQuery.ts` | Query | `GET /api/management/roles/{id}` | `['roles', 'detail', { id }]` | `omitFromBody: ['id']` |
| `createRoleMutation.ts` | Mutation | `POST /api/management/roles` | `['roles', 'create']` | Validated by `roleSchema` |
| `updateRoleMutation.ts` | Mutation | `PUT /api/management/roles/{id}` | `['roles', 'update']` | `omitFromBody: ['id']` |
| `deleteRoleMutation.ts` | Mutation | `DELETE /api/management/roles/{id}` | `['roles', 'delete']` | `omitFromBody: ['id']` |
| `getProfileQuery.ts` | Query | `GET /api/management/profile` | `['profile']` | Current user's profile without permissions array |
| `updateProfileMutation.ts` | Mutation | `PUT /api/management/profile` | `['profile', 'update']` | Validated by `updateProfileSchema` |
| `uploadProfileImageMutation.ts` | Mutation | `POST /api/management/profile-image` | `['profile', 'image', 'upload']` | Sends `FormData` with field `Image` |
| `deleteProfileImageMutation.ts` | Mutation | `DELETE /api/management/profile-image` | `['profile', 'image', 'delete']` | Deletes own avatar |
| `uploadUserProfileImageMutation.ts` | Mutation | `POST /api/management/users/{id}/profile-image` | `['users', 'profile-image', 'upload']` | Admin upload (`FormData` `Image`) |
| `deleteUserProfileImageMutation.ts` | Mutation | `DELETE /api/management/users/{id}/profile-image` | `['users', 'profile-image', 'delete']` | Admin delete target avatar |

---

## 5. Run, Build, Lint & Test Commands

Run from `frontend/` or from root using `pnpm --filter frontend <script>`:

```powershell
# Start Next.js dev server (port 3000)
pnpm --filter frontend dev

# Build standalone production output & verify routes
pnpm --filter frontend build

# Run full static quality gate (ESLint + Prettier check + TypeScript check)
pnpm --filter frontend check

# Individual quality checks / auto-fixes
pnpm --filter frontend lint
pnpm --filter frontend lint:fix
pnpm --filter frontend format
pnpm --filter frontend format:check
pnpm --filter frontend typecheck

# Run Vitest suite (watch mode / single run)
pnpm --filter frontend test
pnpm --filter frontend exec vitest run
```
