import * as React from 'react';

import {
  DataTable,
  DataTableColumnHeader,
  DataTableToolbar,
  getDataTableActionsColumn,
  getDataTableSelectColumn,
} from '..';
import { type ColumnDef, useTable } from '@tanstack/react-table';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { page, userEvent } from 'vitest/browser';

import { cleanup, render } from '@/__tests__/test-utils';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';

import { type DataTableFeatures, dataTableFeatures } from '../lib';

interface DroneRow {
  id: string;
  name: string;
  status: string;
}

const SAMPLE_DATA: DroneRow[] = [
  { id: '1', name: 'Alpha', status: 'active' },
  { id: '2', name: 'Charlie', status: 'idle' },
  { id: '3', name: 'Bravo', status: 'active' },
  { id: '4', name: 'Delta', status: 'maintenance' },
  { id: '5', name: 'Echo', status: 'idle' },
];

interface HarnessProps {
  data?: DroneRow[];
  pageSize?: number;
  withSelection?: boolean;
  withActions?: boolean;
  onActionClick?: (row: DroneRow) => void;
  actionBar?: React.ReactNode;
  onEscapeKeyDown?: (event: KeyboardEvent) => void;
  globalFilterPlaceholder?: string;
}

function DataTableHarness({
  data = SAMPLE_DATA,
  pageSize = 10,
  withSelection = false,
  withActions = false,
  onActionClick,
  actionBar,
  onEscapeKeyDown,
  globalFilterPlaceholder = 'Search drones...',
}: HarnessProps) {
  const columns = React.useMemo<ColumnDef<DataTableFeatures, DroneRow>[]>(() => {
    const cols: ColumnDef<DataTableFeatures, DroneRow>[] = [];

    if (withSelection) {
      cols.push(getDataTableSelectColumn<DroneRow>());
    }

    cols.push(
      {
        id: 'name',
        accessorKey: 'name',
        enableColumnFilter: true,
        enableSorting: true,
        enableHiding: true,
        meta: {
          label: 'Name',
          placeholder: 'Filter name...',
          variant: 'text',
        },
        header: ({ column }) => <DataTableColumnHeader column={column} label="Name" />,
      },
      {
        id: 'status',
        accessorKey: 'status',
        enableSorting: false,
        enableHiding: true,
        meta: {
          label: 'Status',
        },
        header: ({ column }) => <DataTableColumnHeader column={column} label="Status" />,
      }
    );

    if (withActions) {
      cols.push(
        getDataTableActionsColumn<DroneRow>({
          actions: ({ row }) => (
            <DropdownMenuItem onClick={() => onActionClick?.(row.original)}>
              Inspect {row.original.name}
            </DropdownMenuItem>
          ),
        })
      );
    }

    return cols;
  }, [withSelection, withActions, onActionClick]);

  const table = useTable({
    features: dataTableFeatures,
    data,
    columns,
    getRowId: (row) => row.id,
    enableRowSelection: withSelection,
    initialState: {
      pagination: { pageIndex: 0, pageSize },
    },
  });

  return (
    <DataTable table={table} actionBar={actionBar} onEscapeKeyDown={onEscapeKeyDown} aria-label="Drones table">
      <DataTableToolbar table={table} globalFilterPlaceholder={globalFilterPlaceholder} />
    </DataTable>
  );
}

