import AuthGuard from '..';
import { getMeKeyGenerator, type GetMeResponse } from '@api/getMeQuery';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page } from 'vitest/browser';

import { PERMISSIONS } from '@core/auth';

import { cleanup, makeTestQueryClient, render } from '@/__tests__/test-utils';

const replaceMock = vi.fn();

const routerMock = {
  replace: replaceMock,
  back: vi.fn(),
};

vi.mock('next/navigation', () => ({
  default: {
    useRouter: () => routerMock,
  },
  useRouter: () => routerMock,
}));

const baseUser: GetMeResponse = {
  id: '1',
  fullName: 'Test Operator',
  userName: 'operator',
  profileImageUrl: '',
  roles: ['Operator'],
  permissions: [],
  createdAt: '2026-11-22T00:00:00Z',
  updatedAt: '2026-11-22T00:00:00Z',
};

function renderWithUser(user: GetMeResponse, permission?: (typeof PERMISSIONS.users)[keyof typeof PERMISSIONS.users]) {
  const queryClient = makeTestQueryClient();
  queryClient.setQueryData([...getMeKeyGenerator()], user);

  return render(
    <AuthGuard permission={permission}>
      <div>Protected Content</div>
    </AuthGuard>,
    { queryClient }
  );
}

describe('AuthGuard', () => {
  beforeEach(() => {
    replaceMock.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('redirects to "/" and renders nothing when user lacks required permission', async () => {
    const { container } = renderWithUser({ ...baseUser, permissions: [] }, PERMISSIONS.users.view);

    await vi.waitFor(() => {
      expect(replaceMock).toHaveBeenCalledWith('/');
    });
    expect(container.innerHTML).toBe('');
    await expect.element(page.getByText('Protected Content')).not.toBeInTheDocument();
  });

  it('renders children and does not redirect when user has required permission', async () => {
    renderWithUser({ ...baseUser, permissions: [PERMISSIONS.users.view] }, PERMISSIONS.users.view);

    await expect.element(page.getByText('Protected Content')).toBeInTheDocument();
    expect(replaceMock).not.toHaveBeenCalled();
  });

  it('renders children when no permission is required', async () => {
    renderWithUser({ ...baseUser, permissions: [] });

    await expect.element(page.getByText('Protected Content')).toBeInTheDocument();
    expect(replaceMock).not.toHaveBeenCalled();
  });
});
