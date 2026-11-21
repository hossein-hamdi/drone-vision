import { z } from 'zod';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  image: z.instanceof(File),
});

const responseSchema = z.object({
  profileImageUrl: z.string(),
});

type RequestData = z.infer<typeof requestDataSchema> & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['profile', 'image', 'upload'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/profile-image',
  method: 'POST',
  type: 'mutation',
  requestDataSchema,
  requestNormalizer: ({ image }) => {
    const formData = new FormData();
    formData.append('Image', image);
    return formData;
  },
  responseSchema,
});

export { keyGenerator as uploadProfileImageKeyGenerator };
export type { RequestData as UploadProfileImageRequestData, Response as UploadProfileImageResponse };
