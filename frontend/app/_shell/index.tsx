import type { Metadata, Viewport } from 'next';
import { Roboto, Share_Tech_Mono } from 'next/font/google';

import Providers from '@features/providers';

import '@styles/globals.css';

import { cn } from '@/lib/utils';

import AppleSplashScreens from './AppleSplashScreens';

const tech = Share_Tech_Mono({
  subsets: ['latin'],
  weight: '400',
  variable: '--font-share-tech-mono',
  display: 'swap',
});

const robotoSans = Roboto({
  subsets: ['latin'],
  variable: '--font-roboto',
  display: 'swap',
});

const LOCALIZED_META = {
  brand: 'Skyeye',
  description: 'Skyeye is a framework to unified you battle.',
} as const;

export function buildMetadata(): Metadata {
  const m = LOCALIZED_META;

  return {
    title: {
      template: `%s | ${m.brand}`,
      default: m.brand,
    },
    description: m.description,
    appleWebApp: {
      capable: true,
      statusBarStyle: 'default',
      title: m.brand,
    },
    icons: {
      icon: [
        { url: '/favicon.ico' },
        { url: '/favicon-16x16.png', sizes: '16x16', type: 'image/png' },
        { url: '/favicon-32x32.png', sizes: '32x32', type: 'image/png' },
      ],
      apple: [{ url: '/apple-touch-icon.png', sizes: '180x180', type: 'image/png' }],
    },
  };
}

export const rootViewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 1,
  viewportFit: 'cover',
  // Android/Chrome: shrink the layout viewport when the keyboard opens so
  // dvh-sized drawers resize natively. iOS ignores this hint (handled by
  // useKeyboardInset instead).
  interactiveWidget: 'resizes-content',
  // Must track --color-background in src/styles/globals.css, or the mobile
  // browser chrome shows a visible seam against the top of the page.
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: '#ffffff' },
    { media: '(prefers-color-scheme: dark)', color: '#121214' },
  ],
};

interface RootShellProps {
  children: React.ReactNode;
}

/** The <html>/<body> + provider stack shared by every root layout. */
export function RootShell({ children }: RootShellProps) {
  return (
    <html
      lang="en"
      className={cn(tech.variable, robotoSans.variable, 'bg-background h-full font-sans antialiased')}
      suppressHydrationWarning
    >
      <head>
        <AppleSplashScreens />
      </head>
      <body className="bg-background antialiased">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
