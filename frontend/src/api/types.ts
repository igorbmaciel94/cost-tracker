export type MonthStatus = 'OPEN' | 'CLOSED';

export interface MonthSummaryDto {
  id: string;
  referenceMonth: string;
  salary: number;
  currency: string;
  status: MonthStatus;
  plannedTotal: number;
  spentTotal: number;
  differenceTotal: number;
  isOverPlanned: boolean;
  isOverSpent: boolean;
}

export interface BudgetLineDto {
  id: string;
  name: string;
  groupName: string;
  planned: number;
  spent: number;
  difference: number;
  displayOrder: number;
}

export interface BudgetResponseDto {
  monthId: string;
  referenceMonth: string;
  salary: number;
  plannedTotal: number;
  spentTotal: number;
  differenceTotal: number;
  lines: BudgetLineDto[];
}

export interface EntryDto {
  id: string;
  categoryBudgetId: string;
  categoryName: string;
  entryDate: string;
  description: string;
  amount: number;
}

export interface EntriesResponseDto {
  monthId: string;
  referenceMonth: string;
  totalSpent: number;
  items: EntryDto[];
}

export interface TargetGroupDto {
  groupName: string;
  targetPercent: number;
  currentPlannedPercent: number;
  currentSpentPercent: number;
  plannedDifference: number;
  plannedStatus: string;
  spentDifference: number;
  spentStatus: string;
}

export interface TargetsResponseDto {
  monthId: string;
  referenceMonth: string;
  items: TargetGroupDto[];
}

export interface DashboardCategoryPointDto {
  category: string;
  planned: number;
  spent: number;
}

export interface DashboardGroupPointDto {
  groupName: string;
  spent: number;
}

export interface DashboardDto {
  monthId: string;
  referenceMonth: string;
  salary: number;
  plannedTotal: number;
  spentTotal: number;
  isOverPlanned: boolean;
  isOverSpent: boolean;
  categoryChart: DashboardCategoryPointDto[];
  groupPie: DashboardGroupPointDto[];
}

export interface CreateMonthRequest {
  referenceMonth?: string;
}

export interface UpdateSalaryRequest {
  salary: number;
}

export interface CreateCategoryRequest {
  name: string;
  groupName: string;
  plannedAmount: number;
  displayOrder?: number;
}

export interface UpdateCategoryRequest {
  name: string;
  groupName: string;
  plannedAmount: number;
  displayOrder?: number;
}

export interface CreateEntryRequest {
  categoryBudgetId: string;
  entryDate: string;
  description: string;
  amount: number;
}

export interface UpdateEntryRequest {
  categoryBudgetId: string;
  entryDate: string;
  description: string;
  amount: number;
}

export interface UpdateTargetsRequest {
  items: Array<{
    groupName: string;
    targetPercent: number;
  }>;
}
