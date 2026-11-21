import { z } from 'zod';

export const createUserSchema = z.object({
  fullName: z.string().trim().min(1, 'Full name is required').max(200),
  userName: z.string().trim().min(1, 'Username is required').max(256),
  password: z.string().min(12, 'Password must be at least 12 characters').max(128),
  roles: z.array(z.string()).max(100),
});

export type CreateUserSchema = z.infer<typeof createUserSchema>;

export const editUserSchema = z.object({
  fullName: z.string().trim().min(1, 'Full name is required').max(200),
  userName: z.string().trim().min(1, 'Username is required').max(256),
  roles: z.array(z.string()).max(100),
});

export type EditUserSchema = z.infer<typeof editUserSchema>;
