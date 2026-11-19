'use client';

import { useTheme } from 'next-themes';

import { Moon, Sun } from 'lucide-react';

import { Button } from '@components/ui/button';
import { SidebarTrigger } from '@components/ui/sidebar';

import AccountMenu from './account-menu';

export default function Navbar() {
  const { resolvedTheme, setTheme } = useTheme();
  return (
    <header className="bg-background border-border flex h-16 shrink-0 items-center justify-between gap-4 border-b px-4 sm:px-6">
      <div className="flex min-w-0 items-center justify-center gap-3">
        <SidebarTrigger aria-label="Toggle navigation" />
      </div>
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          aria-label="Toggle color theme"
          onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
        >
          <Sun className="hidden dark:block" />
          <Moon className="dark:hidden" />
        </Button>
        <AccountMenu />
      </div>
    </header>
  );
}
