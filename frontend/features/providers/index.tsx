'use client';

import { ThemeProvider } from 'next-themes';

import { NuqsAdapter } from 'nuqs/adapters/next/app';

import ClientProvider from '@core/client/provider';

import Toaster from '@components/toast/toaster';
import { TooltipProvider } from '@components/ui/tooltip';

interface ProvidersProps {
  children: React.ReactNode;
}

const Providers = ({ children }: ProvidersProps) => (
  <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
    <ClientProvider>
      <NuqsAdapter>
        <TooltipProvider>
          {children}
          <Toaster />
        </TooltipProvider>
      </NuqsAdapter>
    </ClientProvider>
  </ThemeProvider>
);

export default Providers;
