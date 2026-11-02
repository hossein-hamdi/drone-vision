import { buildMetadata, RootShell } from './_shell';

export { rootViewport as viewport } from './_shell';

export async function generateMetadata() {
  return buildMetadata();
}

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return <RootShell>{children}</RootShell>;
}
