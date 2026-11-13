<!-- BEGIN:nextjs-agent-rules -->

## This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

# Skyeye Frontend

This file provides guidance to AI coding assistants, including Codex and Claude Code (claude.ai/code), when working in `frontend/`.

## Project Overview

Skyeye is the frontend of the `drone-analyze` project. The current application is an early scaffold with a login screen and a placeholder dashboard. A separate backend owns authentication, authorization, business rules, and persistence.

| Area            | Current stack                                           |
| --------------- | ------------------------------------------------------- |
| Framework       | Next.js 16.4.3, App Router                              |
| UI runtime      | React and React DOM 19.3.0                              |
| Language        | TypeScript 5, strict mode, ES2017 target                |
| Styling         | Tailwind CSS v4 through `@tailwindcss/turbopack`        |
| Components      | shadcn/ui, `base-nova` style, Base UI primitives        |
| Icons           | `lucide-react`                                          |
| Server state    | TanStack React Query v5 with a shared endpoint client   |
| Forms           | React Hook Form with Zod v4 and `zodResolver`           |
| Themes          | `next-themes`, system preference, class-based dark mode |
| Fonts           | Roboto and Share Tech Mono through `next/font/google`   |
| Package manager | pnpm 12.16.1, pinned in `package.json`                  |

Treat `package.json`, `pnpm-lock.yaml`, and the installed Next.js documentation as the sources of truth when these versions change.

## Frontend and Backend Boundary

**No SQL or database access belongs in this frontend.** This applies to every frontend directory and to Server Components, Server Actions, and Route Handlers as well as browser code.

- Keep SQL queries, database clients, ORMs, connection strings, migrations, and database schema definitions in the separate backend.
- Access backend functionality through HTTP contracts. Frontend validation and permission-aware UI complement backend enforcement.
- `api/` is for frontend endpoint definitions and query/mutation keys. It is currently empty; it is not a Next.js Route Handler directory.
- If a Next.js proxy or Route Handler is needed, keep it focused on HTTP forwarding, cookies, and frontend-specific composition. Persistence and domain rules remain in the backend.
- This frontend uses root-level source directories. There is no frontend `src/` directory; do not create a parallel `src/app/` structure.

The existing Management backend lives at `../src/services/Management/` and uses ASP.NET Core on .NET 10. Read its own `AGENTS.md`, controllers, and DTOs before changing an integration. Its development Swagger is at `https://localhost:7146/swagger/index.html` when the service is running. Backend instructions and commands belong to that service.

## Development Commands

Run these commands from `frontend/`:

```bash
pnpm install        # Install dependencies
pnpm dev            # Start Next.js development server, normally localhost:3000
pnpm build          # Create production build
pnpm start          # Serve an existing production build
pnpm preview        # Build, then start production server
pnpm lint           # Run ESLint
pnpm lint:fix       # Run ESLint with fixes
pnpm typecheck      # Run tsc --noEmit
pnpm format         # Format supported files throughout the frontend
pnpm format:check   # Check formatting
pnpm check          # Lint, formatting check, then typecheck
```

Use pnpm consistently and preserve the pnpm lockfiles. There is currently no frontend test script, database migration command, or release script. For a focused edit, prefer `pnpm exec prettier --write <changed-files>` over formatting the entire project.

## Current Application Features

- `/login`: Skyeye login screen with username/password validation, a password visibility toggle, and a light/dark theme toggle.
- Login media: responsive drone video with play/pause controls and reduced-motion support.
- `/`: placeholder dashboard in the `(dashboard)` route group.
- Shared root shell: metadata, fonts, viewport settings, iOS splash declarations, theme provider, and React Query provider.
- API infrastructure: typed endpoint registration, request/response validation, error types, and unauthorized/signout helpers.

**Sign-in is not connected to the backend yet.** Submitting the login form currently shows a root form error. No frontend route guard, session restoration, or token refresh flow is wired. Do not describe these as implemented or replace real integration with a simulated successful login.

## Project Structure

