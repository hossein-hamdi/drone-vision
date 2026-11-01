import type { Metadata } from 'next';
import { Inter, Roboto } from 'next/font/google';

import { cn } from '@/lib/utils';

import './globals.css';

const inter = Inter({ subsets: ['latin'], variable: '--font-sans' });

const robotoSans = Roboto({
  variable: '--font-robot-sans',
  subsets: ['latin'],
});

export const metadata: Metadata = {
  title: 'Battle eye',
  description: '',
};

export default function RootLayout({ children }: LayoutProps<'/'>) {
  return (
    <html lang="en" className={cn('h-full', 'antialiased', robotoSans.variable, 'font-sans', inter.variable)}>
      <body>{children}</body>
    </html>
  );
}
