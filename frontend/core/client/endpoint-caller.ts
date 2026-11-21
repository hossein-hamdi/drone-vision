import type { QueryFunctionContext } from '@tanstack/react-query';
import { z, ZodError } from 'zod';

import { ApiError, ValidationError } from '@core/errors';

import type { EndpointOptions } from './@types';
import { triggerUnauthorized } from './auth-handler';

interface CallerContext {
  signal?: AbortSignal;
  pageParam?: unknown;
}

class EndpointCaller<Data, Response> {
  private readonly options: EndpointOptions<Data, Response>;

  constructor(options: EndpointOptions<Data, Response>) {
    this.options = options;
  }

  private static getErrorMessage(json: unknown, fallback: string): string {
    if (json && typeof json === 'object') {
      for (const key of ['detail', 'title', 'error', 'message'] as const) {
        if (key in json) {
          const value = (json as Record<string, unknown>)[key];
          if (typeof value === 'string' && value.trim()) return value;
        }
      }
    }
    return fallback;
  }

  /** Arrays are joined with ',' (e.g. tagIds=1,2,3); null/undefined values are skipped. */
  private static buildSearchParams(data: Record<string, unknown>): string {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(data)) {
      if (value === undefined || value === null) continue;
      if (Array.isArray(value)) {
        if (value.length > 0) params.set(key, value.join(','));
      } else {
        params.set(key, String(value));
      }
    }
    const queryString = params.toString();
    return queryString ? `?${queryString}` : '';
  }

  private async caller(callData?: Data, { signal, pageParam }: CallerContext = {}): Promise<Response> {
    let validationStage: 'requestDataSchema' | 'responseSchema' = 'requestDataSchema';
    const {
      url,
      method = 'POST',
      requestDataSchema,
      requestNormalizer,
      responseSchema,
      responseNormalizer,
      omitFromBody,
      pageParamName,
      skipUnauthorizedHandling = false,
    } = this.options;

    try {
      let finalData = callData;
      if (requestDataSchema && finalData !== undefined) {
        finalData = requestDataSchema.parse(finalData);
      }

      let payload: Record<string, unknown> | FormData | undefined;
      if (requestNormalizer) {
        payload = requestNormalizer(finalData as Data);
      } else if (finalData !== undefined) {
        payload = { ...(finalData as Record<string, unknown>) };
      }

      if (payload && !(payload instanceof FormData) && omitFromBody) {
        for (const key of omitFromBody) delete payload[key as string];
        if (Object.keys(payload).length === 0) payload = undefined;
      }

      if (pageParamName && pageParam !== undefined && pageParam !== null && !(payload instanceof FormData)) {
        payload = { ...payload, [pageParamName]: pageParam };
      }

      let finalUrl = typeof url === 'function' ? url(callData as Data) : url;

      const csrfResponse = await fetch('/api/management/auth/csrf', {
        method: 'GET',
        credentials: 'same-origin',
        cache: 'no-store',
        signal,
      });
      if (!csrfResponse.ok) {
        const json: unknown = await csrfResponse.json().catch(() => undefined);
        throw new ApiError(EndpointCaller.getErrorMessage(json, 'Failed to obtain CSRF token'), csrfResponse.status);
      }

      const csrfData: unknown = await csrfResponse.json();
      if (
        !csrfData ||
        typeof csrfData !== 'object' ||
        !('requestToken' in csrfData) ||
        typeof csrfData.requestToken !== 'string' ||
        !csrfData.requestToken.trim()
      ) {
        throw new ApiError('Invalid CSRF token response', csrfResponse.status);
      }

      const headers: Record<string, string> = { 'X-CSRF-TOKEN': csrfData.requestToken };
      const requestInit: RequestInit = { method, signal, credentials: 'same-origin', headers };
      if (method === 'GET') {
        if (payload && !(payload instanceof FormData)) finalUrl += EndpointCaller.buildSearchParams(payload);
      } else if (payload instanceof FormData) {
        requestInit.body = payload;
      } else if (payload !== undefined) {
        headers['Content-Type'] = 'application/json';
        requestInit.body = JSON.stringify(payload);
      }

      const response = await fetch(finalUrl, requestInit);
      const json: unknown = await response.json().catch(() => undefined);

      if (!response.ok) {
        throw new ApiError(EndpointCaller.getErrorMessage(json, 'Request failed'), response.status);
      }

      let finalResponse = json as Response;
      if (responseNormalizer) {
        finalResponse = responseNormalizer(finalResponse, callData);
      }

      if (responseSchema) {
        validationStage = 'responseSchema';
        finalResponse = responseSchema.parse(finalResponse);
      }

      return finalResponse;
    } catch (error) {
      if (error instanceof ApiError && error.status === 401 && !skipUnauthorizedHandling) triggerUnauthorized();
      if (error instanceof ZodError) {
        throw new ValidationError(`The ${validationStage} is not valid.`, validationStage, [z.prettifyError(error)]);
      }
      throw error;
    }
  }

  callQuery(context: QueryFunctionContext): Promise<Response> {
    const { queryKey, signal } = context;
    let queryData: Data | undefined;

    if (Array.isArray(queryKey) && queryKey.length > 0) {
      const lastQueryKeyValue = queryKey[queryKey.length - 1];
      if (typeof lastQueryKeyValue === 'object' && lastQueryKeyValue !== null) {
        queryData = lastQueryKeyValue as Data;
      }
    }

    return this.caller(queryData, { signal, pageParam: 'pageParam' in context ? context.pageParam : undefined });
  }

  callMutation(mutationData?: Data): Promise<Response> {
    if (
      mutationData &&
      typeof mutationData === 'object' &&
      'signal' in mutationData &&
      mutationData.signal instanceof AbortSignal
    ) {
      const { signal, ...rest } = mutationData as Data & { signal?: AbortSignal };
      return this.caller(rest as Data, { signal });
    }
    return this.caller(mutationData);
  }
}

export default EndpointCaller;
