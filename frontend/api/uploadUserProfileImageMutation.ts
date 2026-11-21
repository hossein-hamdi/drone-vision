import { z } from 'zod';

import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

const requestDataSchema = z.object({
  id: z.string().trim().min(1),
  image: z.instanceof(File),
});

const responseSchema = z.object({
  profileImageUrl: z.string(),
});

type RequestData = z.infer<typeof requestDataSchema> & { signal?: AbortSignal };
type Response = z.infer<typeof responseSchema>;

const keyGenerator: MutationKeyGenerator = () => ['users', 'profile-image', 'upload'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: ({ id }) => `/api/management/users/${id}/profile-image`,
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

export { keyGenerator as uploadUserProfileImageKeyGenerator };
export type { RequestData as UploadUserProfileImageRequestData, Response as UploadUserProfileImageResponse };
