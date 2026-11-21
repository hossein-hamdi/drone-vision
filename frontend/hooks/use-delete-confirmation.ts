'use client';

import { useCallback, useState } from 'react';

import { isAbortError, useModalAbort } from '@hooks/use-modal-abort';

interface UseDeleteConfirmationOptions<TId extends string | number = string> {
  onDelete: (id: TId, signal: AbortSignal) => Promise<void>;
  onError?: (error: unknown) => void;
}

export function useDeleteConfirmation<T extends { id: string | number }>(
  options: UseDeleteConfirmationOptions<T['id']>
) {
  const [itemToDelete, setItemToDelete] = useState<T | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [deletingId, setDeletingId] = useState<T['id'] | null>(null);

  const { createSignal, abort } = useModalAbort({ open: isModalOpen });

  const openModal = useCallback((item: T) => {
    setItemToDelete(item);
    setIsModalOpen(true);
  }, []);

  const closeModal = useCallback(() => {
    abort();
    setItemToDelete(null);
    setIsModalOpen(false);
  }, [abort]);

  const confirmDelete = useCallback(async () => {
    if (!itemToDelete) return;

    const signal = createSignal();
    setDeletingId(itemToDelete.id);

    try {
      await options.onDelete(itemToDelete.id, signal);
      closeModal();
    } catch (err) {
      if (isAbortError(err)) return;
      options.onError?.(err);
    } finally {
      setDeletingId(null);
    }
  }, [itemToDelete, options, createSignal, closeModal]);

  return {
    itemToDelete,
    isModalOpen,
    deletingId,
    openModal,
    closeModal,
    confirmDelete,
  };
}
