import { z } from 'zod';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  id: z.string().trim().min(1),
});

type RequestData = z.infer<typeof requestDataSchema> & { signal?: AbortSignal };

const keyGenerator: MutationKeyGenerator = () => ['users', 'delete'];

client.registerEndpoint<RequestData, void>(keyGenerator, {
  url: ({ id }) => `/api/management/users/${id}`,
  method: 'DELETE',
  type: 'mutation',
  omitFromBody: ['id'],
  requestDataSchema,
});

export { keyGenerator as deleteUserKeyGenerator };
export type { RequestData as DeleteUserRequestData };
