# Frontend — Components, Hooks & DataTable Catalog

---

## 1. Shared Layout & Navigation Components

### `Providers` (`features/providers/index.tsx`)
Composes global client context providers in exact nesting order:
`ThemeProvider` (`attribute="class"`, `defaultTheme="system"`, `enableSystem`) → `ClientProvider` (`getQueryClient()` singleton) → `NuqsAdapter` → `TooltipProvider` → `{children}` + `<Toaster />`.

### `DashboardLayout` (`components/dashboard-layout/index.tsx`)
Wraps dashboard routes in `SidebarProvider`, `<AppSidebar />`, `<SidebarInset>`, `<Navbar />`, and a padded flex content container (`p-4 sm:p-6`).

### `AppSidebar` (`features/navigation/components/app-sidebar.tsx`)
Collapsible icon sidebar (`collapsible="icon"`) rendering the Skyeye brand link (`/`), and filtering items from `constant/sidebar.ts` (`Overview`, `Users`, `Roles`) using `useAuth().can(item.permission)`.

### `Navbar` & `AccountMenu` (`features/navigation/components/`)
- `Navbar`: Fixed `h-16` header with `<SidebarTrigger />`, `<ThemeToggle />`, and `<AccountMenu />`.
- `AccountMenu`: Base UI `DropdownMenu` triggered by user avatar + display name, linking to `/profile` (`render={<Link href="/profile" />}`) and triggering `useAuth().logout()`.

### `DashboardBreadcrumb` (`components/breadcrumb/index.tsx`)
Automatically generates breadcrumb items from `usePathname()`, shows a `"Back"` button (`router.back()`) on nested routes, and renders an optional icon badge, `h1` title, description, and right-aligned action slot.

| Prop | Type | Required | Description |
| :--- | :--- | :---: | :--- |
| `title` | `string` | Yes | Page heading |
| `description` | `string` | No | Subtitle below heading |
| `icon` | `ReactElement<LucideIcon>` | No | Icon rendered in bordered `size-10` container |
| `actions` | `ReactNode` | No | Right-aligned action controls (e.g., `<CreateUserDialog />`) |
| `className` | `string` | No | Root container classes |

### `FullPageLoader`, `ThemeToggle` & `Toaster`
- `FullPageLoader` (`components/full-page-load/index.tsx`): Fixed full-screen overlay (`z-50`) displaying `/icons/icon-512.png` and an accessible `role="status"` spinner.
- `ThemeToggle` (`components/theme-toggle/index.tsx`): Toggles `next-themes` `light` / `dark`.
- `Toaster` & `ToastContainer` (`components/toast/`): Bridges `useToastStore` (`stores/toast.ts`) to a fixed bottom-right (`z-9999`) stack of dismissible, 5-second auto-closing toasts (`success`, `error`, `warning`, `info`).

---

## 2. DataTable System (`components/data-table/` & `hooks/use-data-table.ts`)

A feature-rich **TanStack React Table v9** (`@tanstack/react-table` `9.3.1`) system integrated with **`nuqs`** URL state persistence and custom `dataTableFilteringFeature`.

### Core Exports (`components/data-table/index.tsx`)

