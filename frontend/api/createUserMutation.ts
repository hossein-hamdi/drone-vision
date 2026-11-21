import { z } from 'zod';

import { createUserSchema, type CreateUserSchema } from '@schemas';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

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

type RequestData = CreateUserSchema & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['users', 'create'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/users',
  method: 'POST',
  type: 'mutation',
  requestDataSchema: createUserSchema,
  responseSchema,
});

export { keyGenerator as createUserKeyGenerator };
export type { RequestData as CreateUserRequestData, Response as CreateUserResponse };
