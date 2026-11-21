import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { playwright } from '@vitest/browser-playwright';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

const sharedResolve = {
  alias: {
    '@types': path.resolve(rootDir, './@types/index.ts'),
    '@schemas': path.resolve(rootDir, './@schemas/index.ts'),
    '@actions': path.resolve(rootDir, './actions'),
    '@api': path.resolve(rootDir, './api'),
    '@components': path.resolve(rootDir, './components'),
    '@configs': path.resolve(rootDir, './configs'),
    '@constants': path.resolve(rootDir, './constants'),
    '@core': path.resolve(rootDir, './core'),
    '@features': path.resolve(rootDir, './features'),
    '@hooks': path.resolve(rootDir, './hooks'),
    '@storages': path.resolve(rootDir, './storages'),
    '@stores': path.resolve(rootDir, './stores'),
    '@styles': path.resolve(rootDir, './styles'),
    '@utils': path.resolve(rootDir, './utils'),
    '@': rootDir,
  },
};

/** @type {import('vitest/config').ViteUserConfig} */
export default {
  resolve: sharedResolve,
  test: {
    projects: [
      {
        extends: true,
        test: {
          name: 'unit',
          environment: 'node',
          include: ['**/*.test.ts'],
        },
      },
      {
        extends: true,
        define: {
          'process.env': {},
        },
        test: {
          name: 'browser',
          include: ['**/*.test.tsx'],
          browser: {
            enabled: true,
            headless: true,
            provider: playwright(),
            instances: [{ browser: 'chromium' }],
          },
        },
      },
    ],
  },
};
