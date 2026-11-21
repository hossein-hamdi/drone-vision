'use client';

import { Fragment } from 'react';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import { cn } from 'cn';
import { ArrowLeft } from 'lucide-react';

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@components/ui/breadcrumb';
import { Button } from '@components/ui/button';

import type { BreadcrumbSegment, DashboardBreadcrumbProps } from './@types';

export type { BreadcrumbSegment, DashboardBreadcrumbProps };

function formatSegmentLabel(segment: string): string {
  return segment[0].toUpperCase() + segment.substring(1);
}

function buildBreadcrumbSegments(pathname: string): BreadcrumbSegment[] {
  const segments = pathname.split('/').filter(Boolean);

  if (segments.length === 0) {
    return [{ label: 'Overview' }];
  }

  const items: BreadcrumbSegment[] = [{ label: 'Overview', href: '/' }];

  let currentPath = '';
  for (let index = 0; index < segments.length; index += 1) {
    const segment = segments[index];
    currentPath += `/${segment}`;
    const isLast = index === segments.length - 1;
    const label = formatSegmentLabel(segment);

    items.push(isLast ? { label } : { label, href: currentPath });
  }

  return items;
}

export function DashboardBreadcrumb({ title, description, icon: Icon, actions, className }: DashboardBreadcrumbProps) {
  const pathname = usePathname();
  const router = useRouter();

  const items = buildBreadcrumbSegments(pathname);
  const isNestedRoute = items.length > 1;

  const handleBack = () => {
    router.back();
  };

  return (
    <div className={cn('flex flex-col gap-4', className)}>
      <div className="flex flex-wrap items-center gap-3">
        {isNestedRoute && (
          <Button variant="outline" size="sm" type="button" onClick={handleBack} aria-label="Go back">
            <ArrowLeft data-icon="inline-start" aria-hidden="true" />
            Back
          </Button>
        )}

        <Breadcrumb>
          <BreadcrumbList>
            {items.map((item, index) => {
              const isLast = index === items.length - 1;
              return (
                <Fragment key={`${item.label}-${index}`}>
                  {index > 0 && <BreadcrumbSeparator />}
                  <BreadcrumbItem>
                    {isLast || !item.href ? (
                      <BreadcrumbPage>{item.label}</BreadcrumbPage>
                    ) : (
                      <BreadcrumbLink render={<Link href={item.href} />}>{item.label}</BreadcrumbLink>
                    )}
                  </BreadcrumbItem>
                </Fragment>
              );
            })}
          </BreadcrumbList>
        </Breadcrumb>
      </div>

      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex items-start gap-3">
          {Icon && (
            <div className="bg-muted text-muted-foreground border-border flex size-10 shrink-0 items-center justify-center rounded-lg border">
              {Icon}
            </div>
          )}
          <div className="flex flex-col gap-1">
            <h1 className="text-foreground text-2xl font-semibold tracking-tight">{title}</h1>
            {description && <p className="text-muted-foreground max-w-2xl text-sm leading-relaxed">{description}</p>}
          </div>
        </div>
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}

export default DashboardBreadcrumb;