| Component / Helper | Role |
| :--- | :--- |
| `DataTable` | Root container (`data-slot="data-table"`), table layout, sticky header support, `Escape` key handler to clear visible row selection, pagination footer, and conditional `actionBar` |
| `DataTableToolbar` | Inline filter toolbar rendering per-column controls (`text`, `number`/`slider`, `date`, `select`, `boolean`), optional global filter input, `"Reset"` button, custom `children`, and `DataTableViewOptions` |
| `DataTableAdvancedToolbar` | Minimal toolbar for pairing with `DataTableFilterMenu` / `DataTableCommandFilterMenu` and `DataTableViewOptions` |
| `DataTableColumnHeader` | Sortable/hideable column header dropdown (`Asc`, `Desc`, `Hide`), or plain truncated label when sorting & hiding are disabled |
| `DataTableFilterMenu` | Popover rule builder (`Where` / `and` / `or` join operator, column/operator/value selectors, `@dnd-kit` drag-to-reorder rules, `Ctrl/Cmd+Shift+F` shortcut) |
| `DataTableCommandFilterMenu` | `cmdk`-powered command palette filter builder (`Ctrl/Cmd+Shift+F` shortcut) + removable inline filter chips |
| `DataTableSortMenu` | Multi-column sort builder with drag-to-reorder priority (`Ctrl/Cmd+Shift+S` shortcut) |
| `DataTableDateFilter` | Single date or date-range calendar popover (`react-day-picker`) |
| `DataTableFacetedFilter` | Searchable single/multi-select filter popover using `Faceted` + badge summaries |
| `DataTableRangeFilter` | Dual min/max numeric input with delayed (`500ms`) invalid range indicator |
| `DataTableSliderFilter` | Dual-thumb numeric range slider + min/max inputs synced with faceted min/max values |
| `DataTablePagination` | Selected row counter (when row selection enabled), rows-per-page selector (`[10, 20, 30, 40, 50]`), page indicator, and first/prev/next/last buttons |
| `DataTableViewOptions` | Searchable column visibility toggle menu |
| `DataTableSkeleton` | Accessible loading skeleton matching toolbar, column count, row count, and pagination |
| `getDataTableSelectColumn` | Row selection checkbox column with `"Select all"` on page and optional Shift+click range selection |
| `getDataTableActionsColumn` | Ellipsis row dropdown actions column (`size: 48`, `enableHiding: false`, `enableSorting: false`) |

### `DataTable` Props Reference

| Prop | Type | Required | Default | Description |
| :--- | :--- | :---: | :---: | :--- |
| `table` | `ReactTable<DataTableFeatures, TData, unknown>` | Yes | — | Instance returned by `useDataTable()` or `useTable({ features: dataTableFeatures, ... })` |
| `children` | `React.ReactNode` | No | — | Rendered above the table border (typically `<DataTableToolbar />`) |
| `actionBar` | `React.ReactNode` | No | `undefined` | Floating/footer bar rendered when `getVisibleSelectedRows(table).length > 0` |
| `onEscapeKeyDown` | `(event: KeyboardEvent) => void` | No | `undefined` | Intercepts `Escape` before clearing visible row selection (`preventDefault()` cancels clear) |
| `stickyHeader` | `boolean` | No | `false` | Enables sticky `<thead>` with scroll-padding adjustments |

### `useDataTable` Props & URL State (`hooks/use-data-table.ts`)

| Prop | Default | Behavior |
| :--- | :--- | :--- |
| `mode` | `'server'` | `'server'` enables `manualPagination`, `manualSorting`, `manualFiltering` and requires `pageCount`; `'client'` runs client-side filtering/sorting/pagination |
| `config` | `{}` | Inline `DataTableConfigInput` or pre-built `createDataTableConfig(...)` (supports optional `id` namespace prefix for multi-table pages) |
| `history` | `'replace'` | `nuqs` history mode (`'replace'` or `'push'`) |
| `debounceMs` | `300` | Debounce window applied when `mode === 'server' && shallow === false` |
| `shallow` | `true` | `nuqs` shallow URL updates |

### Copy-Paste Usage Example (Server Mode)

```tsx
import { DataTable, DataTableColumnHeader, DataTableToolbar } from '@/components/data-table';
import { getColumnFilterParser, buildPagedQueryParams } from '@/components/data-table/lib';
import { useDataTable } from '@/hooks/use-data-table';
import { parseAsInteger, useQueryStates } from 'nuqs';

export function ExampleTable({ items, totalPages }: { items: Item[]; totalPages: number }) {
  const [{ page, perPage, name }] = useQueryStates({
    page: parseAsInteger.withDefault(1),
    perPage: parseAsInteger.withDefault(10),
    name: getColumnFilterParser('name', 'text'),
  });

  const { table } = useDataTable({
    data: items,
    columns,
    pageCount: totalPages,
    enableRowSelection: false,
    config: { defaultPageSize: 10 },
    getRowId: (row) => row.id,
  });

  return (
    <DataTable table={table}>
      <DataTableToolbar table={table} />
    </DataTable>
  );
}
```

