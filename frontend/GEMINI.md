# GEMINI.md — Frontend Architecture & Guidelines

## 1. Tech Stack & `package.json` Overview

- **Package Manager:** `pnpm` (`12.16.1`, monorepo workspace member `frontend`) — always use `pnpm`, never `npm` or `yarn`.
- **Framework:** Next.js `16.4.6` (App Router, `output: 'standalone'`, `cacheComponents: true`, `partialPrefetching: true`) with React `19.3.0`.
- **Language:** TypeScript `^5` (`strict: true`, path aliases `@api/*`, `@components/*`, `@configs/*`, `@core/*`, `@features/*`, `@hooks/*`, `@schemas`, `@stores/*`, `@styles/*`, `@types`, `@/*`).
- **Styling & UI Primitives:** Tailwind CSS `^4` (`@tailwindcss/turbopack`), `tw-animate-css` `^1.4.0`, `shadcn` `^4.23.0` (`base-nova` style) built on `@base-ui/react` `^1.9.0`, `class-variance-authority` `^0.7.1`, `cn` `^0.5.1`, `next-themes` `^0.4.6`, `lucide-react` `^1.66.0`.
- **Data Fetching, State & Forms:** `@tanstack/react-query` `^5.105.1`, `zustand` `^5.1.0` (vanilla store + React bindings), `react-hook-form` `^7.91.0`, `@hookform/resolvers` `^7.0.1`, `zod` `^4.6.7`.
- **Linting & Formatting:** ESLint `^9` (`eslint-config-next/core-web-vitals`, `eslint-config-next/typescript`, `eslint-config-prettier`) and Prettier `^3.9.10` (`@trivago/prettier-plugin-sort-imports`, `prettier-plugin-tailwindcss`).

### Key Scripts (`frontend/package.json`)

| Script              | Command                                                   | Purpose                                              |
| :------------------ | :-------------------------------------------------------- | :--------------------------------------------------- |
| `pnpm dev`          | `next dev`                                                | Start local Next.js dev server on port 3000          |
| `pnpm build`        | `next build`                                              | Build standalone production bundle and verify routes |
| `pnpm start`        | `next start`                                              | Serve production Next.js build                       |
| `pnpm preview`      | `pnpm build && pnpm start`                                | Build and run production bundle locally              |
| `pnpm typecheck`    | `tsc --noEmit`                                            | Run strict TypeScript typechecking                   |
| `pnpm lint`         | `eslint . --ext .js,.jsx,.ts,.tsx,.mjs`                   | Run ESLint (Core Web Vitals, TS, React Hooks)        |
| `pnpm lint:fix`     | `eslint . --ext .js,.jsx,.ts,.tsx,.mjs --fix`             | Auto-fix lint issues                                 |
| `pnpm format`       | `prettier --write "**/*.{js,jsx,ts,tsx,mjs,json,css,md}"` | Format code, sort imports, and sort Tailwind classes |
| `pnpm format:check` | `prettier --check "**/*.{js,jsx,ts,tsx,mjs,json,css,md}"` | Verify formatting without modifying files            |
| `pnpm check`        | `pnpm lint && pnpm format:check && pnpm typecheck`        | Full static quality gate                             |

---

## 2. Directory Structure

