'use client';

import * as React from 'react';

import { deleteUserKeyGenerator, type DeleteUserRequestData } from '@api/deleteUserMutation';
import { getRolesKeyGenerator, type GetRolesResponse } from '@api/getRolesQuery';
import { getUsersKeyGenerator, type GetUsersRequestData, type GetUsersResponse } from '@api/getUsersQuery';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { parseAsInteger, useQueryStates } from 'nuqs';

import { useDeleteConfirmation } from '@hooks/use-delete-confirmation';

import { useToast } from '@stores/toast';

import { DataTable, DataTableSortMenu, DataTableToolbar } from '@/components/data-table';
import {
  buildPagedQueryParams,
  type ColumnFilterItem,
  getColumnFilterParser,
  parseFilterDate,
} from '@/components/data-table/lib';
import { useDataTable } from '@/hooks/use-data-table';

import { getUsersColumns } from './columns';
import { DeleteUserDialog } from './delete-user-dialog';
import { EditUserDialog } from './edit-user-dialog';

type ManagedUser = GetUsersResponse['items'][number];

function getCreatedAtRange(filters: ColumnFilterItem[] | null | undefined): {
  CreatedAtFrom?: string;
  CreatedAtTo?: string;
} {
  const item = filters?.[0];
  if (!item) return {};

  const values = Array.isArray(item.value) ? item.value : [item.value];
  const first = values[0]?.trim();
  const second = values[1]?.trim();

  let rawFrom: string | undefined;
  let rawTo: string | undefined;

  switch (item.operator) {
    case 'isBetween':
      rawFrom = first;
      rawTo = second || first;
      break;
    case 'eq':
      rawFrom = first;
      rawTo = first;
      break;
    case 'gt':
    case 'gte':
      rawFrom = first;
      break;
    case 'lt':
    case 'lte':
      rawTo = first;
      break;
    default:
      return {};
  }

  const fromDate = rawFrom ? parseFilterDate(rawFrom) : undefined;
  const toDate = rawTo ? parseFilterDate(rawTo) : undefined;
  if (fromDate) fromDate.setHours(0, 0, 0, 0);
  if (toDate) toDate.setHours(23, 59, 59, 999);

  return {
    CreatedAtFrom: fromDate?.toISOString(),
    CreatedAtTo: toDate?.toISOString(),
  };
}

export function UsersTable() {
  const [editingUserId, setEditingUserId] = React.useState<string | null>(null);
  const queryClient = useQueryClient();
  const { showToast } = useToast();

  const [{ page, perPage, fullName, roles, createdAt }] = useQueryStates({
    page: parseAsInteger.withDefault(1),
    perPage: parseAsInteger.withDefault(10),
    fullName: getColumnFilterParser('fullName', 'text'),
    roles: getColumnFilterParser('roles', 'select'),
    createdAt: getColumnFilterParser('createdAt', 'date'),
  });

  const queryParams = React.useMemo<GetUsersRequestData>(
    () => ({
      ...buildPagedQueryParams({
        page,
        perPage,
        filters: {
          Name: fullName,
          Role: roles,
        },
      }),
      ...getCreatedAtRange(createdAt),
    }),
    [page, perPage, fullName, roles, createdAt]
  );

  const { data } = useQuery<GetUsersResponse>({
    queryKey: getUsersKeyGenerator(queryParams),
    placeholderData: keepPreviousData,
  });

  const { data: rolesData } = useQuery<GetRolesResponse>({
    queryKey: getRolesKeyGenerator({ Page: 1, PageSize: 100 }),
  });

  const { mutateAsync: deleteUserAsync } = useMutation<void, Error, DeleteUserRequestData>({
    mutationKey: deleteUserKeyGenerator(),
  });

  const { itemToDelete, isModalOpen, deletingId, openModal, closeModal, confirmDelete } =
    useDeleteConfirmation<ManagedUser>({
      onDelete: async (id, signal) => {
        await deleteUserAsync({ id, signal });
        await queryClient.invalidateQueries({ queryKey: ['users'] });
        showToast('User deleted successfully', 'info');
      },
      onError: (error) => {
        showToast(error instanceof Error ? error.message : 'Failed to delete user', 'error');
      },
    });

  const columns = React.useMemo(
    () =>
      getUsersColumns({
        roles: rolesData?.items ?? [],
        onEdit: (user) => setEditingUserId(user.id),
        onDelete: (user) => openModal(user),
      }),
    [rolesData, openModal]
  );

  const { table } = useDataTable({
    data: data?.items ?? [],
    columns,
    pageCount: data?.totalPages ?? 1,
    enableRowSelection: false,
    config: {
      defaultPageSize: 10,
    },
    getRowId: (row) => row.id,
  });

  return (
    <>
      <DataTable table={table}>
        <DataTableToolbar table={table}>
          <DataTableSortMenu table={table} />
        </DataTableToolbar>
      </DataTable>

      <EditUserDialog
        userId={editingUserId}
        open={editingUserId !== null}
        onOpenChange={(open) => {
          if (!open) setEditingUserId(null);
        }}
      />

      <DeleteUserDialog
        open={isModalOpen}
        userName={itemToDelete?.fullName || itemToDelete?.userName}
        isDeleting={deletingId !== null}
        onOpenChange={(open) => {
          if (!open) closeModal();
        }}
        onConfirm={() => void confirmDelete()}
      />
    </>
  );
}
