'use client';

import * as React from 'react';

import { deleteRoleKeyGenerator, type DeleteRoleRequestData } from '@api/deleteRoleMutation';
import { getRolesKeyGenerator, type GetRolesRequestData, type GetRolesResponse } from '@api/getRolesQuery';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { parseAsInteger, useQueryStates } from 'nuqs';

import { PERMISSIONS } from '@core/auth';

import { useAuth } from '@hooks/use-auth';
import { useDeleteConfirmation } from '@hooks/use-delete-confirmation';

import { useToast } from '@stores/toast';

import { DataTable, DataTableToolbar } from '@/components/data-table';
import { buildPagedQueryParams, getColumnFilterParser, getDateFilterRange } from '@/components/data-table/lib';
import { useDataTable } from '@/hooks/use-data-table';

import { getRolesColumns } from './columns';
import { DeleteRoleDialog } from './delete-role-dialog';
import { EditRoleDialog } from './edit-role-dialog';
import { RolePermissionsDialog } from './role-permissions-dialog';

type ManagedRole = GetRolesResponse['items'][number];

export function RolesTable() {
  const [editingRoleId, setEditingRoleId] = React.useState<string | null>(null);
  const [viewingPermissionsRole, setViewingPermissionsRole] = React.useState<ManagedRole | null>(null);
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { can } = useAuth();
  const canEditRoles = can(PERMISSIONS.roles.update);
  const canDeleteRoles = can(PERMISSIONS.roles.delete);

  const [{ page, perPage, name, createdAt }] = useQueryStates({
    page: parseAsInteger.withDefault(1),
    perPage: parseAsInteger.withDefault(10),
    name: getColumnFilterParser('name', 'text'),
    createdAt: getColumnFilterParser('createdAt', 'date'),
  });

  const queryParams = React.useMemo<GetRolesRequestData>(
    () => ({
      ...buildPagedQueryParams({
        page,
        perPage,
        filters: {
          RoleName: name,
        },
      }),
      ...getDateFilterRange(createdAt),
    }),
    [page, perPage, name, createdAt]
  );

  const { data } = useQuery<GetRolesResponse>({
    queryKey: getRolesKeyGenerator(queryParams),
    placeholderData: keepPreviousData,
  });

  const { mutateAsync: deleteRoleAsync } = useMutation<void, Error, DeleteRoleRequestData>({
    mutationKey: deleteRoleKeyGenerator(),
  });

  const { itemToDelete, isModalOpen, deletingId, openModal, closeModal, confirmDelete } =
    useDeleteConfirmation<ManagedRole>({
      onDelete: async (id, signal) => {
        await deleteRoleAsync({ id, signal });
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ['roles'] }),
          queryClient.invalidateQueries({ queryKey: ['users'] }),
          queryClient.invalidateQueries({ queryKey: ['auth', 'me'] }),
          queryClient.invalidateQueries({ queryKey: ['profile'] }),
        ]);
        showToast('Role deleted successfully', 'info');
      },
      onError: (error) => {
        showToast(error instanceof Error ? error.message : 'Failed to delete role', 'error');
      },
    });

  const columns = React.useMemo(
    () =>
      getRolesColumns({
        onViewPermissions: (role) => setViewingPermissionsRole(role),
        onEdit: (role) => setEditingRoleId(role.id),
        onDelete: (role) => openModal(role),
        canEdit: canEditRoles,
        canDelete: canDeleteRoles,
      }),
    [openModal, canEditRoles, canDeleteRoles]
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
        <DataTableToolbar table={table} />
      </DataTable>

      <RolePermissionsDialog
        role={viewingPermissionsRole}
        open={viewingPermissionsRole !== null}
        onOpenChange={(open) => {
          if (!open) setViewingPermissionsRole(null);
        }}
      />

      {canEditRoles && (
        <EditRoleDialog
          roleId={editingRoleId}
          open={editingRoleId !== null}
          onOpenChange={(open) => {
            if (!open) setEditingRoleId(null);
          }}
        />
      )}

      {canDeleteRoles && (
        <DeleteRoleDialog
          open={isModalOpen}
          roleName={itemToDelete?.name}
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
