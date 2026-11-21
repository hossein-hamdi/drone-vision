import { describe, expect, it } from 'vitest';

import { roleSchema } from '../roles';

describe('roleSchema', () => {
  const valid = {
    name: 'Operator',
    permissions: ['Users.View', 'Roles.View'],
  };

  it('accepts a valid payload and trims role name', () => {
    const result = roleSchema.safeParse({
      ...valid,
      name: '  Operator  ',
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data).toEqual(valid);
    }
  });

  it.each([
    ['empty name', { ...valid, name: '' }, 'Role name is required'],
    ['whitespace name', { ...valid, name: '   ' }, 'Role name is required'],
  ])('rejects %s', (_label, payload, expectedMessage) => {
    const result = roleSchema.safeParse(payload);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe(expectedMessage);
    }
  });

  it('enforces max lengths on name and permissions array', () => {
    expect(roleSchema.safeParse({ ...valid, name: 'a'.repeat(257) }).success).toBe(false);
    expect(roleSchema.safeParse({ ...valid, permissions: Array.from({ length: 101 }, () => 'perm') }).success).toBe(
      false
    );
  });
});
