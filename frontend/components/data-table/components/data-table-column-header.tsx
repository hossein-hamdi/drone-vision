'use client';

import * as React from 'react';

import { type Column, type RowData, type SortDirection, type SortingState, Subscribe } from '@tanstack/react-table';
import { cn } from 'cn';
import { ChevronDown, ChevronsUpDown, ChevronUp, EyeOff } from 'lucide-react';

import { useDirection } from '@/components/ui/direction';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

import type { DataTableFeatures } from '../lib';

interface DataTableColumnHeaderProps<TData extends RowData, TValue> extends React.ComponentProps<
  typeof DropdownMenuTrigger
> {
  column: Column<DataTableFeatures, TData, TValue>;
  label?: string;
}

export function DataTableColumnHeader<TData extends RowData, TValue>({
  column,
  label = column.columnDef.meta?.label ?? column.id,
  className,
  ...props
}: DataTableColumnHeaderProps<TData, TValue>) {
  if (!column.getCanSort() && !column.getCanHide()) {
    return (
      <div title={label} className={cn('truncate', className)}>
        {label}
      </div>
    );
  }

  return (
    <Subscribe
      source={column.table.store}
      selector={(state) => ({
        isVisible: state.columnVisibility[column.id] !== false,
        sortDirection: getSortDirection(state.sorting, column.id),
      })}
    >
      {(headerState) => (
        <DataTableColumnHeaderMenu
          column={column}
          label={label}
          className={className}
          isVisible={headerState.isVisible}
          sortDirection={headerState.sortDirection}
          {...props}
        />
      )}
    </Subscribe>
  );
}

interface DataTableColumnHeaderMenuProps<TData extends RowData, TValue> extends DataTableColumnHeaderProps<
  TData,
  TValue
> {
  label: string;
  isVisible: boolean;
  sortDirection: SortDirection | 'none';
}

function DataTableColumnHeaderMenu<TData extends RowData, TValue>({
  column,
  label,
  className,
  isVisible,
  sortDirection,
  ...props
}: DataTableColumnHeaderMenuProps<TData, TValue>) {
  const dir = useDirection();

  let sortIcon: React.ReactNode = <ChevronsUpDown />;
  if (sortDirection === 'desc') {
    sortIcon = <ChevronDown />;
  } else if (sortDirection === 'asc') {
    sortIcon = <ChevronUp />;
  }

  function onSortDirectionChange(direction: SortDirection) {
    if (sortDirection === direction) {
      column.clearSorting();
      return;
    }

    column.table.setSorting((prev) => {
      const sort = { id: column.id, desc: direction === 'desc' };
      if (!column.getCanMultiSort()) return [sort];
      return [sort, ...prev.filter((item) => item.id !== column.id)].slice(
        0,
        column.table.options.maxMultiSortColCount
      );
    });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(
          'hover:bg-accent focus:ring-ring data-[state=open]:bg-accent [&_svg]:text-muted-foreground -ms-1.5 flex h-8 max-w-full min-w-0 items-center gap-1.5 rounded-md px-2 py-1.5 focus:ring-1 focus:outline-none rtl:flex-row-reverse [&_svg]:size-4 [&_svg]:shrink-0',
          className
        )}
        title={label}
        {...props}
      >
        <bdi className="truncate">{label}</bdi>
        {column.getCanSort() && sortIcon}
      </DropdownMenuTrigger>
      <DropdownMenuContent dir={dir} align="start" className="data-closed:fill-mode-forwards w-28">
        {column.getCanSort() && (
          <>
            <DropdownMenuCheckboxItem
              className="[&_svg]:text-muted-foreground"
              checked={sortDirection === 'asc'}
              onClick={() => onSortDirectionChange('asc')}
            >
              <ChevronUp />
              Asc
            </DropdownMenuCheckboxItem>
            <DropdownMenuCheckboxItem
              className="[&_svg]:text-muted-foreground"
              checked={sortDirection === 'desc'}
              onClick={() => onSortDirectionChange('desc')}
            >
              <ChevronDown />
              Desc
            </DropdownMenuCheckboxItem>
          </>
        )}
        {column.getCanHide() && (
          <DropdownMenuCheckboxItem
            className="[&_svg]:text-muted-foreground"
            checked={!isVisible}
            onClick={() => column.toggleVisibility(false)}
          >
            <EyeOff />
            Hide
          </DropdownMenuCheckboxItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function getSortDirection(sorting: SortingState, columnId: string): SortDirection | 'none' {
  const sort = sorting.find((sort) => sort.id === columnId);
  if (!sort) return 'none';
  return sort.desc ? 'desc' : 'asc';
}
