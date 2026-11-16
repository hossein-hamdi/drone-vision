export const authConfig = {
  sessionCookie: '__Secure-DroneVision.Refresh',
  defaultRedirect: '/',
  routes: {
    public: ['/login'],
    auth: ['/login'],
  },
} as const;
