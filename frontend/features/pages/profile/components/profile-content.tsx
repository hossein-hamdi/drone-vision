'use client';

import * as React from 'react';

import { deleteProfileImageKeyGenerator, type DeleteProfileImageRequestData } from '@api/deleteProfileImageMutation';
import { getMeKeyGenerator } from '@api/getMeQuery';
import { getProfileKeyGenerator, type GetProfileResponse } from '@api/getProfileQuery';
import {
  uploadProfileImageKeyGenerator,
  type UploadProfileImageRequestData,
  type UploadProfileImageResponse,
} from '@api/uploadProfileImageMutation';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Loader2, RefreshCw, Shield, Trash2, Upload } from 'lucide-react';
import { Controller, useForm } from 'react-hook-form';

import { updateProfileSchema, type UpdateProfileSchema } from '@schemas';

import { useDeleteConfirmation } from '@hooks/use-delete-confirmation';
import { useUpdateProfile } from '@hooks/use-update-profile';

import { useToast } from '@stores/toast';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';

import { DeleteProfileImageDialog } from './delete-profile-image-dialog';

function getInitials(fullName: string, userName: string): string {
  const source = fullName.trim() || userName.trim();
  if (!source) return '?';
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

export function ProfileContent() {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const fileInputRef = React.useRef<HTMLInputElement | null>(null);

  const {
    data: profile,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery<GetProfileResponse>({
    queryKey: getProfileKeyGenerator(),
  });

  const form = useForm<UpdateProfileSchema>({
    resolver: zodResolver(updateProfileSchema),
    defaultValues: {
      fullName: '',
      userName: '',
    },
  });

  React.useEffect(() => {
    if (profile) {
      form.reset({
        fullName: profile.fullName,
        userName: profile.userName,
      });
    }
  }, [profile, form]);

  const { mutateAsync: updateProfileAsync, isPending: isUpdating } = useUpdateProfile();

  const { mutateAsync: uploadImageAsync, isPending: isUploadingImage } = useMutation<
    UploadProfileImageResponse,
    Error,
    UploadProfileImageRequestData
  >({
    mutationKey: uploadProfileImageKeyGenerator(),
  });

  const { mutateAsync: deleteImageAsync } = useMutation<void, Error, DeleteProfileImageRequestData>({
    mutationKey: deleteProfileImageKeyGenerator(),
  });

  const { isModalOpen, deletingId, openModal, closeModal, confirmDelete } = useDeleteConfirmation<{ id: string }>({
    onDelete: async (_id, signal) => {
      await deleteImageAsync({ signal });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getProfileKeyGenerator() }),
        queryClient.invalidateQueries({ queryKey: getMeKeyGenerator() }),
      ]);
      showToast('Profile photo removed', 'info');
    },
    onError: (err) => {
      showToast(err instanceof Error ? err.message : 'Failed to remove profile photo', 'error');
    },
  });

  const handleSubmit = async (values: UpdateProfileSchema) => {
    try {
      await updateProfileAsync(values);
      showToast('Profile updated successfully', 'success');
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Failed to update profile', 'error');
    }
  };

  const handleImageChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    try {
      await uploadImageAsync({ image: file });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getProfileKeyGenerator() }),
        queryClient.invalidateQueries({ queryKey: getMeKeyGenerator() }),
      ]);
      showToast('Profile photo updated', 'success');
    } catch (err) {
      showToast(err instanceof Error ? err.message : 'Failed to upload profile photo', 'error');
    }
  };

  if (isLoading && !profile) {
    return (
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="border-border bg-card flex flex-col items-center gap-4 rounded-xl border p-6">
          <Skeleton className="size-24 rounded-full" />
          <Skeleton className="h-5 w-40" />
          <Skeleton className="h-4 w-28" />
          <div className="flex gap-2">
            <Skeleton className="h-8 w-28" />
            <Skeleton className="h-8 w-20" />
          </div>
        </div>
        <div className="border-border bg-card flex flex-col gap-4 rounded-xl border p-6 lg:col-span-2">
          <Skeleton className="h-5 w-44" />
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-32 self-end" />
        </div>
      </div>
    );
  }

  if (isError && !profile) {
    return (
      <div className="border-border bg-card flex flex-col items-center justify-center gap-3 rounded-xl border p-8 text-center">
        <AlertCircle className="text-destructive size-8" aria-hidden="true" />
        <div className="flex flex-col gap-1">
          <p className="text-foreground text-sm font-medium">Failed to load profile</p>
          <p className="text-muted-foreground text-xs">{error?.message || 'An unexpected error occurred.'}</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void refetch()}>
          <RefreshCw data-icon="inline-start" aria-hidden="true" />
          Retry
        </Button>
      </div>
    );
  }

  const isBusy = isUpdating || isUploadingImage || deletingId !== null;

  return (
    <>
      <div className="grid gap-6 lg:grid-cols-3">
        <section className="border-border bg-card flex flex-col items-center gap-5 rounded-xl border p-6 text-center">
          <Avatar className="size-24 text-lg">
            {profile?.profileImageUrl && (
              <AvatarImage src={profile.profileImageUrl} alt={profile.fullName || profile.userName} />
            )}
            <AvatarFallback>{getInitials(profile?.fullName ?? '', profile?.userName ?? '')}</AvatarFallback>
          </Avatar>

          <div className="flex flex-col gap-1">
            <h2 className="text-foreground text-lg font-semibold tracking-tight">
              {profile?.fullName || profile?.userName}
            </h2>
            <p className="text-muted-foreground font-mono text-xs">@{profile?.userName}</p>
          </div>

          <div className="flex flex-wrap items-center justify-center gap-2">
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

            {profile?.profileImageUrl && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={isBusy}
                onClick={() => openModal({ id: 'profile-image' })}
              >
                <Trash2 data-icon="inline-start" aria-hidden="true" />
                Remove
              </Button>
            )}
          </div>

          <p className="text-muted-foreground text-xs">JPEG, PNG, or WebP up to 5 MiB.</p>

          {profile && profile.roles.length > 0 && (
            <>
              <Separator />
              <div className="flex w-full flex-col gap-2 text-left">
                <span className="text-muted-foreground flex items-center gap-1.5 text-xs font-medium">
                  <Shield className="size-3.5" aria-hidden="true" />
                  Assigned Roles
                </span>
                <div className="flex flex-wrap gap-1.5">
                  {profile.roles.map((role) => (
                    <Badge key={role} variant="secondary">
                      {role}
                    </Badge>
                  ))}
                </div>
              </div>
            </>
          )}
        </section>

        <section className="border-border bg-card flex flex-col gap-6 rounded-xl border p-6 lg:col-span-2">
          <div className="flex flex-col gap-1">
            <h2 className="text-foreground text-base font-semibold tracking-tight">Personal Information</h2>
            <p className="text-muted-foreground text-sm">Update your display name and username.</p>
          </div>

          <form
            onSubmit={(event) => void form.handleSubmit(handleSubmit)(event)}
            className="flex flex-1 flex-col justify-between gap-6"
          >
            <FieldGroup>
              <Controller
                name="fullName"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="profile-fullname">Full name</FieldLabel>
                    <Input
                      {...field}
                      id="profile-fullname"
                      aria-invalid={fieldState.invalid}
                      aria-describedby={fieldState.invalid ? 'profile-fullname-error' : undefined}
                      disabled={isBusy}
                    />
                    {fieldState.invalid && <FieldError id="profile-fullname-error" errors={[fieldState.error]} />}
                  </Field>
                )}
              />

              <Controller
                name="userName"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="profile-username">Username</FieldLabel>
                    <Input
                      {...field}
                      id="profile-username"
                      aria-invalid={fieldState.invalid}
                      aria-describedby={fieldState.invalid ? 'profile-username-error' : undefined}
                      disabled={isBusy}
                    />
                    {fieldState.invalid && <FieldError id="profile-username-error" errors={[fieldState.error]} />}
                  </Field>
                )}
              />
            </FieldGroup>

            <div className="flex justify-end">
              <Button type="submit" disabled={isBusy || !form.formState.isDirty}>
                {isUpdating && <Loader2 className="animate-spin" data-icon="inline-start" aria-hidden="true" />}
                Save changes
              </Button>
            </div>
          </form>
        </section>
      </div>

      <DeleteProfileImageDialog
        open={isModalOpen}
        isDeleting={deletingId !== null}
        onOpenChange={(open) => {
          if (!open) closeModal();
        }}
        onConfirm={() => void confirmDelete()}
      />
    </>
  );
}