### DataTable Utilities (`components/data-table/lib/utils.ts`)
- `buildPagedQueryParams({ page, perPage, maxPageSize, filters })`: Produces `{ Page, PageSize, ...extra }` for Management list endpoints (`Page >= 1`, `PageSize` clamped `1..100`).
- `getDateFilterRange(filters)`: Converts `ColumnFilterItem` (`eq`, `isBetween`, `gt`, `gte`, `lt`, `lte`) into `{ CreatedAtFrom?, CreatedAtTo? }` UTC ISO strings spanning full local calendar days (`00:00:00.000` to `23:59:59.999`).

---

## 3. Feature Modules (`features/pages/`)

| Feature | Components | Responsibilities |
| :--- | :--- | :--- |
| **Login** (`features/pages/login/`) | `LoginHeader`, `LoginForm`, `LoginVideo` | Credential form (`loginSchema`), password visibility toggle, reduced-motion video playback, `loginNavigation.replace()` |
| **Profile** (`features/pages/profile/`) | `ProfileContent`, `DeleteProfileImageDialog` | Avatar upload/removal, role badges, optimistic profile updates via `useUpdateProfile()` |
| **Roles** (`features/pages/roles/`) | `RolesTable`, `getRolesColumns`, `CreateRoleDialog`, `EditRoleDialog`, `DeleteRoleDialog`, `RolePermissionsDialog`, `RoleFormFields`, `PermissionCategorySelect` | Paginated/filtered role table, category-level permission selector (`PERMISSION_GROUPS` with partial-count display), modal abort cancellation |
| **Users** (`features/pages/users/`) | `UsersTable`, `getUsersColumns`, `CreateUserDialog`, `EditUserDialog`, `DeleteUserDialog` | Paginated/filtered user table (`Name`, `Role`, `CreatedAt`), user CRUD, role assignment, admin avatar upload/removal |

---

## 4. UI Primitives (`components/ui/`)

Built with `shadcn` (`base-nova` style) on **`@base-ui/react`** (using `render` prop composition instead of Radix `asChild`):
- `avatar.tsx`, `badge.tsx`, `breadcrumb.tsx`, `button.tsx`, `calendar.tsx`, `checkbox.tsx`, `command.tsx`, `dialog.tsx`, `direction.tsx`, `dropdown-menu.tsx`, `faceted.tsx` (custom Cmdk+Popover store primitive), `field.tsx`, `input-group.tsx`, `input.tsx`, `label.tsx`, `popover.tsx`, `select.tsx`, `separator.tsx`, `sheet.tsx`, `sidebar.tsx`, `skeleton.tsx`, `slider.tsx`, `sortable.tsx` (custom `@dnd-kit` wrapper), `table.tsx`, `textarea.tsx`, `tooltip.tsx`.

---

## 5. Custom Hooks & Stores

| Hook / Store | Location | Signature & Behavior |
| :--- | :--- | :--- |
| `useAuth()` | `hooks/use-auth.tsx` | Returns `{ user, loading, error, isUnauthorized, can, canAny, canAll, logout, refetch, updateUser }` backed by `getMeQuery` and `logoutMutation` with `beginSignout()` guard |
| `useDataTable()` | `hooks/use-data-table.ts` | Integrates `@tanstack/react-table` + `nuqs` URL state |
| `useDeleteConfirmation()` | `hooks/use-delete-confirmation.ts` | Manages `{ itemToDelete, isModalOpen, deletingId, openModal, closeModal, confirmDelete }` with `useModalAbort` cancellation |
| `useModalAbort()` | `hooks/use-modal-abort.ts` | Returns `{ createSignal, abort }` + cancels React Query requests (`queryKey`) on dialog close/unmount; exports `isAbortError()` |
| `useUpdateProfile()` | `hooks/use-update-profile.ts` | Optimistically updates `['profile']` and `['auth', 'me']`, rolls back on error, invalidates on settle |
| `useIsMobile()` | `hooks/use-mobile.ts` | `useSyncExternalStore` tracking `(max-width: 767px)` (`MOBILE_BREAKPOINT = 768`) |
| `toastStore` / `useToast()` | `stores/toast.ts` | Vanilla Zustand store (`showToast(message, type?)`, `closeToast(id)`); usable inside and outside React |
