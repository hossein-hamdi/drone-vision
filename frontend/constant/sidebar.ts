import { LayoutDashboard, type LucideIcon, UserRound } from 'lucide-react';

interface SidebarItem {
  title: string;
  url: string;
  icon: LucideIcon;
}

export const sidebar: SidebarItem[] = [
  { title: 'Overview', url: '/', icon: LayoutDashboard },
  { title: 'Profile', url: '/profile', icon: UserRound },
];
