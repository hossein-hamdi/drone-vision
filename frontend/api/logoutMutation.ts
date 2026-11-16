import client from '@core/client';
import type { MutationKeyGenerator } from '@core/client/@types';

type RequestData = void;
type Response = void;

const keyGenerator: MutationKeyGenerator = () => ['auth', 'logout'];

client.registerEndpoint<RequestData, Response>(keyGenerator, {
  url: '/api/management/auth/logout',
  method: 'POST',
  type: 'mutation',
});

export { keyGenerator as logoutKeyGenerator };
export type { RequestData as LogoutRequestData, Response as LogoutResponse };
