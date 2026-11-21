import { UserRound } from 'lucide-react';

import AuthGuard from '@features/guards/auth-guard';

import DashboardBreadcrumb from '@components/breadcrumb';

import { ProfileContent } from './components/profile-content';

export function ProfilePage() {
  return (
    <AuthGuard>
      <section className="flex flex-col gap-6">
        <DashboardBreadcrumb
          title="Profile"
          description="Manage your personal details, profile photo, and account roles."
          icon={<UserRound />}
        />
        <ProfileContent />
      </section>
    </AuthGuard>
  );
}

export default ProfilePage;
