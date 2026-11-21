import { z } from 'zod';

import client from '@core/client';
import type { QueryKeyGenerator } from '@core/client/@types';

const responseSchema = z.object({
  id: z.union([z.string(), z.number()]).transform(String),
  fullName: z.string(),
  userName: z.string(),
  profileImageUrl: z
    .string()
    .nullish()
    .transform((url) => url ?? ''),
  roles: z.array(z.string()),
});

type RequestData = void;
type Response = z.infer<typeof responseSchema>;

const keyGenerator: QueryKeyGenerator = () => ['profile'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/profile',
  method: 'GET',
  type: 'query',
  responseSchema,
});

export { keyGenerator as getProfileKeyGenerator };
export type { RequestData as GetProfileRequestData, Response as GetProfileResponse };
