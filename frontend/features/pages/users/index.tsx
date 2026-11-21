import { Users } from 'lucide-react';

import AuthGuard from '@features/guards/auth-guard';

import DashboardBreadcrumb from '@components/breadcrumb';

import { CreateUserDialog } from './components/create-user-dialog';
import { UsersTable } from './components/users-table';

export function UsersPage() {
  return (
    <AuthGuard>
      <div className="flex flex-col gap-6">
        <DashboardBreadcrumb
          title="User Management"
          description="View, invite, and manage team members, roles, and access permissions."
          icon={<Users />}
          actions={<CreateUserDialog />}
        />
        <UsersTable />
      </div>
    </AuthGuard>
  );
}

export default UsersPage;
