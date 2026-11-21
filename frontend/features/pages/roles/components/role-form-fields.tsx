'use client';

import { type Control, Controller } from 'react-hook-form';

import type { RoleSchema } from '@schemas';

import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';

import { PermissionCategorySelect } from './permission-category-select';

interface RoleFormFieldsProps {
  control: Control<RoleSchema>;
  disabled?: boolean;
  idPrefix: string;
}

export function RoleFormFields({ control, disabled, idPrefix }: RoleFormFieldsProps) {
  const nameId = `${idPrefix}-name`;
  const nameErrorId = `${idPrefix}-name-error`;
  const permissionsId = `${idPrefix}-permissions`;
  const permissionsErrorId = `${idPrefix}-permissions-error`;

  return (
    <FieldGroup>
      <Controller
        name="name"
        control={control}
        render={({ field, fieldState }) => (
          <Field data-invalid={fieldState.invalid}>
            <FieldLabel htmlFor={nameId}>Role name</FieldLabel>
            <Input
              {...field}
              id={nameId}
              aria-invalid={fieldState.invalid}
              aria-describedby={fieldState.invalid ? nameErrorId : undefined}
              disabled={disabled}
            />
            {fieldState.invalid && <FieldError id={nameErrorId} errors={[fieldState.error]} />}
          </Field>
        )}
      />

      <Controller
        name="permissions"
        control={control}
        render={({ field, fieldState }) => (
          <Field data-invalid={fieldState.invalid}>
            <FieldLabel htmlFor={permissionsId}>Permissions</FieldLabel>
            <PermissionCategorySelect
              id={permissionsId}
              value={field.value}
              onChange={field.onChange}
              disabled={disabled}
              aria-invalid={fieldState.invalid}
              aria-describedby={fieldState.invalid ? permissionsErrorId : undefined}
            />
            {fieldState.invalid && <FieldError id={permissionsErrorId} errors={[fieldState.error]} />}
          </Field>
        )}
      />
    </FieldGroup>
  );
}
