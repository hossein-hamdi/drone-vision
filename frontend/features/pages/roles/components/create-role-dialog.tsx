'use client';

import * as React from 'react';

import { createRoleKeyGenerator, type CreateRoleRequestData, type CreateRoleResponse } from '@api/createRoleMutation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, ShieldPlus } from 'lucide-react';
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
  DialogTrigger,
} from '@/components/ui/dialog';

import { RoleFormFields } from './role-form-fields';

export function CreateRoleDialog() {
  const [open, setOpen] = React.useState(false);
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { createSignal, abort } = useModalAbort({ open });

  const form = useForm<RoleSchema>({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      name: '',
      permissions: [],
    },
  });

  const { mutateAsync: createRoleAsync, isPending: isCreating } = useMutation<
    CreateRoleResponse,
    Error,
    CreateRoleRequestData
  >({
    mutationKey: createRoleKeyGenerator(),
  });

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      abort();
      form.reset();
    }
    setOpen(nextOpen);
  };

  const handleSubmit = async (values: RoleSchema) => {
    const signal = createSignal();
    try {
      await createRoleAsync({
        name: values.name,
        permissions: values.permissions,
        signal,
      });
      await queryClient.invalidateQueries({ queryKey: ['roles'] });
      showToast('Role created successfully', 'success');
      handleOpenChange(false);
    } catch (error) {
      if (isAbortError(error)) return;
      showToast(error instanceof Error ? error.message : 'Failed to create role', 'error');
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger render={<Button />}>
        <ShieldPlus data-icon="inline-start" aria-hidden="true" />
        Create Role
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create Role</DialogTitle>
          <DialogDescription>Add a new role and select its permission categories.</DialogDescription>
        </DialogHeader>

        <form
          id="create-role-form"
          onSubmit={(event) => void form.handleSubmit(handleSubmit)(event)}
          className="flex flex-col gap-4"
        >
          <RoleFormFields control={form.control} disabled={isCreating} idPrefix="create-role" />
        </form>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" form="create-role-form" disabled={isCreating}>
            {isCreating && <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />}
            Create Role
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
