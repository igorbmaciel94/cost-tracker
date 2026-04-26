import type {
  AuthSessionDto,
  BudgetResponseDto,
  CreateCategoryRequest,
  CreateEntryRequest,
  CreateMonthRequest,
  CreatePlanningGoalRequest,
  DashboardDto,
  EntriesResponseDto,
  HealthProfileDto,
  LoginRequest,
  MonthSummaryDto,
  PlanningGoalDto,
  TargetsResponseDto,
  UpdateCategoryRequest,
  UpdateEntryRequest,
  UpdateHealthProfileRequest,
  UpdatePlanningGoalRequest,
  UpdateSalaryRequest,
  UpdateTargetsRequest
} from './types';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  (import.meta.env.PROD ? '/api' : 'http://localhost:8080/api');

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  const response = await fetch(`${API_BASE_URL}${normalizedPath}`, {
    ...init,
    credentials: 'include',
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
  getSession: () => apiFetch<AuthSessionDto>('/auth/session'),
  login: (request: LoginRequest) =>
    apiFetch<AuthSessionDto>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  logout: () =>
    apiFetch<AuthSessionDto>('/auth/logout', {
      method: 'POST'
    }),
  getMonths: () => apiFetch<MonthSummaryDto[]>('/months'),
  createNewMonth: (request?: CreateMonthRequest) =>
    apiFetch<MonthSummaryDto>('/months/new', {
      method: 'POST',
      body: JSON.stringify(request ?? {})
    }),
  updateSalary: (monthId: string, request: UpdateSalaryRequest) =>
    apiFetch<MonthSummaryDto>(`/months/${monthId}/salary`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  getBudget: (monthId: string) => apiFetch<BudgetResponseDto>(`/months/${monthId}/budget`),
  createCategory: (monthId: string, request: CreateCategoryRequest) =>
    apiFetch<BudgetResponseDto>(`/months/${monthId}/budget/categories`, {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  updateCategory: (monthId: string, categoryId: string, request: UpdateCategoryRequest) =>
    apiFetch<BudgetResponseDto>(`/months/${monthId}/budget/categories/${categoryId}`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  deleteCategory: (monthId: string, categoryId: string) =>
    apiFetch<BudgetResponseDto>(`/months/${monthId}/budget/categories/${categoryId}`, {
      method: 'DELETE'
    }),
  getEntries: (monthId: string) => apiFetch<EntriesResponseDto>(`/months/${monthId}/entries`),
  createEntry: (monthId: string, request: CreateEntryRequest) =>
    apiFetch<EntriesResponseDto>(`/months/${monthId}/entries`, {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  updateEntry: (monthId: string, entryId: string, request: UpdateEntryRequest) =>
    apiFetch<EntriesResponseDto>(`/months/${monthId}/entries/${entryId}`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  deleteEntry: (monthId: string, entryId: string) =>
    apiFetch<EntriesResponseDto>(`/months/${monthId}/entries/${entryId}`, {
      method: 'DELETE'
    }),
  getTargets: (monthId: string) => apiFetch<TargetsResponseDto>(`/months/${monthId}/targets`),
  updateTargets: (monthId: string, request: UpdateTargetsRequest) =>
    apiFetch<TargetsResponseDto>(`/months/${monthId}/targets`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  getDashboard: (monthId: string) => apiFetch<DashboardDto>(`/months/${monthId}/dashboard`),
  getPlanningGoals: () => apiFetch<PlanningGoalDto[]>('/planning/goals'),
  createPlanningGoal: (request: CreatePlanningGoalRequest) =>
    apiFetch<PlanningGoalDto[]>('/planning/goals', {
      method: 'POST',
      body: JSON.stringify(request)
    }),
  updatePlanningGoal: (id: string, request: UpdatePlanningGoalRequest) =>
    apiFetch<PlanningGoalDto[]>(`/planning/goals/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  deletePlanningGoal: (id: string) =>
    apiFetch<PlanningGoalDto[]>(`/planning/goals/${id}`, { method: 'DELETE' }),
  getHealthProfile: () => apiFetch<HealthProfileDto>('/financial-health/profile'),
  updateHealthProfile: (request: UpdateHealthProfileRequest) =>
    apiFetch<HealthProfileDto>('/financial-health/profile', {
      method: 'PUT',
      body: JSON.stringify(request)
    })
};
