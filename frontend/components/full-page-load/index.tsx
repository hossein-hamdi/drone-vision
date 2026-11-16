'use client';

import type { FC } from 'react';

import Image from 'next/image';

const FullPageLoader: FC = () => {
  return (
    <div className="bg-background fixed inset-0 z-50 flex items-center justify-center">
      <div className="flex flex-col items-center gap-9">
        <Image src="/icons/icon-512.png" width={80} height={80} alt="Skyeye" className="rounded-full" priority />
        <div
          role="status"
          aria-label="Loading"
          className="border-border border-t-primary h-6 w-6 animate-spin rounded-full border-2"
        />
      </div>
    </div>
  );
};

export default FullPageLoader;
