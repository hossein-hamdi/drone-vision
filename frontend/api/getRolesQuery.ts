import { z } from 'zod';

import client from '@core/client';
import type { QueryKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  RoleName: z.string().trim().min(1).optional(),
  CreatedAtFrom: z.string().trim().min(1).optional(),
  CreatedAtTo: z.string().trim().min(1).optional(),
  Page: z.number().int().min(1),
  PageSize: z.number().int().min(1).max(100),
});

const roleSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  name: z.string(),
  permissions: z.array(z.string()),
  createdAt: z.string(),
  updatedAt: z.string(),
});

const responseSchema = z.object({
  items: z.array(roleSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
});

type RequestData = z.infer<typeof requestDataSchema>;
type Response = z.infer<typeof responseSchema>;

const keyGenerator: QueryKeyGenerator<RequestData> = (params) => ['roles', 'list', params];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/roles',
  method: 'GET',
  type: 'query',
  requestDataSchema,
  responseSchema,
});

export { keyGenerator as getRolesKeyGenerator };
export type { RequestData as GetRolesRequestData, Response as GetRolesResponse };
