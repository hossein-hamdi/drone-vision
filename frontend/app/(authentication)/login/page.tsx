import type { Metadata } from 'next';

import LoginPage from '@features/pages/login';

export const metadata: Metadata = {
  title: 'Login',
};

export default function Page() {
  return <LoginPage />;
}
