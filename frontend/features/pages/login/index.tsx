import LoginForm from './components/LoginForm';
import LoginHeader from './components/LoginHeader';
import LoginVideo from './components/LoginVideo';

export default function LoginPage() {
  return (
    <section className="bg-background text-foreground min-h-dvh">
      <LoginHeader />
      <main className="mx-auto grid min-h-[calc(100dvh-88px)] max-w-360 items-center gap-8 px-6 py-4 sm:px-10 lg:grid-cols-2 lg:gap-16 lg:px-16 lg:py-8">
        <div className="mx-auto w-full max-w-115 text-center">
          <h1 className="text-4xl leading-[1.12] font-medium tracking-tight sm:text-5xl xl:text-[56px]">
            See what’s next.
          </h1>
          <p className="text-muted-foreground mt-4 text-base sm:text-lg">Your perspective on a changing sky.</p>
          <LoginForm />
          <p className="text-muted-foreground mt-7 text-xs">One workspace. The whole picture.</p>
        </div>
        <LoginVideo />
      </main>
    </section>
  );
}
