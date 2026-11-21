import { z } from 'zod';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  id: z.string().trim().min(1),
});

type RequestData = z.infer<typeof requestDataSchema> & { signal?: AbortSignal };

const keyGenerator: MutationKeyGenerator = () => ['users', 'profile-image', 'delete'];

client.registerEndpoint<RequestData, void>(keyGenerator, {
  url: ({ id }) => `/api/management/users/${id}/profile-image`,
  method: 'DELETE',
  type: 'mutation',
  omitFromBody: ['id'],
  requestDataSchema,
});

export { keyGenerator as deleteUserProfileImageKeyGenerator };
export type { RequestData as DeleteUserProfileImageRequestData };
