import { describe, expect, it } from 'vitest';

import { loginSchema } from '../auth';

describe('loginSchema', () => {
  const valid = { username: 'pilot', password: 'secret' };

  it('accepts a valid payload and trims username', () => {
    const result = loginSchema.safeParse({ username: '  pilot  ', password: 'secret' });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data).toEqual(valid);
    }
  });

  it.each([
    ['empty username', { ...valid, username: '' }, 'Enter your username.'],
    ['whitespace username', { ...valid, username: '   ' }, 'Enter your username.'],
    ['empty password', { ...valid, password: '' }, 'Enter your password.'],
  ])('rejects %s', (_label, payload, expectedMessage) => {
    const result = loginSchema.safeParse(payload);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe(expectedMessage);
    }
  });
});
