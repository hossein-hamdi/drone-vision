import { describe, expect, it } from 'vitest';

import { updateProfileSchema } from '../profile';

describe('updateProfileSchema', () => {
  const valid = { fullName: 'Jane Doe', userName: 'janedoe' };

  it('accepts a valid payload and trims fields', () => {
    const result = updateProfileSchema.safeParse({ fullName: '  Jane Doe  ', userName: '  janedoe  ' });
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
  ])('rejects %s', (_label, payload, expectedMessage) => {
    const result = updateProfileSchema.safeParse(payload);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe(expectedMessage);
    }
  });

  it('enforces max length on fullName and userName', () => {
    expect(updateProfileSchema.safeParse({ ...valid, fullName: 'a'.repeat(201) }).success).toBe(false);
    expect(updateProfileSchema.safeParse({ ...valid, userName: 'a'.repeat(257) }).success).toBe(false);
  });
});
