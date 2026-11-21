'use client';

import type { GetRolesResponse } from '@api/getRolesQuery';
import type { ColumnDef } from '@tanstack/react-table';
import { Eye, Pencil, Trash2 } from 'lucide-react';

import { DataTableColumnHeader, getDataTableActionsColumn } from '@/components/data-table';
import { type DataTableFeatures, formatDate } from '@/components/data-table/lib';
import { Button } from '@/components/ui/button';
import { DropdownMenuGroup, DropdownMenuItem } from '@/components/ui/dropdown-menu';

type ManagedRole = GetRolesResponse['items'][number];

export function getRolesColumns({
  onViewPermissions,
  onEdit,
  onDelete,
  canEdit = true,
  canDelete = true,
}: {
  onViewPermissions: (role: ManagedRole) => void;
  onEdit: (role: ManagedRole) => void;
  onDelete: (role: ManagedRole) => void;
  canEdit?: boolean;
  canDelete?: boolean;
}): ColumnDef<DataTableFeatures, ManagedRole>[] {
  const columns: ColumnDef<DataTableFeatures, ManagedRole>[] = [
    {
      id: 'id',
      accessorKey: 'id',
      header: ({ column }) => <DataTableColumnHeader column={column} label="#" />,
      cell: ({ row }) => (
        <span className="text-muted-foreground bg-muted/60 inline-flex items-center rounded-md px-2 py-0.5 font-mono text-xs font-medium tabular-nums">
          #{row.original.id}
        </span>
      ),
      enableSorting: false,
      size: 80,
    },
    {
      id: 'name',
      accessorKey: 'name',
      header: ({ column }) => <DataTableColumnHeader column={column} label="Role name" />,
      cell: ({ row }) => <span className="font-medium">{row.original.name}</span>,
      meta: {
        label: 'Role name',
        placeholder: 'Search by role name...',
        variant: 'text',
      },
      enableColumnFilter: true,
      enableSorting: false,
    },
    {
      id: 'permissions',
      accessorKey: 'permissions',
      header: ({ column }) => <DataTableColumnHeader column={column} label="Permissions" />,
      cell: ({ row }) => (
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          className="text-muted-foreground"
          aria-label={`View permissions for ${row.original.name}`}
          title="View permissions"
          onClick={() => onViewPermissions(row.original)}
        >
          <Eye aria-hidden="true" />
        </Button>
      ),
      enableSorting: false,
      size: 110,
    },
    {
      id: 'createdAt',
      accessorKey: 'createdAt',
      header: ({ column }) => <DataTableColumnHeader column={column} label="Created at" />,
      cell: ({ row }) => (
        <span className="text-muted-foreground text-xs whitespace-nowrap tabular-nums">
          {formatDate(row.original.createdAt)}
        </span>
      ),
      meta: {
        label: 'Created at',
        variant: 'date',
      },
      enableColumnFilter: true,
      enableSorting: false,
    },
  ];

  if (canEdit || canDelete) {
    columns.push(
      getDataTableActionsColumn<ManagedRole>({
        actions: ({ row }) => (
          <DropdownMenuGroup>
            {canEdit && (
              <DropdownMenuItem onClick={() => onEdit(row.original)}>
                <Pencil aria-hidden="true" />
                Edit
              </DropdownMenuItem>
            )}
            {canDelete && (
              <DropdownMenuItem variant="destructive" onClick={() => onDelete(row.original)}>
                <Trash2 aria-hidden="true" />
                Delete
              </DropdownMenuItem>
            )}
          </DropdownMenuGroup>
        ),
      })
    );
  }

  return columns;
}
