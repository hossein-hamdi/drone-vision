import { z } from 'zod';

import client from '@core/client';
import type { QueryKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  Role: z.string().trim().min(1).optional(),
  Name: z.string().trim().min(1).optional(),
  Page: z.number().int().min(1),
  PageSize: z.number().int().min(1).max(100),
});

const userSchema = z.object({
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

const responseSchema = z.object({
  items: z.array(userSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
});

type RequestData = z.infer<typeof requestDataSchema>;
type Response = z.infer<typeof responseSchema>;

const keyGenerator: QueryKeyGenerator<RequestData> = (params) => ['users', 'list', params];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/users',
  method: 'GET',
  type: 'query',
  requestDataSchema,
  responseSchema,
});

export { keyGenerator as getUsersKeyGenerator };
export type { RequestData as GetUsersRequestData, Response as GetUsersResponse };
