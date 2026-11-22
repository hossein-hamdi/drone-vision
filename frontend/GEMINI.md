# GEMINI.md — Frontend Architecture & Guidelines

## 1. Tech Stack & `package.json` Overview

- **Package Manager:** `pnpm` (`12.16.1`, monorepo workspace member `frontend`) — always use `pnpm`, never `npm` or `yarn`.
- **Framework:** Next.js `16.4.6` (App Router, `output: 'standalone'`, `cacheComponents: true`, `partialPrefetching: true`) with React `19.3.0`.
- **Language:** TypeScript `^5` (`strict: true`, path aliases `@api/*`, `@components/*`, `@configs/*`, `@core/*`, `@features/*`, `@hooks/*`, `@schemas`, `@stores/*`, `@styles/*`, `@types`, `@/*`).
- **Styling & UI Primitives:** Tailwind CSS `^4` (`@tailwindcss/turbopack`), `tw-animate-css` `^1.4.0`, `shadcn` `^4.23.0` (`base-nova` style) built on `@base-ui/react` `^1.9.0`, `class-variance-authority` `^0.7.1`, `cn` `^0.5.1`, `next-themes` `^0.4.6`, `lucide-react` `^1.66.0`.
- **Data Fetching, Tables, State & Forms:** `@tanstack/react-query` `^5.105.1`, `@tanstack/react-table` `9.3.1`, `nuqs` `^2.10.3`, `zustand` `^5.1.0` (vanilla store + React bindings), `react-hook-form` `^7.91.0`, `@hookform/resolvers` `^7.0.1`, `zod` `^4.6.7`.
- **Testing, Linting & Formatting:** Vitest `^5.1.1` (`node` unit tests + `@vitest/browser-playwright` Chromium browser tests), ESLint `^9` (`eslint-config-next/core-web-vitals`, `eslint-config-next/typescript`, `eslint-config-prettier`), and Prettier `^3.9.10` (`@trivago/prettier-plugin-sort-imports`, `prettier-plugin-tailwindcss`).

### Key Scripts (`frontend/package.json`)

| Script              | Command                                                   | Purpose                                                |
| :------------------ | :-------------------------------------------------------- | :----------------------------------------------------- |
| `pnpm dev`          | `next dev`                                                | Start local Next.js dev server on port 3000            |
| `pnpm build`        | `next build`                                              | Build standalone production bundle and verify routes   |
| `pnpm start`        | `next start`                                              | Serve production Next.js build                         |
| `pnpm preview`      | `pnpm build && pnpm start`                                | Build and run production bundle locally                |
| `pnpm typecheck`    | `tsc --noEmit`                                            | Run strict TypeScript typechecking                     |
| `pnpm test`         | `vitest`                                                  | Run Vitest unit (`*.test.ts`) & browser (`*.test.tsx`) |
| `pnpm lint`         | `eslint . --ext .js,.jsx,.ts,.tsx,.mjs`                   | Run ESLint (Core Web Vitals, TS, React Hooks)          |
| `pnpm lint:fix`     | `eslint . --ext .js,.jsx,.ts,.tsx,.mjs --fix`             | Auto-fix lint issues                                   |
| `pnpm format`       | `prettier --write "**/*.{js,jsx,ts,tsx,mjs,json,css,md}"` | Format code, sort imports, and sort Tailwind classes   |
| `pnpm format:check` | `prettier --check "**/*.{js,jsx,ts,tsx,mjs,json,css,md}"` | Verify formatting without modifying files              |
| `pnpm check`        | `pnpm lint && pnpm format:check && pnpm typecheck`        | Full static quality gate                               |

---

## 2. Directory Structure

