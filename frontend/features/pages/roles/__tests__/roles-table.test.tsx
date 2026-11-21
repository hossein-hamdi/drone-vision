import { getMeKeyGenerator, type GetMeResponse } from '@api/getMeQuery';
import { getRoleKeyGenerator, type GetRoleResponse } from '@api/getRoleQuery';
import { getRolesKeyGenerator, type GetRolesResponse } from '@api/getRolesQuery';
import { NuqsTestingAdapter } from 'nuqs/adapters/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { page, userEvent } from 'vitest/browser';

import { PERMISSIONS } from '@core/auth';

import { cleanup, makeTestQueryClient, render } from '@/__tests__/test-utils';

import { EditRoleDialog } from '../components/edit-role-dialog';
import { RolesTable } from '../components/roles-table';

const currentUser: GetMeResponse = {
  id: '1',
  fullName: 'Admin User',
  userName: 'admin',
  profileImageUrl: '',
  roles: ['Admin'],
  permissions: [PERMISSIONS.roles.view, PERMISSIONS.roles.update, PERMISSIONS.roles.delete],
  createdAt: '2026-11-22T00:00:00Z',
  updatedAt: '2026-11-22T00:00:00Z',
};

const rolesFixture: GetRolesResponse = {
  items: [
    {
      id: '1',
      name: 'Operator',
      permissions: [PERMISSIONS.users.view, PERMISSIONS.roles.view],
      createdAt: '2026-11-22T00:00:00Z',
      updatedAt: '2026-11-22T00:00:00Z',
    },
    {
      id: '2',
      name: 'Guest',
      permissions: [],
      createdAt: '2026-11-22T00:00:00Z',
      updatedAt: '2026-11-22T00:00:00Z',
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 2,
  totalPages: 1,
};

function renderRolesTable() {
  const queryClient = makeTestQueryClient();
  queryClient.setQueryData([...getMeKeyGenerator()], currentUser);
  queryClient.setQueryData(getRolesKeyGenerator({ Page: 1, PageSize: 10 }), rolesFixture);

  return render(
    <NuqsTestingAdapter hasMemory>
      <RolesTable />
    </NuqsTestingAdapter>,
    { queryClient }
  );
}

describe('RolesTable', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('shows view icon instead of inline permission badges and opens modal on click', async () => {
    renderRolesTable();

    await expect.element(page.getByText('Operator')).toBeInTheDocument();
    await expect.element(page.getByText(PERMISSIONS.users.view)).not.toBeInTheDocument();
    await expect.element(page.getByRole('button', { name: 'Sort' })).not.toBeInTheDocument();

    await userEvent.click(page.getByRole('button', { name: 'View permissions for Operator' }));

    await expect.element(page.getByRole('heading', { name: 'Operator Permissions' })).toBeInTheDocument();
    await expect.element(page.getByText('2 assigned permissions.')).toBeInTheDocument();
    await expect.element(page.getByText(PERMISSIONS.users.view)).toBeInTheDocument();
    await expect.element(page.getByText(PERMISSIONS.roles.view)).toBeInTheDocument();

    await userEvent.click(page.getByRole('button', { name: 'Close' }).last());
    await expect.element(page.getByRole('heading', { name: 'Operator Permissions' })).not.toBeInTheDocument();

    await userEvent.click(page.getByRole('button', { name: 'View permissions for Guest' }));
    await expect.element(page.getByRole('heading', { name: 'Guest Permissions' })).toBeInTheDocument();
    await expect.element(page.getByText('This role does not have any permissions assigned.')).toBeInTheDocument();
  });
});

describe('EditRoleDialog', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('shows error state with retry when loading role fails', async () => {
    const queryClient = makeTestQueryClient();
    const roleDetail: GetRoleResponse = {
      id: '1',
      name: 'Operator',
      permissions: [PERMISSIONS.users.view],
      createdAt: '2026-11-22T00:00:00Z',
      updatedAt: '2026-11-22T00:00:00Z',
    };
    const queryFn = vi.fn().mockRejectedValueOnce(new Error('Role fetch failed')).mockResolvedValueOnce(roleDetail);

    queryClient.setQueryDefaults(getRoleKeyGenerator({ id: '1' }).slice(0, 2), { queryFn });

    render(<EditRoleDialog roleId="1" open onOpenChange={() => {}} />, { queryClient });

    await expect.element(page.getByText('Failed to load role')).toBeInTheDocument();
    await expect.element(page.getByText('Role fetch failed')).toBeInTheDocument();
    await expect.element(page.getByRole('button', { name: 'Save changes' })).toBeDisabled();

    await userEvent.click(page.getByRole('button', { name: 'Try again' }));

    await expect.element(page.getByLabelText('Role name')).toHaveValue('Operator');
    await expect.element(page.getByRole('button', { name: 'Save changes' })).toBeEnabled();
  });
});
