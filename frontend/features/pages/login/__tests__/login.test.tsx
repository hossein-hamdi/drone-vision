import Login from '..';
import { loginKeyGenerator, type LoginRequestData, type LoginResponse } from '@api/loginMutation';
import { authConfig } from '@configs/auth.config';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page, userEvent } from 'vitest/browser';

import toastStore from '@stores/toast';

import { cleanup, makeTestQueryClient, render } from '@/__tests__/test-utils';

import { loginNavigation } from '../components/login-form';

const loginResponse: LoginResponse = {
  accessToken: 'test-access-token',
  tokenType: 'Bearer',
  expiresIn: 3600,
  refreshTokenExpiresAt: '2026-12-01T00:00:00Z',
};

function renderLogin(mutationFn = vi.fn().mockResolvedValue(loginResponse)) {
  const queryClient = makeTestQueryClient();
  queryClient.setMutationDefaults([...loginKeyGenerator()], {
    mutationFn: mutationFn as (data: LoginRequestData) => Promise<LoginResponse>,
  });
  render(<Login />, { queryClient });
  return { mutationFn, queryClient };
}

describe('Login page', () => {
  let replaceSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    toastStore.setState({ toasts: [] });
    replaceSpy = vi.spyOn(loginNavigation, 'replace').mockImplementation(() => {});

    vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined);
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('does not submit when username or password is empty', async () => {
    const { mutationFn } = renderLogin();

    await userEvent.click(page.getByRole('button', { name: 'Sign in' }));

    await expect.element(page.getByText('Enter your username.')).toBeInTheDocument();
    await expect.element(page.getByText('Enter your password.')).toBeInTheDocument();
    expect(mutationFn).not.toHaveBeenCalled();
  });

  it('submits credentials, shows success toast, and redirects on success', async () => {
    const { mutationFn } = renderLogin();

    await userEvent.fill(page.getByLabelText('Username'), 'pilot');
    await userEvent.fill(page.getByLabelText('Password'), 'secret123');
    await userEvent.click(page.getByRole('button', { name: 'Sign in' }));

    await vi.waitFor(() => {
      expect(mutationFn).toHaveBeenCalledWith({ username: 'pilot', password: 'secret123' }, expect.anything());
    });
    await vi.waitFor(() => {
      expect(toastStore.getState().toasts).toContainEqual(
        expect.objectContaining({ message: 'Signed in successfully.', type: 'success' })
      );
      expect(replaceSpy).toHaveBeenCalledWith(authConfig.defaultRedirect);
    });
  });

  it('shows an error toast when login fails', async () => {
    const { mutationFn } = renderLogin(vi.fn().mockRejectedValue(new Error('Invalid credentials')));

    await userEvent.fill(page.getByLabelText('Username'), 'pilot');
    await userEvent.fill(page.getByLabelText('Password'), 'wrong-pass');
    await userEvent.click(page.getByRole('button', { name: 'Sign in' }));

    await vi.waitFor(() => {
      expect(mutationFn).toHaveBeenCalled();
      expect(toastStore.getState().toasts).toContainEqual(
        expect.objectContaining({ message: 'Invalid credentials', type: 'error' })
      );
    });
    expect(replaceSpy).not.toHaveBeenCalled();
  });
});
