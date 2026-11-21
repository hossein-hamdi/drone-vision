import { z } from 'zod';

import { roleSchema, type RoleSchema } from '@schemas';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const responseSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  name: z.string(),
  permissions: z.array(z.string()),
  createdAt: z.string(),
  updatedAt: z.string(),
});

type RequestData = RoleSchema & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['roles', 'create'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/roles',
  method: 'POST',
  type: 'mutation',
  requestDataSchema: roleSchema,
  responseSchema,
});

export { keyGenerator as createRoleKeyGenerator };
export type { RequestData as CreateRoleRequestData, Response as CreateRoleResponse };
