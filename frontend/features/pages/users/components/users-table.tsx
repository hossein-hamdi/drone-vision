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
import { buildPagedQueryParams, getColumnFilterParser } from '@/components/data-table/lib';
import { useDataTable } from '@/hooks/use-data-table';

import { getUsersColumns } from './columns';
import { DeleteUserDialog } from './delete-user-dialog';
import { EditUserDialog } from './edit-user-dialog';

type ManagedUser = GetUsersResponse['items'][number];

export function UsersTable() {
  const [editingUserId, setEditingUserId] = React.useState<string | null>(null);
  const queryClient = useQueryClient();
  const { showToast } = useToast();

  const [{ page, perPage, fullName, roles }] = useQueryStates({
    page: parseAsInteger.withDefault(1),
    perPage: parseAsInteger.withDefault(10),
    fullName: getColumnFilterParser('fullName', 'text'),
    roles: getColumnFilterParser('roles', 'select'),
  });

  const queryParams = React.useMemo<GetUsersRequestData>(
    () =>
      buildPagedQueryParams({
        page,
        perPage,
        filters: {
          Name: fullName,
          Role: roles,
        },
      }),
    [page, perPage, fullName, roles]
  );

  const { data } = useQuery<GetUsersResponse>({
    queryKey: getUsersKeyGenerator(queryParams),
    placeholderData: keepPreviousData,
  });

  const { data: rolesData } = useQuery<GetRolesResponse>({
    queryKey: getRolesKeyGenerator(),
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
        roles: rolesData ?? [],
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
