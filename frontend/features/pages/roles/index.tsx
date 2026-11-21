'use client';

import { Shield } from 'lucide-react';

import { PERMISSIONS } from '@core/auth';

import AuthGuard from '@features/guards/auth-guard';

import DashboardBreadcrumb from '@components/breadcrumb';

import { useAuth } from '@hooks/use-auth';

import { CreateRoleDialog } from './components/create-role-dialog';
import { RolesTable } from './components/roles-table';

export function RolesPage() {
  const { can } = useAuth();

  return (
    <AuthGuard permission={PERMISSIONS.roles.view}>
      <div className="flex flex-col gap-6">
        <DashboardBreadcrumb
          title="Role Management"
          description="Create, inspect, and manage access roles and permission categories."
          icon={<Shield />}
          actions={can(PERMISSIONS.roles.create) ? <CreateRoleDialog /> : undefined}
        />
        <RolesTable />
      </div>
    </AuthGuard>
  );
}

export default RolesPage;
