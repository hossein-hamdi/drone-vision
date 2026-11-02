'use client';

import { ThemeProvider } from 'next-themes';

import ClientProvider from '@core/client/provider';

interface ProvidersProps {
  children: React.ReactNode;
}

const Providers = ({ children }: ProvidersProps) => (
  <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
    <ClientProvider>{children}</ClientProvider>
  </ThemeProvider>
);

export default Providers;
