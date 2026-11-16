import AuthGuard from '@features/guards/auth-guard';

export default function Page() {
  return (
    <AuthGuard>
      <div>fopko</div>
    </AuthGuard>
  );
}
