'use client';

import * as React from 'react';

import { getRoleKeyGenerator, type GetRoleResponse } from '@api/getRoleQuery';
import { updateRoleKeyGenerator, type UpdateRoleRequestData, type UpdateRoleResponse } from '@api/updateRoleMutation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Loader2, RefreshCw } from 'lucide-react';
import { useForm } from 'react-hook-form';

import { roleSchema, type RoleSchema } from '@schemas';

import { isAbortError, useModalAbort } from '@hooks/use-modal-abort';

import { useToast } from '@stores/toast';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';

import { RoleFormFields } from './role-form-fields';

interface EditRoleDialogProps {
  roleId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function EditRoleDialog({ roleId, open, onOpenChange }: EditRoleDialogProps) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();

  const roleQueryKey = React.useMemo(() => (roleId ? getRoleKeyGenerator({ id: roleId }) : undefined), [roleId]);

  const { createSignal, abort } = useModalAbort({
    open,
    queryKey: roleQueryKey,
  });

  const {
    data: role,
    isLoading: isLoadingRole,
    isError: isRoleError,
    error: roleError,
    refetch: refetchRole,
  } = useQuery<GetRoleResponse>({
    queryKey: getRoleKeyGenerator({ id: roleId ?? '' }),
    enabled: open && Boolean(roleId),
  });

  const form = useForm<RoleSchema>({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      name: '',
      permissions: [],
    },
  });

  React.useEffect(() => {
    if (open && role) {
      form.reset({
        name: role.name,
        permissions: role.permissions,
      });
    }
  }, [open, role, form]);

  const { mutateAsync: updateRoleAsync, isPending: isUpdating } = useMutation<
    UpdateRoleResponse,
    Error,
    UpdateRoleRequestData
  >({
    mutationKey: updateRoleKeyGenerator(),
  });

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      abort();
      form.reset();
    }
    onOpenChange(nextOpen);
  };

  const handleSubmit = async (values: RoleSchema) => {
    if (!roleId) return;
    const signal = createSignal();
    try {
      await updateRoleAsync({
        id: roleId,
        name: values.name,
        permissions: values.permissions,
        signal,
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['roles'] }),
        queryClient.invalidateQueries({ queryKey: ['users'] }),
        queryClient.invalidateQueries({ queryKey: ['auth', 'me'] }),
        queryClient.invalidateQueries({ queryKey: ['profile'] }),
      ]);
      showToast('Role updated successfully', 'success');
      handleOpenChange(false);
    } catch (error) {
      if (isAbortError(error)) return;
      showToast(error instanceof Error ? error.message : 'Failed to update role', 'error');
    }
  };

  let dialogBody: React.ReactNode;
  if (isLoadingRole && !role) {
    dialogBody = (
      <div className="flex flex-col gap-4 py-2">
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-9 w-full" />
      </div>
    );
  } else if (isRoleError && !role) {
    dialogBody = (
      <div className="flex flex-col items-center justify-center gap-3 py-4 text-center">
        <AlertCircle className="text-destructive size-8" aria-hidden="true" />
        <div className="flex flex-col gap-1">
          <p className="text-foreground text-sm font-medium">Failed to load role</p>
          <p className="text-muted-foreground text-xs">{roleError?.message || 'An unexpected error occurred.'}</p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={() => void refetchRole()}>
          <RefreshCw data-icon="inline-start" aria-hidden="true" />
          Try again
        </Button>
      </div>
    );
  } else {
    dialogBody = (
      <form
        id="edit-role-form"
        onSubmit={(event) => void form.handleSubmit(handleSubmit)(event)}
        className="flex flex-col gap-4"
      >
        <RoleFormFields control={form.control} disabled={isUpdating} idPrefix="edit-role" />
      </form>
    );
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Edit Role</DialogTitle>
          <DialogDescription>Update role name and assigned permission categories.</DialogDescription>
        </DialogHeader>

        {dialogBody}

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" form="edit-role-form" disabled={isLoadingRole || isUpdating || (isRoleError && !role)}>
            {isUpdating && <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />}
            Save changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
