export interface AuthUser {
  id: string;
  fullName: string;
  userName: string;
  profileImageUrl: string;
  roles: string[];
  permissions: string[];
}
