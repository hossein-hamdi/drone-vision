import type { ReactElement, ReactNode } from 'react';

import type { LucideIcon } from 'lucide-react';

export interface BreadcrumbSegment {
  label: string;
  href?: string;
}

export interface DashboardBreadcrumbProps {
  /** Page heading displayed below the breadcrumb. */
  title: string;
  /** Short explanation of what this page is and why users use it. */
  description?: string;
  /** Optional Lucide icon displayed beside the page title. */
  icon?: ReactElement<LucideIcon>;
  /** Optional action buttons or controls aligned with the page title. */
  actions?: ReactNode;
  className?: string;
}
