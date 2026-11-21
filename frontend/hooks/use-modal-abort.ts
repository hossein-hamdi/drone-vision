'use client';

import * as React from 'react';

import { type QueryKey, useQueryClient } from '@tanstack/react-query';

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export function useModalAbort({ open, queryKey }: { open: boolean; queryKey?: QueryKey }) {
  const queryClient = useQueryClient();
  const abortControllerRef = React.useRef<AbortController | null>(null);

  const abort = React.useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
    if (queryKey) {
      void queryClient.cancelQueries({ queryKey });
    }
  }, [queryClient, queryKey]);

  React.useEffect(() => {
    if (!open) {
      abort();
    }
    return () => {
      abort();
    };
  }, [open, abort]);

  const createSignal = React.useCallback(() => {
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;
    return controller.signal;
  }, []);

  return { createSignal, abort };
}
