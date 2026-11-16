import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

import { authConfig } from '@configs/auth.config';

const AUTH_PAGES = new Set<string>(authConfig.routes.auth);
const PUBLIC_PAGES = new Set<string>(authConfig.routes.public);

// Cookie presence only controls navigation. The Management API validates sessions.
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const hasSession = Boolean(request.cookies.get(authConfig.sessionCookie)?.value);

  if (AUTH_PAGES.has(pathname) && hasSession) {
    return NextResponse.redirect(new URL(authConfig.defaultRedirect, request.url));
  }

  if (!PUBLIC_PAGES.has(pathname) && !hasSession) {
    const loginUrl = new URL('/login', request.url);
    loginUrl.searchParams.set('rp', `${pathname}${search}`);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico|opengraph-image|.*\\.[\\w]+$).*)'],
};
