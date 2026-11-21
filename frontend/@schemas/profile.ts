import { z } from 'zod';

export const updateProfileSchema = z.object({
  fullName: z.string().trim().min(1, 'Full name is required').max(200),
  userName: z.string().trim().min(1, 'Username is required').max(256),
});

export type UpdateProfileSchema = z.infer<typeof updateProfileSchema>;
