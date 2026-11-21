import { z } from 'zod';

import { updateProfileSchema, type UpdateProfileSchema } from '@schemas';

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
  createdAt: z.string(),
  updatedAt: z.string(),
});

type RequestData = UpdateProfileSchema & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['profile', 'update'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/profile',
  method: 'PUT',
  type: 'mutation',
  requestDataSchema: updateProfileSchema,
  responseSchema,
});

export { keyGenerator as updateProfileKeyGenerator };
export type { RequestData as UpdateProfileRequestData, Response as UpdateProfileResponse };
