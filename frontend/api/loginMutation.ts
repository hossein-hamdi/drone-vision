import { z } from 'zod';

import { loginSchema } from '@schemas';
import type { LoginSchema } from '@schemas';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

type RequestData = LoginSchema;
type Response = z.infer<typeof responseSchema>;

const responseSchema = z.object({
  accessToken: z.string(),
  tokenType: z.string(),
  expiresIn: z.number(),
  refreshTokenExpiresAt: z.iso.datetime({ offset: true }),
});

const keyGenerator: MutationKeyGenerator = () => ['auth', 'login'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/auth/login',
  method: 'POST',
  type: 'mutation',
  requestDataSchema: loginSchema,
  requestNormalizer: ({ username, password }) => ({ userName: username, password }),
  responseSchema,
  // Invalid credentials should reach the form without triggering global sign-out.
  skipUnauthorizedHandling: true,
});

export { keyGenerator as loginKeyGenerator };
export type { RequestData as LoginRequestData, Response as LoginResponse };
