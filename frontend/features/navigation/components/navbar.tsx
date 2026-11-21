'use client';

import ThemeToggle from '@components/theme-toggle';
import { SidebarTrigger } from '@components/ui/sidebar';

import AccountMenu from './account-menu';

export default function Navbar() {
  return (
    <header className="bg-background border-border flex h-16 shrink-0 items-center justify-between gap-4 border-b px-4 sm:px-6">
      <div className="flex min-w-0 items-center justify-center gap-3">
        <SidebarTrigger aria-label="Toggle navigation" />
      </div>
      <div className="flex items-center gap-3">
        <ThemeToggle />
        <AccountMenu />
      </div>
    </header>
  );
}