```text
frontend/
├── __tests__/                   # Shared test helpers (test-utils.tsx: makeTestQueryClient, render, cleanup)
├── @schemas/                    # Zod validation schemas & inferred types (auth, profile, roles, users, index.ts)
├── @types/                      # Shared TypeScript interfaces (auth.ts, index.ts)
├── api/                         # Registered TanStack Query/Mutation endpoint definitions (*Query.ts, *Mutation.ts)
├── app/
│   ├── _shell/                  # Root <html>/<body> shell, Google fonts, metadata, viewport, splash
│   └── (app)/                   # Main application route group
│       ├── (authentication)/    # Public auth routes (login/page.tsx, layout.tsx)
│       ├── (dashboard)/         # Authenticated shell routes (page.tsx, profile, users, roles, layout.tsx)
│       └── layout.tsx           # RootLayout delegating to RootShell
├── components/
│   ├── breadcrumb/              # DashboardBreadcrumb (auto pathname breadcrumbs, Back button, title, icon, actions)
│   ├── dashboard-layout/        # SidebarProvider + AppSidebar + SidebarInset + Navbar
│   ├── data-table/              # Reusable TanStack Table v9 + nuqs URL-synced data table & helpers (lib/)
│   ├── full-page-load/          # Full-viewport loading state used by AuthGuard
│   ├── theme-toggle/            # Light/dark theme switcher
│   ├── toast/                   # Toast component & Toaster portal subscribed to toastStore
│   └── ui/                      # Base UI + shadcn (base-nova) primitives (button, dialog, input, field, etc.)
├── configs/
│   └── auth.config.ts           # Cookie name (__Secure-DroneVision.Refresh) & public/auth route sets
├── constant/
│   └── sidebar.ts               # Sidebar navigation items, Lucide icons, and optional permissions
├── core/
│   ├── auth/                    # Static PERMISSIONS, PERMISSION_GROUPS, hasPermission / hasAnyPermission / hasAllPermissions
│   ├── client/                  # Centralized API client, EndpointCaller (CSRF, Zod, 401), auth-handler, QueryClient
│   └── errors/                  # AppError, ApiError, ValidationError, ensureError
├── features/
│   ├── guards/auth-guard/       # Client-side AuthGuard (auth + optional permission check) wrapping protected pages
│   ├── navigation/              # AppSidebar, Navbar, AccountMenu
│   ├── pages/                   # Page feature modules (login/, profile/, roles/, users/)
│   └── providers/               # ThemeProvider, ClientProvider, NuqsAdapter, TooltipProvider, Toaster
├── hooks/                       # use-auth, use-data-table, use-delete-confirmation, use-modal-abort, use-mobile, use-update-profile
├── lib/utils.ts                 # Classname utilities
├── stores/toast.ts              # Vanilla Zustand toastStore + useToast() hook
├── styles/globals.css           # Tailwind v4 @theme inline + :root/.dark OKLCH tokens
├── components.json              # shadcn CLI config (base-nova, Base UI, olive baseColor)
├── next.config.ts               # Standalone output, turbopack CSS rule, cacheComponents
├── proxy.ts                     # Next.js 16 edge request proxy for cookie-based route redirects
├── tsconfig.json                # Strict TS config with path aliases
└── vitest.config.mjs            # Vitest unit (node) & browser (Playwright Chromium) projects
```

---

## 3. Core Files & Responsibilities

