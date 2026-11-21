import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

type RequestData = { signal?: AbortSignal } | void;

const keyGenerator: MutationKeyGenerator = () => ['profile', 'image', 'delete'];

client.registerEndpoint<RequestData, void>(keyGenerator, {
  url: '/api/management/profile-image',
  method: 'DELETE',
  type: 'mutation',
});

export { keyGenerator as deleteProfileImageKeyGenerator };
export type { RequestData as DeleteProfileImageRequestData };
