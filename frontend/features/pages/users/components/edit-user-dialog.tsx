'use client';

import * as React from 'react';

import {
  deleteUserProfileImageKeyGenerator,
  type DeleteUserProfileImageRequestData,
} from '@api/deleteUserProfileImageMutation';
import { getRolesKeyGenerator, type GetRolesResponse } from '@api/getRolesQuery';
import { getUserKeyGenerator, type GetUserResponse } from '@api/getUserQuery';
import { updateUserKeyGenerator, type UpdateUserRequestData, type UpdateUserResponse } from '@api/updateUserMutation';
import {
  uploadUserProfileImageKeyGenerator,
  type UploadUserProfileImageRequestData,
  type UploadUserProfileImageResponse,
} from '@api/uploadUserProfileImageMutation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Trash2, Upload } from 'lucide-react';
import { Controller, useForm } from 'react-hook-form';

import { editUserSchema, type EditUserSchema } from '@schemas';

import { PERMISSIONS } from '@core/auth';

import { useAuth } from '@hooks/use-auth';
import { isAbortError, useModalAbort } from '@hooks/use-modal-abort';

import { useToast } from '@stores/toast';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';

interface EditUserDialogProps {
  userId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function getInitials(fullName: string, userName: string): string {
  const source = fullName.trim() || userName.trim();
  if (!source) return '?';
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

export function EditUserDialog({ userId, open, onOpenChange }: EditUserDialogProps) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { can } = useAuth();
  const fileInputRef = React.useRef<HTMLInputElement | null>(null);

  const userQueryKey = React.useMemo(() => (userId ? getUserKeyGenerator({ id: userId }) : undefined), [userId]);

  const { createSignal, abort } = useModalAbort({
    open,
    queryKey: userQueryKey,
  });

  const { data: user, isLoading: isLoadingUser } = useQuery<GetUserResponse>({
    queryKey: getUserKeyGenerator({ id: userId ?? '' }),
    enabled: open && Boolean(userId),
  });

  const { data: rolesData } = useQuery<GetRolesResponse>({
    queryKey: getRolesKeyGenerator({ Page: 1, PageSize: 100 }),
    enabled: open && can(PERMISSIONS.roles.view),
  });

  const form = useForm<EditUserSchema>({
    resolver: zodResolver(editUserSchema),
    defaultValues: {
      fullName: '',
      userName: '',
      roles: [],
    },
  });

  React.useEffect(() => {
    if (user) {
      form.reset({
        fullName: user.fullName,
        userName: user.userName,
        roles: user.roles,
      });
    }
  }, [user, form]);

  const { mutateAsync: updateUserAsync, isPending: isUpdating } = useMutation<
    UpdateUserResponse,
    Error,
    UpdateUserRequestData
  >({
    mutationKey: updateUserKeyGenerator(),
  });

  const { mutateAsync: uploadImageAsync, isPending: isUploadingImage } = useMutation<
    UploadUserProfileImageResponse,
    Error,
    UploadUserProfileImageRequestData
  >({
    mutationKey: uploadUserProfileImageKeyGenerator(),
  });

  const { mutateAsync: deleteImageAsync, isPending: isDeletingImage } = useMutation<
    void,
    Error,
    DeleteUserProfileImageRequestData
  >({
    mutationKey: deleteUserProfileImageKeyGenerator(),
  });

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      abort();
    }
    onOpenChange(nextOpen);
  };

  const handleSubmit = async (values: EditUserSchema) => {
    if (!userId) return;
    const signal = createSignal();
    try {
      await updateUserAsync({
        id: userId,
        fullName: values.fullName,
        userName: values.userName,
        roles: values.roles,
        signal,
      });
      await queryClient.invalidateQueries({ queryKey: ['users'] });
      showToast('User updated successfully', 'success');
      onOpenChange(false);
    } catch (error) {
      if (isAbortError(error)) return;
      showToast(error instanceof Error ? error.message : 'Failed to update user', 'error');
    }
  };

  const handleImageChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file || !userId) return;

    const signal = createSignal();
    try {
      await uploadImageAsync({ id: userId, image: file, signal });
      await queryClient.invalidateQueries({ queryKey: ['users'] });
      showToast('Profile image updated', 'success');
    } catch (error) {
      if (isAbortError(error)) return;
      showToast(error instanceof Error ? error.message : 'Failed to upload profile image', 'error');
    }
  };

  const handleDeleteImage = async () => {
    if (!userId) return;
    const signal = createSignal();
    try {
      await deleteImageAsync({ id: userId, signal });
      await queryClient.invalidateQueries({ queryKey: ['users'] });
      showToast('Profile image removed', 'info');
    } catch (error) {
      if (isAbortError(error)) return;
      showToast(error instanceof Error ? error.message : 'Failed to remove profile image', 'error');
    }
  };

  const isBusy = isUpdating || isUploadingImage || isDeletingImage;
  const availableRoles = rolesData?.items ?? [];

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Edit User</DialogTitle>
          <DialogDescription>Update user details, assigned roles, and profile image.</DialogDescription>
        </DialogHeader>

        {isLoadingUser && !user ? (
          <div className="flex flex-col gap-4 py-2">
            <div className="flex items-center gap-4">
              <Skeleton className="size-12 rounded-full" />
              <div className="flex gap-2">
                <Skeleton className="h-8 w-24" />
                <Skeleton className="h-8 w-20" />
              </div>
            </div>
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-20 w-full" />
          </div>
        ) : (
          <form
            id="edit-user-form"
            onSubmit={(event) => void form.handleSubmit(handleSubmit)(event)}
            className="flex flex-col gap-4"
          >
            <div className="flex items-center gap-4">
              <Avatar size="lg">
                {user?.profileImageUrl && (
                  <AvatarImage src={user.profileImageUrl} alt={user.fullName || user.userName} />
                )}
                <AvatarFallback>{getInitials(user?.fullName ?? '', user?.userName ?? '')}</AvatarFallback>
              </Avatar>
              <div className="flex flex-wrap items-center gap-2">
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  className="hidden"
                  onChange={handleImageChange}
                />
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={isBusy}
                  onClick={() => fileInputRef.current?.click()}
                >
                  {isUploadingImage ? (
                    <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />
                  ) : (
                    <Upload data-icon="inline-start" aria-hidden="true" />
                  )}
                  Upload photo
                </Button>
                {user?.profileImageUrl && (
                  <Button type="button" variant="outline" size="sm" disabled={isBusy} onClick={handleDeleteImage}>
                    {isDeletingImage ? (
                      <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />
                    ) : (
                      <Trash2 data-icon="inline-start" aria-hidden="true" />
                    )}
                    Remove
                  </Button>
                )}
              </div>
            </div>

            <FieldGroup>
              <Controller
                name="fullName"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-user-fullname">Full name</FieldLabel>
                    <Input
                      {...field}
                      id="edit-user-fullname"
                      aria-invalid={fieldState.invalid}
                      aria-describedby={fieldState.invalid ? 'edit-user-fullname-error' : undefined}
                      disabled={isBusy}
                    />
                    {fieldState.invalid && <FieldError id="edit-user-fullname-error" errors={[fieldState.error]} />}
                  </Field>
                )}
              />

              <Controller
                name="userName"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-user-username">Username</FieldLabel>
                    <Input
                      {...field}
                      id="edit-user-username"
                      aria-invalid={fieldState.invalid}
                      aria-describedby={fieldState.invalid ? 'edit-user-username-error' : undefined}
                      disabled={isBusy}
                    />
                    {fieldState.invalid && <FieldError id="edit-user-username-error" errors={[fieldState.error]} />}
                  </Field>
                )}
              />

              <Controller
                name="roles"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="edit-user-roles">Roles</FieldLabel>
                    <Select
                      multiple
                      value={field.value}
                      onValueChange={(value) => field.onChange(value)}
                      disabled={isBusy}
                    >
                      <SelectTrigger id="edit-user-roles" className="w-full" aria-invalid={fieldState.invalid}>
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
        )}

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" form="edit-user-form" disabled={isLoadingUser || isBusy}>
            {isUpdating && <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />}
            Save changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