- `proxy.ts`: Next.js 16 request proxy (`export function proxy`, replacing `middleware.ts`). Checks presence of `__Secure-DroneVision.Refresh` cookie to redirect authenticated users away from `/login` (`authConfig.defaultRedirect` = `/`) and unauthenticated users to `/login?rp=...`. Cookie presence only controls navigation; the Management API validates sessions.
- `configs/auth.config.ts`: Defines `sessionCookie: '__Secure-DroneVision.Refresh'`, `defaultRedirect: '/'`, `routes.public: ['/login']`, and `routes.auth: ['/login']`.
- `core/auth/permissions.ts` & `core/auth/index.ts`: Defines `PERMISSIONS`, `PERMISSION_GROUPS`, `Permission`, and permission helpers (`hasPermission`, `hasAnyPermission`, `hasAllPermissions`) consumed by `AuthGuard`, `AppSidebar`, and `useAuth()`.
- `core/client/endpoint-caller.ts`: Executes all API calls. Before **every** request, fetches `GET /api/management/auth/csrf` (`credentials: 'same-origin'`, `cache: 'no-store'`) and attaches `X-CSRF-TOKEN`. Validates request/response payloads with Zod, runs `requestNormalizer`/`responseNormalizer`, converts non-OK responses to `ApiError`, and calls `triggerUnauthorized()` on `401` unless `skipUnauthorizedHandling: true`.
- `core/client/index.ts`: `client.registerEndpoint(keyGenerator, options)` registers `queryFn` or `mutationFn` directly on the `getQueryClient()` singleton via `setQueryDefaults` / `setMutationDefaults`.
- `core/client/auth-handler.ts`: Holds the global `UnauthorizedHandler` and `beginSignout()` one-shot latch so concurrent `401`s, `AuthGuard`, and manual `logout()` never stack duplicate toasts or redirects.
- `features/providers/unauthorized-listener.tsx`: Registers the global `401` handler. On unauthorized, acquires `beginSignout()`, calls `POST /api/management/auth/logout` to clear the invalid cookie before redirecting, triggers an error toast via `toastStore.getState()`, and performs a hard navigation (`window.location.href = '/login'`).
- `features/guards/auth-guard/index.tsx`: Wraps protected pages. Consumes `useAuth()` and optional `permission?: Permission`; triggers `logout()` if `isUnauthorized`, redirects to `/` via `router.replace('/')` if forbidden, renders a retry view on non-401 errors, and renders `<FullPageLoader />` while loading or unauthenticated.
- `components/breadcrumb/index.tsx`: `DashboardBreadcrumb` builds breadcrumb links from `usePathname()`, renders a Back button on nested routes, and displays page `title`, `description`, `icon`, and optional `actions`.
- `components/data-table/` & `hooks/use-data-table.ts`: Provides `DataTable`, `DataTableToolbar`, `DataTableColumnHeader`, `getDataTableActionsColumn`, and URL-backed table/filter state via `nuqs` (`buildPagedQueryParams`, `getColumnFilterParser`, `getDateFilterRange`).
- `hooks/use-modal-abort.ts` & `hooks/use-delete-confirmation.ts`: Manage `AbortController` lifecycles (`createSignal`, `abort`, `isAbortError`) for create/edit/delete modals so closing a dialog aborts in-flight requests cleanly.
- `stores/toast.ts`: Vanilla Zustand store (`createStore` from `zustand/vanilla`) so both React components (`useToast()`) and non-React modules (`toastStore.getState().showToast(...)`) can dispatch notifications.
- `styles/globals.css`: Defines semantic OKLCH CSS variables under `:root` and `.dark`, mapped into Tailwind v4 via `@theme inline` and `@custom-variant dark (&:is(.dark *));`.

---

## 4. Code Snapshots & UI Conventions

### Endpoint Registration & React Query Usage (`@api/*`)

Endpoints are registered once at module load on the shared `QueryClient` and consumed by key generators—never call `fetch` directly inside components or hooks.

```tsx
// CORRECT — Register endpoint in frontend/api/loginMutation.ts
import { z } from 'zod';

import { loginSchema, type LoginSchema } from '@schemas';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const responseSchema = z.object({
  accessToken: z.string(),
  tokenType: z.string(),
  expiresIn: z.number(),
  refreshTokenExpiresAt: z.iso.datetime({ offset: true }),
});

const keyGenerator: MutationKeyGenerator = () => ['auth', 'login'];

client.registerEndpoint<LoginSchema, z.infer<typeof responseSchema>>(keyGenerator, {
  url: '/api/management/auth/login',
  method: 'POST',
  type: 'mutation',
  requestDataSchema: loginSchema,
  requestNormalizer: ({ username, password }) => ({ userName: username, password }),
  responseSchema,
  skipUnauthorizedHandling: true, // Surface 401 invalid credentials to form instead of global sign-out
});

// WRONG — Calling fetch() directly in a component (skips CSRF token, Zod validation, and 401 handling)
await fetch('/api/management/auth/login', { method: 'POST', body: JSON.stringify(data) });
```

### Base UI Composition (`render` prop, NOT `asChild`)

This project uses `shadcn` `base-nova` backed by `@base-ui/react`, not Radix UI. Compose triggers/links with Base UI's `render` prop, never `asChild`.

```tsx
// CORRECT — Base UI render prop (from features/navigation/components/account-menu.tsx)
<DropdownMenuTrigger
  render={<Button variant="ghost" className="h-auto gap-3 px-2 py-1.5" />}
  aria-label="Open account menu"
>
  ...
</DropdownMenuTrigger>
<DropdownMenuItem render={<Link href="/profile" />}>
  <UserRound aria-hidden="true" />
  Profile
</DropdownMenuItem>

// WRONG — Radix pattern; Base UI primitives do not accept asChild
<DropdownMenuTrigger asChild>
  <Button variant="ghost">Account</Button>
</DropdownMenuTrigger>
```

### Forms with React Hook Form, Zod, and `Field` Primitives

Use `Controller` from `react-hook-form` with `zodResolver` and `@components/ui/field` (`Field`, `FieldGroup`, `FieldLabel`, `FieldError`) for accessible `aria-invalid` and `aria-describedby` wiring.

