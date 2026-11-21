import { useState } from 'react';

import { afterEach, describe, expect, it, vi } from 'vitest';
import { page, userEvent } from 'vitest/browser';

import { PERMISSIONS } from '@core/auth';

import { cleanup, render } from '@/__tests__/test-utils';

import { PermissionCategorySelect } from '../components/permission-category-select';

function Harness({ initial = [], onChangeSpy }: { initial?: string[]; onChangeSpy: (permissions: string[]) => void }) {
  const [value, setValue] = useState<string[]>(initial);

  return (
    <PermissionCategorySelect
      id="test-permissions"
      value={value}
      onChange={(next) => {
        setValue(next);
        onChangeSpy(next);
      }}
    />
  );
}

describe('PermissionCategorySelect', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('selects and deselects all permissions in a category when clicked', async () => {
    const onChangeSpy = vi.fn();
    render(<Harness onChangeSpy={onChangeSpy} />);

    await userEvent.click(page.getByRole('combobox'));
    await userEvent.click(page.getByRole('option', { name: /User Management/i }));

    expect(onChangeSpy).toHaveBeenLastCalledWith([
      PERMISSIONS.users.view,
      PERMISSIONS.users.create,
      PERMISSIONS.users.update,
      PERMISSIONS.users.delete,
    ]);

    await userEvent.click(page.getByRole('option', { name: /User Management/i }));
    expect(onChangeSpy).toHaveBeenLastCalledWith([]);
  });

  it('shows partial count and fills remaining category permissions on click', async () => {
    const onChangeSpy = vi.fn();
    render(<Harness initial={[PERMISSIONS.users.view]} onChangeSpy={onChangeSpy} />);

    await expect.element(page.getByText('User Management (1/4)')).toBeInTheDocument();

    await userEvent.click(page.getByRole('combobox'));
    await userEvent.click(page.getByRole('option', { name: /User Management/i }));

    expect(onChangeSpy).toHaveBeenLastCalledWith([
      PERMISSIONS.users.view,
      PERMISSIONS.users.create,
      PERMISSIONS.users.update,
      PERMISSIONS.users.delete,
    ]);
  });
});
