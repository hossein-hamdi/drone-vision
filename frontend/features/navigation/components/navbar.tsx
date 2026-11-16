'use client';

import { usePathname } from 'next/navigation';

import { Separator } from '@components/ui/separator';
import { SidebarTrigger } from '@components/ui/sidebar';

import { sidebar } from '@/constant/sidebar';

import AccountMenu from './account-menu';

export default function Navbar() {
  const pathname = usePathname();
  const currentPage = sidebar.find((item) => item.url === pathname)?.title;

  return (
    <header className="bg-background border-border flex h-16 shrink-0 items-center justify-between gap-4 border-b px-4 sm:px-6">
      <div className="flex min-w-0 items-center justify-center gap-3">
        <SidebarTrigger aria-label="Toggle navigation" />
        <Separator orientation="vertical" className="self-center" />
        <span className="truncate text-sm font-medium">{currentPage}</span>
      </div>
      <AccountMenu />
    </header>
  );
}