```text
frontend/
├── @schemas/                    # Zod validation schemas & inferred types (auth.ts, index.ts)
├── @types/                      # Shared TypeScript interfaces (auth.ts, index.ts)
├── api/                         # Registered TanStack Query/Mutation endpoint definitions
│   ├── getMeQuery.ts            # GET /api/management/auth/me
│   ├── loginMutation.ts         # POST /api/management/auth/login
│   └── logoutMutation.ts        # POST /api/management/auth/logout
├── app/
│   ├── _shell/                  # Root <html>/<body> shell, Google fonts, metadata, viewport, splash
│   └── (app)/                   # Main application route group
│       ├── (authentication)/    # Public auth routes (login/page.tsx, layout.tsx)
│       ├── (dashboard)/         # Authenticated shell routes (page.tsx, layout.tsx)
│       └── layout.tsx           # RootLayout delegating to RootShell
├── components/
│   ├── dashboard-layout/        # SidebarProvider + AppSidebar + SidebarInset + Navbar
│   ├── full-page-load/          # Full-viewport loading state used by AuthGuard
│   ├── toast/                   # Toast component & Toaster portal subscribed to toastStore
│   └── ui/                      # Base UI + shadcn (base-nova) primitives (button, input, field, etc.)
├── configs/
│   └── auth.config.ts           # Cookie name (__Secure-DroneVision.Refresh) & route sets
├── constant/
│   └── sidebar.ts               # Sidebar navigation items & Lucide icons
├── core/
│   ├── client/                  # Centralized API client & React Query integration
│   │   ├── @types/index.ts      # EndpointOptions, QueryKeyGenerator, MutationKeyGenerator
│   │   ├── auth-handler.ts      # Unauthorized callback registry & beginSignout() one-shot latch
│   │   ├── endpoint-caller.ts   # CSRF fetch, Zod validation, normalizers, 401 dispatch
│   │   ├── get-query-client.ts  # SSR-safe QueryClient singleton (browser) / per-request (server)
│   │   ├── index.ts             # Client.registerEndpoint() -> setQueryDefaults / setMutationDefaults
│   │   └── provider.tsx         # QueryClientProvider wrapper using getQueryClient()
│   └── errors/                  # AppError, ApiError, ValidationError, ensureError
├── features/
│   ├── guards/auth-guard/       # Client-side AuthGuard wrapping protected pages
│   ├── navigation/              # AppSidebar, Navbar, AccountMenu
│   ├── pages/login/             # Login page feature (LoginForm, LoginHeader, LoginVideo)
│   └── providers/               # ThemeProvider, ClientProvider, TooltipProvider, Toaster, UnauthorizedListener
├── hooks/
│   ├── use-auth.tsx             # Auth state hook (getMe query, logout mutation, updateUser)
│   └── use-mobile.ts            # Responsive breakpoint hook
├── lib/
│   └── utils.ts                 # Classname utilities
├── stores/
│   └── toast.ts                 # Vanilla Zustand toastStore + useToast() hook
├── styles/
│   └── globals.css              # Tailwind v4 @theme inline + :root/.dark OKLCH tokens
├── components.json              # shadcn CLI config (base-nova, Base UI, olive baseColor)
├── next.config.ts               # Standalone output, turbopack CSS rule, cacheComponents
├── proxy.ts                     # Next.js 16 edge request proxy for cookie-based route redirects
└── tsconfig.json                # Strict TS config with path aliases
```

---

## 3. Core Files & Responsibilities

- `proxy.ts`: Next.js 16 request proxy (`export function proxy`, replacing `middleware.ts`). Checks presence of `__Secure-DroneVision.Refresh` cookie to redirect authenticated users away from `/login` (`authConfig.defaultRedirect` = `/`) and unauthenticated users to `/login?rp=...`. Cookie presence only controls navigation; the Management API validates sessions.
- `configs/auth.config.ts`: Defines `sessionCookie: '__Secure-DroneVision.Refresh'`, `defaultRedirect: '/'`, `routes.public: ['/login']`, and `routes.auth: ['/login']`.
- `core/client/endpoint-caller.ts`: Executes all API calls. Before **every** request, fetches `GET /api/management/auth/csrf` (`credentials: 'same-origin'`, `cache: 'no-store'`) and attaches `X-CSRF-TOKEN`. Validates request/response payloads with Zod, runs `requestNormalizer`/`responseNormalizer`, converts non-OK responses to `ApiError`, and calls `triggerUnauthorized()` on `401` unless `skipUnauthorizedHandling: true`.
- `core/client/index.ts`: `client.registerEndpoint(keyGenerator, options)` registers `queryFn` or `mutationFn` directly on the `getQueryClient()` singleton via `setQueryDefaults` / `setMutationDefaults`.
- `core/client/auth-handler.ts`: Holds the global `UnauthorizedHandler` and `beginSignout()` one-shot latch so concurrent `401`s, `AuthGuard`, and manual `logout()` never stack duplicate toasts or redirects.
- `features/providers/unauthorized-listener.tsx`: Registers the global `401` handler. On unauthorized, acquires `beginSignout()`, calls `POST /api/management/auth/logout` to clear the invalid cookie before redirecting, triggers an error toast via `toastStore.getState()`, and performs a hard navigation (`window.location.href = '/login'`).
- `features/guards/auth-guard/index.tsx`: Wraps protected pages. Consumes `useAuth()`; triggers `logout()` if `isUnauthorized`, renders a retry view on non-401 errors, and renders `<FullPageLoader />` while loading or unauthenticated.
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
