import { describe, expect, it } from 'vitest';

import { hasAllPermissions, hasAnyPermission, hasPermission, PERMISSION_GROUPS, PERMISSIONS } from '@core/auth';

describe('core/auth permissions', () => {
  it('snapshots all categorized permissions', () => {
    const all = PERMISSION_GROUPS.flatMap((group) => group.permissions.map((item) => item.value));

    expect(all).toEqual([
      'Users.View',
      'Users.Create',
      'Users.Update',
      'Users.Delete',
      'Roles.View',
      'Roles.Create',
      'Roles.Update',
      'Roles.Delete',
      'Permissions.View',
    ]);
  });

  it('checks single, any, and all permissions for a user', () => {
    const user = {
      id: '1',
      roles: ['Operator'],
      permissions: [PERMISSIONS.users.view, PERMISSIONS.users.create],
    };

    expect(hasPermission(user, PERMISSIONS.users.view)).toBe(true);
    expect(hasPermission(user, PERMISSIONS.users.delete)).toBe(false);
    expect(hasPermission(null, PERMISSIONS.users.view)).toBe(false);

    expect(hasAnyPermission(user, [PERMISSIONS.users.delete, PERMISSIONS.users.create])).toBe(true);
    expect(hasAnyPermission(user, [PERMISSIONS.roles.view, PERMISSIONS.roles.delete])).toBe(false);

    expect(hasAllPermissions(user, [PERMISSIONS.users.view, PERMISSIONS.users.create])).toBe(true);
    expect(hasAllPermissions(user, [PERMISSIONS.users.view, PERMISSIONS.users.update])).toBe(false);
  });
});