```text
frontend/
|-- @schemas/
|   |-- auth.ts                    # Zod login schema and inferred type
|   `-- index.ts                   # Schema exports
|-- api/                          # Backend endpoint definitions (currently empty)
|-- app/
|   |-- (authentication)/login/page.tsx
|   |-- (dashboard)/page.tsx       # Placeholder route at /
|   |-- _shell/
|   |   |-- index.tsx             # RootShell, metadata, fonts, viewport
|   |   `-- AppleSplashScreens.tsx
|   `-- layout.tsx                # Single root layout
|-- components/ui/                # Shared shadcn/Base UI components
|-- core/
|   |-- client/                   # Endpoint registration, fetch, QueryClient
|   |   |-- @types/index.ts       # Endpoint options and key generator types
|   |   |-- auth-handler.ts       # Unauthorized callback and signout latch
|   |   |-- endpoint-caller.ts
|   |   |-- get-query-client.ts
|   |   |-- index.ts
|   |   `-- provider.tsx
|   `-- errors/                   # AppError, ApiError, ValidationError, ensureError
|-- features/
|   |-- pages/login/              # Login composition and its local components
|   `-- providers/index.tsx       # Theme and API-client provider composition
|-- lib/utils.ts                  # Re-exports cn from the cn package
|-- public/                       # Icons, splash images, and drone video assets
|-- styles/globals.css            # Global styles and theme tokens
|-- components.json               # shadcn configuration
|-- next.config.ts
`-- package.json
```

Keep route files small: metadata and route composition belong in `app/`; page-specific UI belongs in `features/pages/<page>/`. Reusable UI belongs in `components/`; shared infrastructure belongs in `core/`.

## Next.js Rendering and Routing

- Read the relevant guide in `node_modules/next/dist/docs/` before writing Next.js code. Keep the managed instruction block above intact.
- `next.config.ts` enables `cacheComponents`, `partialPrefetching`, and experimental `agentFeedback`. Preserve these settings unless the task requires changing them.
- Pages and layouts are Server Components by default. Add `'use client'` at the smallest boundary that needs state, effects, event handlers, browser APIs, or React Query hooks.
- Keep shared fonts, global CSS, metadata helpers, and provider composition in the existing root shell. There is one root layout; no marketing/app split exists.
- With Cache Components enabled, handle uncached runtime data with an appropriate `Suspense` boundary, and use `'use cache'` only for data safe to cache. Never place authenticated user data in a shared cache without a deliberate isolation strategy.
- Do not add legacy route exports such as `dynamic`, `revalidate`, or `fetchCache` to bypass Cache Components errors. Follow the installed caching guides; do not disable validation broadly as a routine fix.
- Dynamic route `params` are asynchronous. Await them, and await request APIs such as `cookies()` and `headers()` when used. Validate identifiers against the backend contract.
- Route groups such as `(authentication)` and `(dashboard)` organize code; their names do not appear in URLs and do not enforce authorization.

Relevant local guides:

- `01-app/01-getting-started/05-server-and-client-components.md`
- `01-app/02-guides/migrating-to-cache-components.md`
- `01-app/02-guides/authentication-with-cache-components.md`
- `01-app/03-api-reference/03-file-conventions/route.md`

These paths are relative to `node_modules/next/dist/docs/`.

## API Client and Server State

Build integrations on `@core/client` and TanStack React Query rather than duplicating fetch and validation logic in page components.

- Define endpoint registrations and stable key generators in `api/`, organized by backend feature. Ensure registrations run before their queries or mutations are used.
- `client.registerEndpoint` registers query or mutation defaults on `getQueryClient()`. Specify the HTTP method explicitly; `EndpointCaller` defaults to `POST`.
- Use `requestDataSchema` and `responseSchema` for runtime validation. Use normalizers to map UI names and shapes to backend DTOs without changing the backend contract.
- Query data comes from the final query-key element when it is an object. Include filters and pagination inputs in the key to keep cached results separate.
- GET payloads become search parameters; arrays are comma-separated and null/undefined values are omitted. Match the backend's expected serialization.
- `omitFromBody` excludes fields consumed by a dynamic URL. `pageParamName` forwards an infinite query's page parameter.
- Preserve abort signals for queries. Invalidate or update the relevant query keys after successful mutations.
- `getQueryClient()` returns a browser singleton and a fresh instance on the server. Keep the provider and endpoint registrations using the same client for the relevant environment; never share a server QueryClient between users.
- Current defaults are a one-minute stale time, one retry, and no automatic refetch on focus or reconnect.

The current caller handles JSON and does not configure a backend base URL, bearer headers, CSRF headers, refresh retries, or multipart uploads. Extend the transport deliberately when an integration needs them. Do not invent environment variable names or assume these features already exist.

### Errors

Use `ApiError.status` for HTTP failures, `ValidationError` for schema failures, and `ensureError` when normalizing unknown caught values. All custom errors extend `AppError`.

The Management API returns ProblemDetails (`title`, `detail`, `status`, `traceId`, and validation `errors`). The current caller only extracts string `error` or `message` fields. Add appropriate ProblemDetails handling when connecting the backend, and show useful form or inline errors. Do not assume its existing error parsing already covers that contract.

## Backend API and Browser Authentication

These are backend routes, not implemented Next.js `/api/*` routes:

| Route                                                     | Purpose                                             |
| --------------------------------------------------------- | --------------------------------------------------- |
| `GET /auth/csrf`                                          | Obtain anonymous CSRF token and cookie              |
| `POST /auth/login`                                        | Authenticate and set browser refresh cookie         |
| `POST /auth/refresh`                                      | Rotate refresh cookie and obtain a new access token |
| `POST /auth/logout`                                       | Revoke the session and clear refresh cookie         |
| `POST /auth/register`                                     | Public registration without role assignment         |
| `GET /auth/me`                                            | Current identity, roles, and permissions            |
| `/users`, `/roles`, `/permissions`                        | Permission-protected management APIs                |
| `/profile`, `/profile-image`, `/users/{id}/profile-image` | Profile and image management                        |
| `GET /uploads/profiles/{fileName}`                        | Serve generated public profile image URL            |

When implementing browser authentication:

- Follow the backend's HTTPS, same-origin model. If the frontend is hosted separately, configure forwarding for auth, API, and upload paths while preserving cookies and required headers. No proxy is configured in this frontend yet.
- Fetch `/auth/csrf` anonymously first. Send its `requestToken` as `X-CSRF-TOKEN` for login, refresh, and logout, with the associated cookies.
- Refresh secrets stay in the backend's Secure, HttpOnly, SameSite=Strict cookie scoped to `/auth`. Do not store them in localStorage, expose them in browser JSON, or use the native-client transport from the browser.
- Keep anonymous auth calls free of bearer headers; protected requests such as `/auth/me` use the access token.
- Coordinate refresh attempts through one shared flow. Concurrent rotation can invalidate the session. Retry an expired-token request at most once after successful refresh; clear auth state when refresh fails.
- Reuse `setUnauthorizedHandler`, `triggerUnauthorized`, and `beginSignout` where appropriate. The signout latch assumes a hard navigation resets module state. A login 401 should remain a form error; `skipUnauthorizedHandling` supports that distinction.
- Map form fields to the actual backend DTO names: the login form uses `username`, while the API expects `userName` alongside `password`. UI validation does not replace backend validation, role checks, or permission policies.

## TypeScript and Code Standards

- Preserve strict mode. Prefer inferred schema types, explicit boundary types, and `unknown` with narrowing over `any` or non-null assertions.
- Put shared validation schemas in `@schemas/` and export them through `@schemas/index.ts`. `LoginSchema` is inferred from `loginSchema`.
- `@/*` maps to the frontend root, not `src/*`. Existing specific aliases include `@components/*`, `@features/*`, `@core/*`, `@api/*`, `@styles/*`, and the exact `@schemas` barrel alias.
- Some aliases in `tsconfig.json` reserve future directories such as `@types`, constants, hooks, and stores. Check that the target exists before importing it; an alias declaration does not mean the module exists.
- Use camelCase for TypeScript values and PascalCase for components and types. Preserve backend JSON field names at the HTTP boundary, using normalizers where needed.
- Reuse schemas, endpoint keys, constants, and UI primitives. Extract shared values when they have multiple consumers; avoid duplicating lists or validation rules.
- Use type-only imports where appropriate and let Prettier sort imports and Tailwind classes. Formatting uses two spaces, semicolons, single quotes, LF endings, and a 120-character print width.
- Respect React Hooks rules and dependency checks. Keep browser APIs out of server execution paths.

## Styling and Theme Tokens

The actual stylesheet is `styles/globals.css`, imported by `app/_shell/index.tsx`. It defines light tokens in `:root`, dark tokens in `.dark`, and Tailwind mappings in `@theme inline`. The current palette uses olive neutrals and a yellow primary accent.

Use semantic tokens for application colors:

| Purpose                 | Utilities                                    |
| ----------------------- | -------------------------------------------- |
| Page and main text      | `bg-background`, `text-foreground`           |
| Cards                   | `bg-card`, `text-card-foreground`            |
| Secondary surfaces      | `bg-secondary`, `text-secondary-foreground`  |
| Muted content           | `bg-muted`, `text-muted-foreground`          |
| Primary actions         | `bg-primary`, `text-primary-foreground`      |
| Hover/selected surfaces | `bg-accent`, `text-accent-foreground`        |
| Borders, inputs, focus  | `border-border`, `border-input`, `ring-ring` |
| Destructive actions     | `text-destructive`, `bg-destructive/10`      |

- Add new semantic colors to the stylesheet for both themes and expose them through `@theme inline` before use. Avoid hardcoded colors in application UI.
- Pair surfaces and their foreground tokens; do not assume white text is readable on every semantic fill.
- Theme changes should flow through tokens. Existing Base UI primitives contain dark variants, and the theme toggle uses dark visibility classes; preserve necessary behavior instead of imposing a blanket ban on `dark:`.
- Use `cn` from `@/lib/utils` in application code for conditional class names; shared UI files also import the same utility directly from `cn`.
- Prefer existing radius utilities and component variants. Use flex/grid with `gap-*`, `size-*` for equal dimensions, and `truncate` for single-line overflow.
- Keep colors and typography in shared component variants; use consumer `className` primarily for layout.

**shadcn configuration mismatch:** `components.json` currently names `app/globals.css`, which does not exist. The active file is `styles/globals.css`. Resolve this mismatch before using a CLI operation that writes theme CSS; keep one global stylesheet.

## UI Components and Forms

- Reuse the installed `components/ui/` primitives: button, field, input, input-group, label, separator, and textarea. Check installed files before importing another component.
- This project uses Base UI, not Radix. Follow the local shadcn skill and the correct component documentation; custom triggers use Base UI's `render` API where supported.
- Button variants are `default`, `outline`, `secondary`, `ghost`, `destructive`, and `link`. Use supported variants and icon sizes instead of copying `primary`/`danger` props from another project.
- Use `FieldGroup`, `Field`, `FieldLabel`, and `FieldError` with React Hook Form and Zod. Put `data-invalid` on the field and `aria-invalid` on the input; link error text with `aria-describedby`.
- Inside `InputGroup`, use `InputGroupInput` or `InputGroupTextarea`. Add controls through `InputGroupAddon` and `InputGroupButton`.
- Use `type="button"` for non-submit controls. Disable repeated submissions and show pending/error states. Password fields should support an accessible show/hide toggle.
- Use `lucide-react` consistently. Mark decorative icons `aria-hidden="true"`, label icon-only actions with `aria-label`, and use `data-icon="inline-start"` or `inline-end` when placing icons beside button text.
- Use shared dialog/confirmation primitives for destructive actions instead of browser `confirm()` or `alert()`. Dialog and toast components are not installed yet; add the appropriate Base UI components before importing them.

## Accessibility and Responsive Layout

- Use semantic headings, labels, buttons, links, and landmarks, with a clear page heading hierarchy.
- Preserve visible keyboard focus. Dialogs need accessible titles, focus management, Escape handling, and focus restoration through the shared primitive.
- Keep loading, success, and error feedback accessible and concise. A tooltip or title alone does not label an icon button for screen readers.
- Use mobile-first layouts and verify small screens without horizontal overflow. Keep forms at a readable width and allow data-heavy views the space they need.
- For tables with explicit column widths, use a consistent fixed layout and widths shared by headers and cells; allow horizontal scrolling where necessary.
- Preserve video play/pause controls and `prefers-reduced-motion` behavior. Avoid autoplay that overrides the user's reduced-motion preference.
- Check new text/background pairs in both themes: at least 4.5:1 for normal text and 3:1 for relevant UI boundaries.
- The current interface is English and `components.json` has `rtl: false`. Add localization and RTL support deliberately when requested.

## Verification and Commits

- For documentation-only changes, check formatting and review the diff. Application tests and a production build are unnecessary unless behavior or configuration changed.
- For application changes, run the relevant lint, formatting, and TypeScript checks; `pnpm check` combines them. Run `pnpm build` when changing routing, rendering, framework configuration, or production behavior.
- Verify changed UI and runtime flows in a running app. Use the local `next-dev-loop` skill when editing application code; compilation alone does not verify interaction behavior.
- No frontend test runner is configured. Add focused behavioral tests when the scope warrants a test setup; report the checks actually run and any remaining integration gaps.
- Repository-level Husky and commitlint use Conventional Commits, such as `fix:`, `feat:`, and `docs:`. The frontend's lint-staged configuration does not establish that every frontend check runs in the root hook; run required checks explicitly.
- No frontend release workflow or bilingual release-note system is configured. Do not copy release commands or changelog conventions from unrelated applications.
