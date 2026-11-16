'use client';

import { useState } from 'react';

import { loginKeyGenerator, type LoginRequestData, type LoginResponse } from '@api/loginMutation';
import { authConfig } from '@configs/auth.config';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { ArrowRight, Eye, EyeOff } from 'lucide-react';
import { Controller, useForm } from 'react-hook-form';

import { loginSchema, type LoginSchema } from '@schemas';

import { Button } from '@components/ui/button';
import { Field, FieldError, FieldGroup, FieldLabel } from '@components/ui/field';
import { Input } from '@components/ui/input';
import { InputGroup, InputGroupAddon, InputGroupButton, InputGroupInput } from '@components/ui/input-group';

import { useToast } from '@stores/toast';

export default function LoginForm() {
  const [showPassword, setShowPassword] = useState(false);
  const { showToast } = useToast();
  const form = useForm<LoginSchema>({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: '', password: '' },
  });

  const loginMutation = useMutation<LoginResponse, Error, LoginRequestData>({
    mutationKey: loginKeyGenerator(),
    onSuccess: () => {
      form.resetField('password');
      showToast('Signed in successfully.', 'success');
      // A full navigation lets the proxy observe the HttpOnly cookie set by login.
      window.location.replace(authConfig.defaultRedirect);
    },
    onError: (error) => {
      showToast(error.message || 'Unable to sign in. Please try again.', 'error');
    },
  });

  const onSubmit = form.handleSubmit((data) => {
    loginMutation.mutate(data);
  });

  return (
    <form onSubmit={onSubmit} noValidate className="border-border mt-7 rounded-[28px] border p-6 text-left sm:p-7">
      <FieldGroup>
        <Controller
          name="username"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-username">Username</FieldLabel>
              <Input
                {...field}
                id="login-username"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                placeholder="Enter your username"
                required
                aria-invalid={fieldState.invalid}
                aria-describedby={fieldState.invalid ? 'login-username-error' : undefined}
                className="bg-muted/40 h-12 rounded-xl px-4"
              />
              {fieldState.invalid && <FieldError id="login-username-error" errors={[fieldState.error]} />}
            </Field>
          )}
        />
        <Controller
          name="password"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-password">Password</FieldLabel>
              <InputGroup className="bg-muted/40 h-12 rounded-xl">
                <InputGroupInput
                  {...field}
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="Enter your password"
                  required
                  aria-invalid={fieldState.invalid}
                  aria-describedby={fieldState.invalid ? 'login-password-error' : undefined}
                  className="h-full pl-4"
                />
                <InputGroupAddon align="inline-end">
                  <InputGroupButton
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    className="text-muted-foreground transition-none active:translate-y-0!"
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                    aria-controls="login-password"
                    onClick={() => setShowPassword((visible) => !visible)}
                  >
                    {showPassword ? <EyeOff aria-hidden="true" /> : <Eye aria-hidden="true" />}
                  </InputGroupButton>
                </InputGroupAddon>
              </InputGroup>
              {fieldState.invalid && <FieldError id="login-password-error" errors={[fieldState.error]} />}
            </Field>
          )}
        />
      </FieldGroup>
      <Button
        type="submit"
        disabled={loginMutation.isPending}
        aria-busy={loginMutation.isPending}
        className="mt-5 h-12 w-full justify-center rounded-xl px-4"
      >
        {loginMutation.isPending ? 'Signing in…' : 'Sign in'} <ArrowRight aria-hidden="true" />
      </Button>
      <p className="text-muted-foreground mt-4 text-center text-xs leading-relaxed">
        Access is managed by your workspace administrator.
      </p>
    </form>
  );
}