```tsx
// CORRECT — Accessible form field pattern (from features/pages/login/components/login-form.tsx)
<Controller
  name="username"
  control={form.control}
  render={({ field, fieldState }) => (
    <Field data-invalid={fieldState.invalid}>
      <FieldLabel htmlFor="login-username">Username</FieldLabel>
      <Input
        {...field}
        id="login-username"
        aria-invalid={fieldState.invalid}
        aria-describedby={fieldState.invalid ? 'login-username-error' : undefined}
      />
      {fieldState.invalid && <FieldError id="login-username-error" errors={[fieldState.error]} />}
    </Field>
  )}
/>
```

### Styling & Theme Tokens

Use semantic tokens (`bg-background`, `text-foreground`, `bg-muted/40`, `border-border`, `text-muted-foreground`, `bg-primary`, `text-destructive`) defined in `styles/globals.css`. Avoid hardcoded hex/RGB literals in components.

### ESLint & Import Order Rules

- Enforce `consistent-type-imports` (`import type { ... }`), no `var`, no nested ternaries, and `no-console` (only `console.warn` / `console.error` allowed).
- Import order is enforced by `@trivago/prettier-plugin-sort-imports` in `.prettierrc.mjs`: `node:` → `react` → `react-dom` → `next` → third-party → `@types` → `@schemas` → `@configs` → `@constants` → `@core/*` → `@features/*` → `@components/*` → `@hooks/*` → `@stores/*` → `@utils` → `@styles/*` → `@/*` → relative → CSS.

---

## 5. Authentication & Authorization Management (Frontend)

- **Transport Model:** Same-origin `HttpOnly`, `Secure`, `SameSite=Strict` refresh cookie (`__Secure-DroneVision.Refresh`) proxied through the Ocelot gateway at `/api/management/*`. The frontend **never** stores JWTs or refresh tokens in `localStorage`, `sessionStorage`, or JS memory for API auth; `credentials: 'same-origin'` authenticates all requests.
- **Navigation vs Session Authority:** `proxy.ts` only checks whether `__Secure-DroneVision.Refresh` exists to gate `/login` vs protected routes. Actual session validity is verified by `GET /api/management/auth/me` via `useAuth()` inside `<AuthGuard>`.
- **Login & Logout Transitions:** Always use **hard navigation** (`window.location.replace(authConfig.defaultRedirect)` on login; `window.location.href = '/login'` on logout/401) rather than `router.push()`, so `proxy.ts` re-evaluates the updated `HttpOnly` cookie server-side and all in-memory TanStack Query cache is wiped cleanly.
- **One-Shot Signout Latch:** Multiple paths (`AuthGuard`, `UnauthorizedListener`, manual `logout()`) can detect a dead session simultaneously. Always guard signout transitions with `if (!beginSignout()) return;`.

---

## 6. Core Business Logic & Frontend Invariants

- **Singleton QueryClient Requirement:** `client.registerEndpoint` mutates `getQueryClient()` defaults. `core/client/provider.tsx` **must** pass `getQueryClient()` (never `new QueryClient()` inside a component), otherwise `useQuery`/`useMutation` observers will not see registered `queryFn`/`mutationFn` defaults.
- **Non-React Toast Dispatch:** Always read `toastStore.getState().showToast(...)` outside React components (e.g., `UnauthorizedListener`) and `useToast()` inside components.
- **CSRF Pre-flight:** `EndpointCaller` fetches `/api/management/auth/csrf` before every request. Never bypass `EndpointCaller` for Management API calls.

---

## 7. Step-by-Step Guide: Creating a New Page

Follow these steps in order whenever creating a new page in `frontend/`:

1. **Determine Route Group, Layout & Backend Contract**
   - Choose **`app/(app)/(dashboard)/<route>/`** for authenticated pages inside `DashboardLayout` (`AppSidebar` + `Navbar`) or **`app/(app)/(authentication)/<route>/`** for standalone/public pages inside `<main>`.
   - Identify all upstream endpoints (gateway routes like `/api/management/*` or `/api/checker/*`), request/response DTOs, query/pagination parameters, and required permission claims.

2. **Add Validation Schemas & Types (`@schemas/`, `@types/`)**
   - Create `frontend/@schemas/<feature>.ts` with `zod` schemas and inferred types (`export type ... = z.infer<typeof ...>`), then re-export them from `frontend/@schemas/index.ts`.
   - Add shared cross-feature TypeScript types in `frontend/@types/` if needed.

