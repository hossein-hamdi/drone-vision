import { z } from 'zod';

import type { AuthUser } from '@types';

import client from '@core/client';
import type { QueryKeyGenerator } from '@core/client/@types';

type RequestData = void;
type Response = AuthUser;

const responseSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  fullName: z.string(),
  userName: z.string(),
  profileImageUrl: z
    .string()
    .nullish()
    .transform((url) => url ?? ''),
  roles: z.array(z.string()),
  permissions: z.array(z.string()),
});

const keyGenerator: QueryKeyGenerator = () => ['auth', 'me'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/auth/me',
  method: 'GET',
  type: 'query',
  responseSchema,
});

export { keyGenerator as getMeKeyGenerator };
export type { RequestData as GetMeRequestData, Response as GetMeResponse };
