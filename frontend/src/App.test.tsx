import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';

const { api } = vi.hoisted(() => ({
  api: {
    getSession: vi.fn(),
    login: vi.fn(),
    logout: vi.fn(),
    getMonths: vi.fn(),
    createNewMonth: vi.fn(),
    updateSalary: vi.fn(),
    getBudget: vi.fn(),
    createCategory: vi.fn(),
    updateCategory: vi.fn(),
    deleteCategory: vi.fn(),
    getEntries: vi.fn(),
    createEntry: vi.fn(),
    updateEntry: vi.fn(),
    deleteEntry: vi.fn(),
    getTargets: vi.fn(),
    updateTargets: vi.fn(),
    getDashboard: vi.fn()
  }
}));

vi.mock('./api/client', () => ({
  api
}));

describe('App auth flow', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getMonths.mockResolvedValue([]);
  });

  it('gates the app behind login and supports logout', async () => {
    api.getSession
      .mockResolvedValueOnce({ isAuthenticated: false, username: null })
      .mockResolvedValue({ isAuthenticated: true, username: 'igor' });
    api.login.mockResolvedValue({ isAuthenticated: true, username: 'igor' });
    api.logout.mockImplementation(async () => {
      api.getSession.mockResolvedValue({ isAuthenticated: false, username: null });
      return { isAuthenticated: false, username: null };
    });

    const user = userEvent.setup();
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false
        }
      }
    });

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/login']}>
          <App />
        </MemoryRouter>
      </QueryClientProvider>
    );

    expect(await screen.findByLabelText(/username/i)).toBeInTheDocument();

    await user.type(screen.getByLabelText(/username/i), 'igor');
    await user.type(screen.getByLabelText(/password/i), 'senha-segura');
    await user.click(screen.getByRole('button', { name: /^entrar$/i }));

    await waitFor(() => {
      expect(api.login).toHaveBeenCalledWith({
        username: 'igor',
        password: 'senha-segura'
      });
    });

    expect(await screen.findByRole('button', { name: /sair \(igor\)/i })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /sair \(igor\)/i }));

    expect(await screen.findByLabelText(/username/i)).toBeInTheDocument();
  });
});
