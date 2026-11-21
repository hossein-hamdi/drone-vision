import type { Permission } from './permissions';

export { PERMISSIONS, PERMISSION_GROUPS, type Permission, type PermissionCategory } from './permissions';

export type PermissionSubject = {
  id?: string;
  roles?: readonly string[];
  permissions?: readonly string[];
};

export function hasPermission(user: PermissionSubject | null | undefined, permission: Permission): boolean {
  if (!user?.permissions) return false;
  return user.permissions.includes(permission);
}

export function hasAnyPermission(
  user: PermissionSubject | null | undefined,
  permissions: readonly Permission[]
): boolean {
  return permissions.some((permission) => hasPermission(user, permission));
}

export function hasAllPermissions(
  user: PermissionSubject | null | undefined,
  permissions: readonly Permission[]
): boolean {
  return permissions.every((permission) => hasPermission(user, permission));
}
