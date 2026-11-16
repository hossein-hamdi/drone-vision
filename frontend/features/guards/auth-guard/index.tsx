'use client';

import { useEffect } from 'react';
import type { FC, PropsWithChildren } from 'react';

import FullPageLoader from '@components/full-page-load';
import { Button } from '@components/ui/button';

import { useAuth } from '@hooks/use-auth';

const AuthGuard: FC<PropsWithChildren> = ({ children }) => {
  const { user, loading, error, isUnauthorized, logout, refetch } = useAuth();

  useEffect(() => {
    if (!loading && !user && isUnauthorized) void logout();
  }, [user, loading, isUnauthorized, logout]);

  if (!loading && !user && error && !isUnauthorized) {
    return (
      <div className="bg-background flex min-h-dvh flex-col items-center justify-center gap-4 p-6">
        <p role="alert">{error}</p>
        <Button onClick={() => void refetch()}>Try again</Button>
      </div>
    );
  }

  // Keep the loader up while signed out and redirecting — returning null here
  // paints a bare white page for the whole cookie-clear + navigation window.
  if (loading || !user) return <FullPageLoader />;

  return <>{children}</>;
};

export default AuthGuard;
