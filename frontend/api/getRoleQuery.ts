import { z } from 'zod';

import client from '@core/client';
import type { QueryKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  id: z.string().trim().min(1),
});

const responseSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  name: z.string(),
  permissions: z.array(z.string()),
  createdAt: z.string(),
  updatedAt: z.string(),
});

type RequestData = z.infer<typeof requestDataSchema>;
type Response = z.infer<typeof responseSchema>;

const keyGenerator: QueryKeyGenerator<RequestData> = (params) => ['roles', 'detail', params];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: ({ id }) => `/api/management/roles/${id}`,
  method: 'GET',
  type: 'query',
  omitFromBody: ['id'],
  requestDataSchema,
  responseSchema,
});

export { keyGenerator as getRoleKeyGenerator };
export type { RequestData as GetRoleRequestData, Response as GetRoleResponse };
