'use client';

import { Users } from 'lucide-react';

import { PERMISSIONS } from '@core/auth';

import AuthGuard from '@features/guards/auth-guard';

import DashboardBreadcrumb from '@components/breadcrumb';

import { useAuth } from '@hooks/use-auth';

import { CreateUserDialog } from './components/create-user-dialog';
import { UsersTable } from './components/users-table';

export function UsersPage() {
  const { can } = useAuth();

  return (
    <AuthGuard permission={PERMISSIONS.users.view}>
      <div className="flex flex-col gap-6">
        <DashboardBreadcrumb
          title="User Management"
          description="View, invite, and manage team members, roles, and access permissions."
          icon={<Users />}
          actions={can(PERMISSIONS.users.create) ? <CreateUserDialog /> : undefined}
        />
        <UsersTable />
      </div>
    </AuthGuard>
  );
}

export default UsersPage;
