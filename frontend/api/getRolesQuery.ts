import { z } from 'zod';

import client from '@core/client';
import type { QueryKeyGenerator } from '@core/client/@types';

const roleSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  name: z.string(),
  permissions: z.array(z.string()),
});

type Response = z.infer<typeof roleSchema>[];
type RequestData = void;

const responseSchema = z.array(roleSchema);

const keyGenerator: QueryKeyGenerator = () => ['roles', 'list'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/roles',
  method: 'GET',
  type: 'query',
  responseSchema,
});

export { keyGenerator as getRolesKeyGenerator };
export type { RequestData as GetRolesRequestData, Response as GetRolesResponse };
