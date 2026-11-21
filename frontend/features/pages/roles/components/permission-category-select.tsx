'use client';

import * as React from 'react';

import { cn } from 'cn';

import { PERMISSION_GROUPS } from '@core/auth';

import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

interface PermissionCategorySelectProps {
  id?: string;
  value: string[];
  onChange: (permissions: string[]) => void;
  disabled?: boolean;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string;
}

export function PermissionCategorySelect({
  id,
  value,
  onChange,
  disabled,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}: PermissionCategorySelectProps) {
  const selectedSet = React.useMemo(() => new Set(value), [value]);

  const selectedCategories = React.useMemo(
    () =>
      PERMISSION_GROUPS.filter((group) => group.permissions.every((item) => selectedSet.has(item.value))).map(
        (group) => group.category
      ),
    [selectedSet]
  );

  const handleCategoriesChange = (nextCategories: string[]) => {
    const prevSet = new Set(selectedCategories);
    const nextCategorySet = new Set(nextCategories);
    const updated = new Set(value);

    for (const group of PERMISSION_GROUPS) {
      const wasSelected = prevSet.has(group.category);
      const isSelected = nextCategorySet.has(group.category);
      if (wasSelected === isSelected) continue;

      if (isSelected) {
        for (const item of group.permissions) updated.add(item.value);
      } else {
        for (const item of group.permissions) updated.delete(item.value);
      }
    }

    onChange(Array.from(updated));
  };

  const summaryLabel = React.useMemo(() => {
    const parts = PERMISSION_GROUPS.flatMap((group) => {
      const count = group.permissions.filter((item) => selectedSet.has(item.value)).length;
      if (count === 0) return [];
      return count === group.permissions.length
        ? [group.label]
        : [`${group.label} (${count}/${group.permissions.length})`];
    });
    return parts.length > 0 ? parts.join(', ') : 'Select permissions...';
  }, [selectedSet]);

  const hasPartialOrAnySelection = value.length > 0 && summaryLabel !== 'Select permissions...';

  return (
    <Select multiple value={selectedCategories} onValueChange={handleCategoriesChange} disabled={disabled}>
      <SelectTrigger
        id={id}
        className={cn('w-full', hasPartialOrAnySelection && 'data-placeholder:text-foreground')}
        aria-invalid={ariaInvalid}
        aria-describedby={ariaDescribedBy}
      >
        <SelectValue placeholder="Select permissions...">{() => summaryLabel}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        <SelectGroup>
          {PERMISSION_GROUPS.map((group) => {
            const count = group.permissions.filter((item) => selectedSet.has(item.value)).length;
            const total = group.permissions.length;
            const countLabel = count > 0 && count < total ? `(${count}/${total} selected)` : `(${total})`;

            return (
              <SelectItem key={group.category} value={group.category}>
                <span>{group.label}</span>
                <span className="text-muted-foreground text-xs">{countLabel}</span>
              </SelectItem>
            );
          })}
        </SelectGroup>
      </SelectContent>
    </Select>
  );
}
