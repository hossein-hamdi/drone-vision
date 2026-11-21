'use client';

import type { GetRolesResponse } from '@api/getRolesQuery';
import type { GetUsersResponse } from '@api/getUsersQuery';
import type { ColumnDef } from '@tanstack/react-table';
import { Pencil, Trash2 } from 'lucide-react';

import { DataTableColumnHeader, getDataTableActionsColumn } from '@/components/data-table';
import { type DataTableFeatures, formatDate } from '@/components/data-table/lib';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { DropdownMenuGroup, DropdownMenuItem } from '@/components/ui/dropdown-menu';

type ManagedUser = GetUsersResponse['items'][number];
type RoleItem = GetRolesResponse['items'][number];

function getInitials(fullName: string, userName: string): string {
  const source = fullName.trim() || userName.trim();
  if (!source) return '?';

  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

export function getUsersColumns({
  roles,
  onEdit,
  onDelete,
}: {
  roles: RoleItem[];
  onEdit: (user: ManagedUser) => void;
  onDelete: (user: ManagedUser) => void;
}): ColumnDef<DataTableFeatures, ManagedUser>[] {
  return [
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
      id: 'fullName',
      accessorKey: 'fullName',
      header: ({ column }) => <DataTableColumnHeader column={column} label="Full name" />,
      cell: ({ row }) => {
        const user = row.original;
        return (
          <div className="flex items-center gap-2.5">
            <Avatar size="sm">
              {user.profileImageUrl && <AvatarImage src={user.profileImageUrl} alt={user.fullName || user.userName} />}
              <AvatarFallback>{getInitials(user.fullName, user.userName)}</AvatarFallback>
            </Avatar>
            <span className="truncate font-medium">{user.fullName || user.userName}</span>
          </div>
        );
      },
      meta: {
        label: 'Name',
        placeholder: 'Search by name...',
        variant: 'text',
      },
      enableColumnFilter: true,
    },
    {
      id: 'userName',
      accessorKey: 'userName',
      header: ({ column }) => <DataTableColumnHeader column={column} label="Username" />,
      cell: ({ row }) => <span className="text-muted-foreground font-mono text-xs">@{row.original.userName}</span>,
    },
    {
      id: 'roles',
      accessorKey: 'roles',
      header: ({ column }) => <DataTableColumnHeader column={column} label="Roles" />,
      cell: ({ row }) => (
        <div className="flex flex-wrap gap-1">
          {row.original.roles.map((role) => (
            <Badge key={role} variant="secondary">
              {role}
            </Badge>
          ))}
        </div>
      ),
      meta: {
        label: 'Roles',
        variant: 'select',
        multiple: false,
        options: roles.map((role) => ({
          label: role.name,
          value: role.name,
        })),
      },
      enableColumnFilter: true,
      enableSorting: false,
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
    getDataTableActionsColumn<ManagedUser>({
      actions: ({ row }) => (
        <DropdownMenuGroup>
          <DropdownMenuItem onClick={() => onEdit(row.original)}>
            <Pencil aria-hidden="true" />
            Edit
          </DropdownMenuItem>
          <DropdownMenuItem variant="destructive" onClick={() => onDelete(row.original)}>
            <Trash2 aria-hidden="true" />
            Delete
          </DropdownMenuItem>
        </DropdownMenuGroup>
      ),
    }),
  ];
}
