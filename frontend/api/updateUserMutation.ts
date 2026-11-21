import { z } from 'zod';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  id: z.string().trim().min(1),
  fullName: z.string().trim().min(1, 'Full name is required').max(200),
  userName: z.string().trim().min(1, 'Username is required').max(256),
  roles: z.array(z.string()).max(100),
});

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
  createdAt: z.string(),
  updatedAt: z.string(),
});

type RequestData = z.infer<typeof requestDataSchema> & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['users', 'update'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: ({ id }) => `/api/management/users/${id}`,
  method: 'PUT',
  type: 'mutation',
  omitFromBody: ['id'],
  requestDataSchema,
  responseSchema,
});

export { keyGenerator as updateUserKeyGenerator };
export type { RequestData as UpdateUserRequestData, Response as UpdateUserResponse };
