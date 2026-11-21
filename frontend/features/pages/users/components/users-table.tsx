'use client';

import * as React from 'react';

import { deleteUserKeyGenerator, type DeleteUserRequestData } from '@api/deleteUserMutation';
import { getRolesKeyGenerator, type GetRolesResponse } from '@api/getRolesQuery';
import { getUsersKeyGenerator, type GetUsersRequestData, type GetUsersResponse } from '@api/getUsersQuery';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { parseAsInteger, useQueryStates } from 'nuqs';

import { PERMISSIONS } from '@core/auth';

import { useAuth } from '@hooks/use-auth';
import { useDeleteConfirmation } from '@hooks/use-delete-confirmation';

import { useToast } from '@stores/toast';

import { DataTable, DataTableSortMenu, DataTableToolbar } from '@/components/data-table';
import { buildPagedQueryParams, getColumnFilterParser, getDateFilterRange } from '@/components/data-table/lib';
import { useDataTable } from '@/hooks/use-data-table';

import { getUsersColumns } from './columns';
import { DeleteUserDialog } from './delete-user-dialog';
import { EditUserDialog } from './edit-user-dialog';

type ManagedUser = GetUsersResponse['items'][number];

export function UsersTable() {
  const [editingUserId, setEditingUserId] = React.useState<string | null>(null);
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { can } = useAuth();
  const canViewRoles = can(PERMISSIONS.roles.view);
  const canEditUsers = can(PERMISSIONS.users.update);
  const canDeleteUsers = can(PERMISSIONS.users.delete);

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
      ...getDateFilterRange(createdAt),
    }),
    [page, perPage, fullName, roles, createdAt]
  );

  const { data } = useQuery<GetUsersResponse>({
    queryKey: getUsersKeyGenerator(queryParams),
    placeholderData: keepPreviousData,
  });

  const { data: rolesData } = useQuery<GetRolesResponse>({
    queryKey: getRolesKeyGenerator({ Page: 1, PageSize: 100 }),
    enabled: canViewRoles,
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
        canEdit: canEditUsers,
        canDelete: canDeleteUsers,
      }),
    [rolesData, openModal, canEditUsers, canDeleteUsers]
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

      {canEditUsers && (
        <EditUserDialog
          userId={editingUserId}
          open={editingUserId !== null}
          onOpenChange={(open) => {
            if (!open) setEditingUserId(null);
          }}
        />
      )}

      {canDeleteUsers && (
        <DeleteUserDialog
          open={isModalOpen}
          userName={itemToDelete?.fullName || itemToDelete?.userName}
          isDeleting={deletingId !== null}
          onOpenChange={(open) => {
            if (!open) closeModal();
          }}
          onConfirm={() => void confirmDelete()}
        />
      )}
    </>
  );
}
