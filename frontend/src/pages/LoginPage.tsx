import { useState } from 'react';
import type { LoginRequest } from '../api/types';

interface LoginPageProps {
  onLogin: (request: LoginRequest) => Promise<void>;
  loading: boolean;
  errorMessage: string | null;
}

export function LoginPage({ onLogin, loading, errorMessage }: LoginPageProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  return (
    <div className="auth-shell">
      <section className="auth-card">
        <p className="auth-kicker">Cost Tracker</p>

        <form
          className="auth-form"
          onSubmit={async (event) => {
            event.preventDefault();
            await onLogin({
              username,
              password
            });
          }}
        >
          <label>
            Username
            <input
              autoComplete="username"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
            />
          </label>
          <label>
            Password
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>
          {errorMessage && (
            <p className="inline-error" role="alert">
              {errorMessage}
            </p>
          )}
          <button type="submit" disabled={loading}>
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </section>
    </div>
  );
}
