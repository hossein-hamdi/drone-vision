import type { ReactElement, ReactNode } from 'react';

import { createRoot, type Root } from 'react-dom/client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

/**
 * Fresh client per render so cache/mutation defaults never bleed across tests.
 * Grab the returned client to register test-specific query/mutation defaults.
 *
 * In the app every query key gets its fetcher from `client.registerEndpoint`.
 * Tests render components without that registry, so unmocked queries get a
 * fetcher that never settles (stays loading) instead of React Query logging
 * "No queryFn was passed" on every mount. Seed data with `setQueryData`, or
 * register your own default with `setQueryDefaults`, as before.
 */
export function makeTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, queryFn: () => new Promise<never>(() => {}) },
      mutations: { retry: false },
    },
  });
}

interface ProviderRenderOptions {
  queryClient?: QueryClient;
}

const Providers = ({ queryClient, children }: { queryClient: QueryClient; children: ReactNode }) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);

const mountedRoots = new Set<{ root: Root; container: HTMLElement }>();
let baseStylesInjected = false;

function ensureBaseTestStyles() {
  if (baseStylesInjected || typeof document === 'undefined') return;
  const style = document.createElement('style');
  // Base UI Checkbox renders a <span> whose hitbox depends on Tailwind's size-4 utility.
  style.textContent = '[data-slot="checkbox"] { display: inline-block; width: 16px; height: 16px; }';
  document.head.appendChild(style);
  baseStylesInjected = true;
}

export function cleanup() {
  for (const entry of mountedRoots) {
    entry.root.unmount();
    entry.container.remove();
  }
  mountedRoots.clear();
}

export function render(ui: ReactElement, options: ProviderRenderOptions = {}) {
  ensureBaseTestStyles();
  const { queryClient = makeTestQueryClient() } = options;
  const container = document.createElement('div');
  document.body.appendChild(container);

  const root = createRoot(container);
  mountedRoots.add({ root, container });

  root.render(<Providers queryClient={queryClient}>{ui}</Providers>);

  return {
    container,
    queryClient,
    rerender: (nextUi: ReactElement) => {
      root.render(<Providers queryClient={queryClient}>{nextUi}</Providers>);
    },
    unmount: () => {
      root.unmount();
      container.remove();
      mountedRoots.delete({ root, container });
    },
  };
}
