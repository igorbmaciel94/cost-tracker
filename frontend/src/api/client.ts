import type {
  BudgetResponseDto,
  CreateCategoryRequest,
  CreateEntryRequest,
  CreateMonthRequest,
  DashboardDto,
  EntriesResponseDto,
  MonthSummaryDto,
  TargetsResponseDto,
  UpdateCategoryRequest,
  UpdateEntryRequest,
  UpdateSalaryRequest,
  UpdateTargetsRequest
} from './types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080';

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Erro HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  getMonths: () => apiFetch<MonthSummaryDto[]>('/api/months'),
  createNewMonth: (request?: CreateMonthRequest) =>
    apiFetch<MonthSummaryDto>('/api/months/new', {
      method: 'POST',
      body: JSON.stringify(request ?? {})
    }),
  updateSalary: (monthId: string, request: UpdateSalaryRequest) =>
    apiFetch<MonthSummaryDto>(`/api/months/${monthId}/salary`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  getBudget: (monthId: string) => apiFetch<BudgetResponseDto>(`/api/months/${monthId}/budget`),
  createCategory: (monthId: string, request: CreateCategoryRequest) =>
    apiFetch<BudgetResponseDto>(`/api/months/${monthId}/budget/categories`, {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  updateCategory: (monthId: string, categoryId: string, request: UpdateCategoryRequest) =>
    apiFetch<BudgetResponseDto>(`/api/months/${monthId}/budget/categories/${categoryId}`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  deleteCategory: (monthId: string, categoryId: string) =>
    apiFetch<BudgetResponseDto>(`/api/months/${monthId}/budget/categories/${categoryId}`, {
      method: 'DELETE'
    }),
  getEntries: (monthId: string) => apiFetch<EntriesResponseDto>(`/api/months/${monthId}/entries`),
  createEntry: (monthId: string, request: CreateEntryRequest) =>
    apiFetch<EntriesResponseDto>(`/api/months/${monthId}/entries`, {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  updateEntry: (monthId: string, entryId: string, request: UpdateEntryRequest) =>
    apiFetch<EntriesResponseDto>(`/api/months/${monthId}/entries/${entryId}`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  deleteEntry: (monthId: string, entryId: string) =>
    apiFetch<EntriesResponseDto>(`/api/months/${monthId}/entries/${entryId}`, {
      method: 'DELETE'
    }),
  getTargets: (monthId: string) => apiFetch<TargetsResponseDto>(`/api/months/${monthId}/targets`),
  updateTargets: (monthId: string, request: UpdateTargetsRequest) =>
    apiFetch<TargetsResponseDto>(`/api/months/${monthId}/targets`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  getDashboard: (monthId: string) => apiFetch<DashboardDto>(`/api/months/${monthId}/dashboard`)
};
