import { z } from 'zod';

import { roleSchema } from '@schemas';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const requestDataSchema = roleSchema.extend({
  id: z.string().trim().min(1),
});

const responseSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  name: z.string(),
  permissions: z.array(z.string()),
  createdAt: z.string(),
  updatedAt: z.string(),
});

type RequestData = z.infer<typeof requestDataSchema> & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['roles', 'update'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: ({ id }) => `/api/management/roles/${id}`,
  method: 'PUT',
  type: 'mutation',
  omitFromBody: ['id'],
  requestDataSchema,
  responseSchema,
});

export { keyGenerator as updateRoleKeyGenerator };
export type { RequestData as UpdateRoleRequestData, Response as UpdateRoleResponse };