describe('DataTable', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders rows and shows empty state when data is empty', async () => {
    const { rerender } = render(<DataTableHarness data={SAMPLE_DATA.slice(0, 2)} />);

    await expect.element(page.getByRole('table', { name: 'Drones table' })).toBeInTheDocument();
    await expect.element(page.getByText('Alpha')).toBeInTheDocument();
    await expect.element(page.getByText('Charlie')).toBeInTheDocument();

    rerender(<DataTableHarness data={[]} />);
    await expect.element(page.getByText('No results.')).toBeInTheDocument();
  });

  it('supports row selection, select-all on page, actionBar, and Escape to clear', async () => {
    const onEscapeKeyDown = vi.fn();

    render(
      <DataTableHarness
        data={SAMPLE_DATA.slice(0, 3)}
        pageSize={2}
        withSelection
        actionBar={<div>Bulk actions</div>}
        onEscapeKeyDown={onEscapeKeyDown}
      />
    );

    await expect.element(page.getByText('Bulk actions')).not.toBeInTheDocument();
    await expect.element(page.getByText('0 rows selected.')).toBeInTheDocument();

    await userEvent.click(page.getByRole('checkbox', { name: 'Select row' }).first());

    await expect.element(page.getByText('1 row selected.')).toBeInTheDocument();
    await expect.element(page.getByText('Bulk actions')).toBeInTheDocument();

    await userEvent.click(page.getByRole('checkbox', { name: 'Select all' }));
    await expect.element(page.getByText('2 rows selected.')).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(onEscapeKeyDown).toHaveBeenCalledTimes(1);
    await expect.element(page.getByText('0 rows selected.')).toBeInTheDocument();
    await expect.element(page.getByText('Bulk actions')).not.toBeInTheDocument();
  });

  it('preserves selection when onEscapeKeyDown calls preventDefault', async () => {
    render(
      <DataTableHarness
        data={SAMPLE_DATA.slice(0, 2)}
        withSelection
        actionBar={<div>Bulk actions</div>}
        onEscapeKeyDown={(event) => event.preventDefault()}
      />
    );

    await userEvent.click(page.getByRole('checkbox', { name: 'Select row' }).first());
    await expect.element(page.getByText('1 row selected.')).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    await expect.element(page.getByText('1 row selected.')).toBeInTheDocument();
    await expect.element(page.getByText('Bulk actions')).toBeInTheDocument();
  });

  it('paginates through pages with pagination controls', async () => {
    render(<DataTableHarness data={SAMPLE_DATA} pageSize={2} />);

    await expect.element(page.getByText('Page 1 of 3')).toBeInTheDocument();
    await expect.element(page.getByText('Alpha')).toBeInTheDocument();
    await expect.element(page.getByText('Bravo')).not.toBeInTheDocument();

    const firstBtn = page.getByRole('button', { name: 'Go to first page' });
    const prevBtn = page.getByRole('button', { name: 'Go to previous page' });
    const nextBtn = page.getByRole('button', { name: 'Go to next page' });
    const lastBtn = page.getByRole('button', { name: 'Go to last page' });

    await expect.element(firstBtn).toBeDisabled();
    await expect.element(prevBtn).toBeDisabled();
    await expect.element(nextBtn).toBeEnabled();
    await expect.element(lastBtn).toBeEnabled();

    await userEvent.click(nextBtn);
    await expect.element(page.getByText('Page 2 of 3')).toBeInTheDocument();
    await expect.element(page.getByText('Bravo')).toBeInTheDocument();
    await expect.element(page.getByText('Alpha')).not.toBeInTheDocument();

    await userEvent.click(lastBtn);
    await expect.element(page.getByText('Page 3 of 3')).toBeInTheDocument();
    await expect.element(page.getByText('Echo')).toBeInTheDocument();
    await expect.element(nextBtn).toBeDisabled();
    await expect.element(lastBtn).toBeDisabled();

    await userEvent.click(prevBtn);
    await expect.element(page.getByText('Page 2 of 3')).toBeInTheDocument();

    await userEvent.click(firstBtn);
    await expect.element(page.getByText('Page 1 of 3')).toBeInTheDocument();
  });

  it('sorts and hides columns via DataTableColumnHeader', async () => {
    render(<DataTableHarness data={SAMPLE_DATA.slice(0, 3)} />);

    const nameButton = page.getByRole('button', { name: 'Name' });
    const nameHeader = page.getByRole('cell', { name: 'Name' });
    await userEvent.click(nameButton);
    await userEvent.click(page.getByRole('menuitemcheckbox', { name: 'Asc' }));
    await userEvent.keyboard('{Escape}');

    await expect.element(nameHeader).toHaveAttribute('aria-sort', 'ascending');
    await expect.element(page.getByRole('row').nth(1).getByText('Alpha')).toBeInTheDocument();
    await expect.element(page.getByRole('row').nth(2).getByText('Bravo')).toBeInTheDocument();
    await expect.element(page.getByRole('row').nth(3).getByText('Charlie')).toBeInTheDocument();

    await userEvent.click(nameButton);
    await userEvent.click(page.getByRole('menuitemcheckbox', { name: 'Desc' }));
    await userEvent.keyboard('{Escape}');

    await expect.element(nameHeader).toHaveAttribute('aria-sort', 'descending');
    await expect.element(page.getByRole('row').nth(1).getByText('Charlie')).toBeInTheDocument();
    await expect.element(page.getByRole('row').nth(2).getByText('Bravo')).toBeInTheDocument();
    await expect.element(page.getByRole('row').nth(3).getByText('Alpha')).toBeInTheDocument();

    await userEvent.click(nameButton);
    await userEvent.click(page.getByRole('menuitemcheckbox', { name: 'Hide' }));
    await expect.element(nameHeader).not.toBeInTheDocument();
  });

  it('filters rows with global and column filters and resets via toolbar', async () => {
    render(<DataTableHarness data={SAMPLE_DATA.slice(0, 3)} />);

    const globalInput = page.getByLabelText('Search drones...');
    await userEvent.fill(globalInput, 'Bravo');

    await expect.element(page.getByText('Bravo')).toBeInTheDocument();
    await expect.element(page.getByText('Alpha')).not.toBeInTheDocument();
    await expect.element(page.getByRole('button', { name: 'Reset filters' })).toBeInTheDocument();

    await userEvent.click(page.getByRole('button', { name: 'Reset filters' }));
    await expect.element(page.getByText('Alpha')).toBeInTheDocument();
    await expect.element(page.getByText('Charlie')).toBeInTheDocument();

    const nameFilterInput = page.getByPlaceholder('Filter name...');
    await userEvent.fill(nameFilterInput, 'Alpha');
    await expect.element(page.getByText('Alpha')).toBeInTheDocument();
    await expect.element(page.getByText('Bravo')).not.toBeInTheDocument();

    await userEvent.click(page.getByRole('button', { name: 'Reset filters' }));
    await expect.element(page.getByText('Bravo')).toBeInTheDocument();
  });

  it('renders and invokes row actions from getDataTableActionsColumn', async () => {
    const onActionClick = vi.fn();

    render(<DataTableHarness data={SAMPLE_DATA.slice(0, 2)} withActions onActionClick={onActionClick} />);

    await userEvent.click(page.getByRole('button', { name: 'Open actions for row 1' }));
    await userEvent.click(page.getByRole('menuitem', { name: 'Inspect Alpha' }));

    expect(onActionClick).toHaveBeenCalledTimes(1);
    expect(onActionClick).toHaveBeenCalledWith(SAMPLE_DATA[0]);
  });
});
