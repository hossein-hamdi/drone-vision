'use client';

import * as React from 'react';

import { createUserKeyGenerator, type CreateUserRequestData, type CreateUserResponse } from '@api/createUserMutation';
import { getRolesKeyGenerator, type GetRolesResponse } from '@api/getRolesQuery';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, UserPlus } from 'lucide-react';
import { Controller, useForm } from 'react-hook-form';

import { createUserSchema, type CreateUserSchema } from '@schemas';

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
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

export function CreateUserDialog() {
  const [open, setOpen] = React.useState(false);
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { createSignal, abort } = useModalAbort({ open });

  const { data: rolesData } = useQuery<GetRolesResponse>({
    queryKey: getRolesKeyGenerator({ Page: 1, PageSize: 100 }),
    enabled: open,
  });

  const form = useForm<CreateUserSchema>({
    resolver: zodResolver(createUserSchema),
    defaultValues: {
      fullName: '',
      userName: '',
      password: '',
      roles: [],
    },
  });

  const { mutateAsync: createUserAsync, isPending: isCreating } = useMutation<
    CreateUserResponse,
    Error,
    CreateUserRequestData
  >({
    mutationKey: createUserKeyGenerator(),
  });

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      abort();
      form.reset();
    }
    setOpen(nextOpen);
  };

  const handleSubmit = async (values: CreateUserSchema) => {
    const signal = createSignal();
    try {
      await createUserAsync({
        fullName: values.fullName,
        userName: values.userName,
        password: values.password,
        roles: values.roles,
        signal,
      });
      await queryClient.invalidateQueries({ queryKey: ['users'] });
      showToast('User created successfully', 'success');
      handleOpenChange(false);
    } catch (error) {
      if (isAbortError(error)) return;
      showToast(error instanceof Error ? error.message : 'Failed to create user', 'error');
    }
  };

  const availableRoles = rolesData?.items ?? [];

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger render={<Button />}>
        <UserPlus data-icon="inline-start" aria-hidden="true" />
        Create User
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create User</DialogTitle>
          <DialogDescription>Add a new team member and assign their roles.</DialogDescription>
        </DialogHeader>

        <form
          id="create-user-form"
          onSubmit={(event) => void form.handleSubmit(handleSubmit)(event)}
          className="flex flex-col gap-4"
        >
          <FieldGroup>
            <Controller
              name="fullName"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="create-user-fullname">Full name</FieldLabel>
                  <Input
                    {...field}
                    id="create-user-fullname"
                    aria-invalid={fieldState.invalid}
                    aria-describedby={fieldState.invalid ? 'create-user-fullname-error' : undefined}
                    disabled={isCreating}
                  />
                  {fieldState.invalid && <FieldError id="create-user-fullname-error" errors={[fieldState.error]} />}
                </Field>
              )}
            />

            <Controller
              name="userName"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="create-user-username">Username</FieldLabel>
                  <Input
                    {...field}
                    id="create-user-username"
                    aria-invalid={fieldState.invalid}
                    aria-describedby={fieldState.invalid ? 'create-user-username-error' : undefined}
                    disabled={isCreating}
                  />
                  {fieldState.invalid && <FieldError id="create-user-username-error" errors={[fieldState.error]} />}
                </Field>
              )}
            />

            <Controller
              name="password"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="create-user-password">Password</FieldLabel>
                  <Input
                    {...field}
                    id="create-user-password"
                    type="password"
                    aria-invalid={fieldState.invalid}
                    aria-describedby={fieldState.invalid ? 'create-user-password-error' : undefined}
                    disabled={isCreating}
                  />
                  {fieldState.invalid && <FieldError id="create-user-password-error" errors={[fieldState.error]} />}
                </Field>
              )}
            />

            <Controller
              name="roles"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="create-user-roles">Roles</FieldLabel>
                  <Select
                    multiple
                    value={field.value}
                    onValueChange={(value) => field.onChange(value)}
                    disabled={isCreating}
                  >
                    <SelectTrigger id="create-user-roles" className="w-full" aria-invalid={fieldState.invalid}>
                      <SelectValue placeholder="Select roles...">
                        {(selected: string[]) => (selected.length > 0 ? selected.join(', ') : 'Select roles...')}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      <SelectGroup>
                        {availableRoles.map((role) => (
                          <SelectItem key={role.id} value={role.name}>
                            {role.name}
                          </SelectItem>
                        ))}
                      </SelectGroup>
                    </SelectContent>
                  </Select>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
          </FieldGroup>
        </form>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" form="create-user-form" disabled={isCreating}>
            {isCreating && <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />}
            Create User
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