3. **Register API Endpoints (`api/`)**
   - Create one module per endpoint (`get<Entity>Query.ts`, `get<Entities>Query.ts`, `create<Entity>Mutation.ts`, `update<Entity>Mutation.ts`, `delete<Entity>Mutation.ts`).
   - Define `requestDataSchema` and `responseSchema` with `zod`, export the key generator (`QueryKeyGenerator` / `MutationKeyGenerator`) and request/response types, and register via `client.registerEndpoint(keyGenerator, options)`.
   - For path-param mutations, use `url: ({ id }) => ...` with `omitFromBody: ['id']` and include `& { signal?: AbortSignal }` on `RequestData` so dialogs can abort in-flight mutations. Never call `fetch()` directly in components or hooks.

4. **Register Permissions (if access-restricted) (`core/auth/permissions.ts`)**
   - Add any new permission strings to `PERMISSIONS` and `PERMISSION_GROUPS` in `frontend/core/auth/permissions.ts` to match the backend permission catalog.

5. **Build the Feature Page Module (`features/pages/<page-name>/`)**
   - Create `frontend/features/pages/<page-name>/index.tsx` exporting `<Name>Page` (named + default export) and place page-specific components under `components/` (`*-table.tsx`, `columns.tsx`, `create-*-dialog.tsx`, `edit-*-dialog.tsx`, `delete-*-dialog.tsx`, `*-form-fields.tsx`).
   - Add `'use client'` on interactive feature entrypoints and subcomponents.
   - Wrap authenticated pages in `<AuthGuard>` or `<AuthGuard permission={PERMISSIONS.<resource>.view}>`, and gate create/edit/delete controls with `const { can } = useAuth()`.
   - Start dashboard pages with `<DashboardBreadcrumb title="..." description="..." icon={<Icon />} actions={...} />` inside `<div className="flex flex-col gap-6">` (or `<section>`). `DashboardBreadcrumb` automatically generates breadcrumbs and a Back button from `usePathname()`.
   - **Tables:** Sync URL pagination/filters via `useQueryStates` (`nuqs`) + `getColumnFilterParser`, build params with `buildPagedQueryParams` and `getDateFilterRange`, fetch with `useQuery({ queryKey: ..., placeholderData: keepPreviousData })`, and compose `useDataTable` + `<DataTable>` + `<DataTableToolbar>` + `DataTableColumnHeader` / `getDataTableActionsColumn`.
   - **Forms, Dialogs & States:** Use `react-hook-form` (`zodResolver`) + `Controller` + `@components/ui/field`, Base UI `render` props (never `asChild`), semantic theme tokens (`bg-card`, `border-border`, `text-foreground`, `text-muted-foreground`), `useModalAbort` (`isAbortError`) and `useDeleteConfirmation` for cancellation/deletion, `queryClient.invalidateQueries(...)`, `useToast().showToast(...)`, `Skeleton` loading states, and error views with a retry button.

6. **Create the App Router Route (`app/`)**
   - Add `frontend/app/(app)/(dashboard)/<route>/page.tsx` (or `(authentication)/<route>/page.tsx`) as a thin Server Component wrapper importing and returning `<NamePage />` from `@features/pages/<page-name>`.
   - Export `metadata: Metadata` from `page.tsx` or a route `layout.tsx` when a custom document title is needed.

7. **Wire Navigation & Route Access (`constant/sidebar.ts`, `configs/auth.config.ts`)**
   - Add sidebar destinations (`{ title, url, icon, permission? }`) to `sidebar` in `frontend/constant/sidebar.ts`; `AppSidebar` filters links via `can(item.permission)`.
   - If the route is public or auth-only, add its path to `authConfig.routes.public` (and `authConfig.routes.auth` if authenticated users should be redirected to `/`) in `frontend/configs/auth.config.ts` so `proxy.ts` enforces the right redirects.

8. **Write Tests & Verify**
   - Add unit tests (`*.test.ts`, e.g., `@schemas/__tests__/<feature>.test.ts`) and Vitest browser component tests (`*.test.tsx` in `features/pages/<page-name>/__tests__/`, using `makeTestQueryClient`, `render`, `cleanup` from `@/__tests__/test-utils` and `NuqsTestingAdapter` when using `nuqs`).
   - From `frontend/`, run `pnpm format`, `pnpm check` (`pnpm lint && pnpm format:check && pnpm typecheck`), `pnpm test -- --run`, and `pnpm build`.
