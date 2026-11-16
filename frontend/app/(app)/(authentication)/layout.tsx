import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Login',
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return <main>{children}</main>;
}
