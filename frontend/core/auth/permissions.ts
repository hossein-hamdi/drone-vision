export const PERMISSIONS = {
  users: {
    view: 'Users.View',
    create: 'Users.Create',
    update: 'Users.Update',
    delete: 'Users.Delete',
  },
  roles: {
    view: 'Roles.View',
    create: 'Roles.Create',
    update: 'Roles.Update',
    delete: 'Roles.Delete',
  },
  permissions: {
    view: 'Permissions.View',
  },
} as const;

export const PERMISSION_GROUPS = [
  {
    category: 'users',
    label: 'User Management',
    permissions: [
      { key: 'view', value: PERMISSIONS.users.view, label: 'View users' },
      { key: 'create', value: PERMISSIONS.users.create, label: 'Create users' },
      { key: 'update', value: PERMISSIONS.users.update, label: 'Update users' },
      { key: 'delete', value: PERMISSIONS.users.delete, label: 'Delete users' },
    ],
  },
  {
    category: 'roles',
    label: 'Role Management',
    permissions: [
      { key: 'view', value: PERMISSIONS.roles.view, label: 'View roles' },
      { key: 'create', value: PERMISSIONS.roles.create, label: 'Create roles' },
      { key: 'update', value: PERMISSIONS.roles.update, label: 'Update roles' },
      { key: 'delete', value: PERMISSIONS.roles.delete, label: 'Delete roles' },
    ],
  },
  {
    category: 'permissions',
    label: 'Permissions',
    permissions: [{ key: 'view', value: PERMISSIONS.permissions.view, label: 'View permissions' }],
  },
] as const;

export type PermissionCategory = keyof typeof PERMISSIONS;
export type Permission = (typeof PERMISSION_GROUPS)[number]['permissions'][number]['value'];
