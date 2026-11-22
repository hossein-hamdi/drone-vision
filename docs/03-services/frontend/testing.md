# Frontend — Testing Guide

---

## 1. Test Runner & Dual-Project Setup (`vitest.config.mjs`)

`frontend/` uses **Vitest `5.1.1`** split into two configured test projects in [`frontend/vitest.config.mjs`](../../../frontend/vitest.config.mjs):

| Project Name | Environment | File Pattern | Provider / Purpose |
| :--- | :--- | :--- | :--- |
| **`unit`** | `node` | `**/*.test.ts` | Pure TypeScript tests: Zod schemas, permission helpers, `EndpointCaller`, DataTable date/query utilities, Zustand `toastStore` |
| **`browser`** | Real Chromium (`headless: true`) | `**/*.test.tsx` via `@vitest/browser-playwright` (`playwright`) | Real browser DOM & user interaction tests (`page`, `userEvent` from `vitest/browser`) for React 19 components |

### First-Time Browser Setup

Before running the `browser` project for the first time, install Playwright's Chromium binary:

```powershell
pnpm --filter frontend exec playwright install chromium
```

---

## 2. Test Utilities & Mocking Patterns (`__tests__/test-utils.tsx`)

### Custom Render Harness
[`frontend/__tests__/test-utils.tsx`](../../../frontend/__tests__/test-utils.tsx) provides:
- `makeTestQueryClient()`: Creates a fresh `QueryClient` per test with `queries: { retry: false, queryFn: () => new Promise<never>(() => {}) }` and `mutations: { retry: false }` so unmocked queries stay cleanly in a pending state without triggering `No queryFn was passed` warnings.
- `render(ui, { queryClient })`: Mounts into a real DOM container via React 19 `createRoot()`, injects minimal Base UI checkbox test styles (`[data-slot="checkbox"] { width: 16px; height: 16px; }`), wraps in `<QueryClientProvider>`, and tracks roots for `cleanup()`.

### Common Mocking Strategies Used in the Suite
- **Seeded Auth & Query Data:** Call `queryClient.setQueryData([...getMeKeyGenerator()], user)` or `queryClient.setQueryDefaults(key, { queryFn })` / `setMutationDefaults(key, { mutationFn })` to test loading, error/retry, and success transitions (`roles-table.test.tsx`, `login.test.tsx`).
- **URL State (`nuqs`):** Wrap components using `useDataTable` or `useQueryStates` in `<NuqsTestingAdapter hasMemory>` (`roles-table.test.tsx`).
- **Next.js Navigation & Hard Redirects:** Mock `next/navigation` via `vi.mock('next/navigation', ...)` (`auth-guard.test.tsx`) and spy on `loginNavigation.replace` (`login.test.tsx`) so tests verify redirects without triggering full page reloads.
- **Global `fetch` Stubs:** Use `vi.stubGlobal('fetch', ...)` to intercept `/api/management/auth/csrf` and endpoint URLs in `endpoint-caller.test.ts`.
- **Media Elements:** Spy on `HTMLMediaElement.prototype.play` / `pause` when mounting `<LoginVideo />` in headless Chromium.

---

## 3. Current Test Inventory (12 Test Files)

### Unit Project (`*.test.ts` — Node)

| File | Coverage |
| :--- | :--- |
| `@schemas/__tests__/auth.test.ts` | `loginSchema` trimming and empty/whitespace validation |
| `@schemas/__tests__/profile.test.ts` | `updateProfileSchema` trimming, required fields, max lengths (200 / 256) |
| `@schemas/__tests__/roles.test.ts` | `roleSchema` trimming, required name, max length (256), max 100 permissions |
| `@schemas/__tests__/users.test.ts` | `createUserSchema` (12..128 password) & `editUserSchema` (no password) validation |
| `core/auth/__tests__/permissions.test.ts` | `PERMISSION_GROUPS` snapshot (9 permissions), `hasPermission`, `hasAnyPermission`, `hasAllPermissions` |
| `core/client/__tests__/endpoint-caller.test.ts` | Search param serialization, `pageParamName`, `omitFromBody`, CSRF pre-flight, `ApiError`, `401` handler vs `skipUnauthorizedHandling`, `ValidationError`, normalizers |
| `core/client/__tests__/index.test.ts` | `EndpointCaller` behavior verification |
| `components/data-table/__tests__/utils.test.ts` | `getDateFilterRange` for `eq`, `isBetween`, `gt`, `gte`, `lt`, `lte`, invalid dates |
| `stores/__tests__/toast.test.ts` | `toastStore` `showToast`, default `'info'` type, `closeToast` |

### Browser Project (`*.test.tsx` — Playwright Chromium)

| File | Coverage |
| :--- | :--- |
| `components/data-table/__tests__/data-table.test.tsx` | Row rendering, empty state, page row selection + `actionBar` + `Escape` handling, pagination, column sort/hide, global & column filtering, row action dropdown |
| `features/guards/auth-guard/__tests__/auth-guard.test.tsx` | Missing permission redirect to `/`, authorized children rendering, unguarded authenticated access |
| `features/pages/login/__tests__/login.test.tsx` | Empty validation errors, successful login toast + `loginNavigation.replace`, error toast on rejection |
| `features/pages/roles/__tests__/permission-category-select.test.tsx` | Whole-category toggle and partial-selection fill behavior |
| `features/pages/roles/__tests__/roles-table.test.tsx` | `RolePermissionsDialog` open/close/empty state, `EditRoleDialog` load failure retry recovery |

---

## 4. Commands to Run Frontend Tests & Static Checks

```powershell
# Run all Vitest tests once (both unit + browser projects)
pnpm --filter frontend exec vitest run

# Run in interactive watch mode
pnpm --filter frontend test

# Run only Node unit tests (*.test.ts)
pnpm --filter frontend exec vitest run --project unit

# Run only Playwright Chromium browser tests (*.test.tsx)
pnpm --filter frontend exec vitest run --project browser

# Run a specific test file
pnpm --filter frontend exec vitest run components/data-table/__tests__/data-table.test.tsx

# Run ESLint + Prettier check + TypeScript check
pnpm --filter frontend check
```
