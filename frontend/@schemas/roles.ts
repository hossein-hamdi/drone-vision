import { z } from 'zod';

export const roleSchema = z.object({
  name: z.string().trim().min(1, 'Role name is required').max(256),
  permissions: z.array(z.string()).max(100),
});

export type RoleSchema = z.infer<typeof roleSchema>;
