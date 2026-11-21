import { LayoutDashboard, type LucideIcon, Shield, UserRound } from 'lucide-react';

import { type Permission, PERMISSIONS } from '@core/auth';

interface SidebarItem {
  title: string;
  url: string;
  icon: LucideIcon;
  permission?: Permission;
}

export const sidebar: SidebarItem[] = [
  { title: 'Overview', url: '/', icon: LayoutDashboard },
  { title: 'Users', url: '/users', icon: UserRound, permission: PERMISSIONS.users.view },
  { title: 'Roles', url: '/roles', icon: Shield, permission: PERMISSIONS.roles.view },
];
