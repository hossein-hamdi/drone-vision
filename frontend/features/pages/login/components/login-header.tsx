'use client';

import Image from 'next/image';
import Link from 'next/link';

import ThemeToggle from '@components/theme-toggle';

export default function LoginHeader() {
  return (
    <header className="mx-auto flex h-22 max-w-360 items-center justify-between px-6 sm:px-10 lg:px-16">
      <Link href="/" aria-label="Skyeye home" className="flex items-center gap-2.5">
        <Image src="/icons/icon-512.png" height={36} width={36} alt="" className="rounded-full" priority />
        <span className="font-mono text-2xl tracking-tight">Skyeye</span>
      </Link>
      <ThemeToggle />
    </header>
  );
}
