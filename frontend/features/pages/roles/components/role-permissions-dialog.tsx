'use client';

import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

interface RolePermissionsDialogProps {
  role: {
    name: string;
    permissions: string[];
  } | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function RolePermissionsDialog({ role, open, onOpenChange }: RolePermissionsDialogProps) {
  const permissions = role?.permissions ?? [];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{role?.name ? `${role.name} Permissions` : 'Role Permissions'}</DialogTitle>
          <DialogDescription>
            {permissions.length > 0
              ? `${permissions.length} assigned ${permissions.length === 1 ? 'permission' : 'permissions'}.`
              : 'No permissions assigned to this role.'}
          </DialogDescription>
        </DialogHeader>

        {permissions.length > 0 ? (
          <div className="flex flex-wrap gap-1.5">
            {permissions.map((permission) => (
              <Badge key={permission} variant="secondary">
                {permission}
              </Badge>
            ))}
          </div>
        ) : (
          <p className="text-muted-foreground text-sm">This role does not have any permissions assigned.</p>
        )}

        <DialogFooter showCloseButton />
      </DialogContent>
    </Dialog>
  );
}
