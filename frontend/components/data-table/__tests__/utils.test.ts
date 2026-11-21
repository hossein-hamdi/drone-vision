import { describe, expect, it } from 'vitest';

import type { ColumnFilterItem } from '../@types';
import { getDateFilterRange } from '../lib/utils';

function makeDateFilter(operator: ColumnFilterItem['operator'], value: string | string[]): ColumnFilterItem[] {
  return [
    {
      id: 'createdAt',
      variant: 'date',
      operator,
      value,
      filterId: 'createdAt-0',
    },
  ];
}

function expectedLocalRange(
  startYear: number,
  startMonth: number,
  startDay: number,
  endYear = startYear,
  endMonth = startMonth,
  endDay = startDay
) {
  const from = new Date(startYear, startMonth - 1, startDay, 0, 0, 0, 0).toISOString();
  const to = new Date(endYear, endMonth - 1, endDay, 23, 59, 59, 999).toISOString();
  return { CreatedAtFrom: from, CreatedAtTo: to };
}

describe('getDateFilterRange', () => {
  it('returns empty object when filters are empty or missing', () => {
    expect(getDateFilterRange(undefined)).toEqual({});
    expect(getDateFilterRange(null)).toEqual({});
    expect(getDateFilterRange([])).toEqual({});
  });

  it('handles eq for a single day', () => {
    expect(getDateFilterRange(makeDateFilter('eq', '2026-11-20'))).toEqual(expectedLocalRange(2026, 11, 20));
  });

  it('handles isBetween with two dates and single-date fallback', () => {
    expect(getDateFilterRange(makeDateFilter('isBetween', ['2026-11-18', '2026-11-20']))).toEqual(
      expectedLocalRange(2026, 11, 18, 2026, 11, 20)
    );
    expect(getDateFilterRange(makeDateFilter('isBetween', ['2026-11-18', '']))).toEqual(
      expectedLocalRange(2026, 11, 18)
    );
  });

  it('handles gte inclusively and gt exclusively', () => {
    expect(getDateFilterRange(makeDateFilter('gte', '2026-11-20'))).toEqual({
      CreatedAtFrom: new Date(2026, 10, 20, 0, 0, 0, 0).toISOString(),
      CreatedAtTo: undefined,
    });
    expect(getDateFilterRange(makeDateFilter('gt', '2026-11-20'))).toEqual({
      CreatedAtFrom: new Date(2026, 10, 21, 0, 0, 0, 0).toISOString(),
      CreatedAtTo: undefined,
    });
  });

  it('handles lte inclusively and lt exclusively', () => {
    expect(getDateFilterRange(makeDateFilter('lte', '2026-11-20'))).toEqual({
      CreatedAtFrom: undefined,
      CreatedAtTo: new Date(2026, 10, 20, 23, 59, 59, 999).toISOString(),
    });
    expect(getDateFilterRange(makeDateFilter('lt', '2026-11-20'))).toEqual({
      CreatedAtFrom: undefined,
      CreatedAtTo: new Date(2026, 10, 19, 23, 59, 59, 999).toISOString(),
    });
  });

  it('returns empty object for unsupported operators and undefined values for invalid dates', () => {
    expect(getDateFilterRange(makeDateFilter('isEmpty', ''))).toEqual({});
    expect(getDateFilterRange(makeDateFilter('eq', 'not-a-date'))).toEqual({
      CreatedAtFrom: undefined,
      CreatedAtTo: undefined,
    });
  });
});
