import { describe, expect, it } from 'vitest';

import { createUserSchema, editUserSchema } from '../users';

describe('createUserSchema', () => {
  const valid = {
    fullName: 'Jane Doe',
    userName: 'janedoe',
    password: 'StrongPassword123',
    roles: ['Admin'],
  };

  it('accepts a valid payload and trims string fields', () => {
    const result = createUserSchema.safeParse({
      ...valid,
      fullName: '  Jane Doe  ',
      userName: '  janedoe  ',
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data).toEqual(valid);
    }
  });

  it.each([
    ['empty fullName', { ...valid, fullName: '' }, 'Full name is required'],
    ['whitespace fullName', { ...valid, fullName: '   ' }, 'Full name is required'],
    ['empty userName', { ...valid, userName: '' }, 'Username is required'],
    ['whitespace userName', { ...valid, userName: '   ' }, 'Username is required'],
    ['short password', { ...valid, password: 'short' }, 'Password must be at least 12 characters'],
  ])('rejects %s', (_label, payload, expectedMessage) => {
    const result = createUserSchema.safeParse(payload);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe(expectedMessage);
    }
  });

  it('enforces max lengths on fields and roles array', () => {
    expect(createUserSchema.safeParse({ ...valid, fullName: 'a'.repeat(201) }).success).toBe(false);
    expect(createUserSchema.safeParse({ ...valid, userName: 'a'.repeat(257) }).success).toBe(false);
    expect(createUserSchema.safeParse({ ...valid, password: 'a'.repeat(129) }).success).toBe(false);
    expect(createUserSchema.safeParse({ ...valid, roles: Array.from({ length: 101 }, () => 'role') }).success).toBe(
      false
    );
  });
});

describe('editUserSchema', () => {
  const valid = {
    fullName: 'Jane Doe',
    userName: 'janedoe',
    roles: ['Viewer'],
  };

  it('accepts a valid payload without password and trims string fields', () => {
    const result = editUserSchema.safeParse({
      ...valid,
      fullName: '  Jane Doe  ',
      userName: '  janedoe  ',
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data).toEqual(valid);
    }
  });

  it.each([
    ['empty fullName', { ...valid, fullName: '' }, 'Full name is required'],
    ['empty userName', { ...valid, userName: '' }, 'Username is required'],
  ])('rejects %s', (_label, payload, expectedMessage) => {
    const result = editUserSchema.safeParse(payload);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe(expectedMessage);
    }
  });

  it('enforces max lengths on fields and roles array', () => {
    expect(editUserSchema.safeParse({ ...valid, fullName: 'a'.repeat(201) }).success).toBe(false);
    expect(editUserSchema.safeParse({ ...valid, userName: 'a'.repeat(257) }).success).toBe(false);
    expect(editUserSchema.safeParse({ ...valid, roles: Array.from({ length: 101 }, () => 'role') }).success).toBe(
      false
    );
  });
});
