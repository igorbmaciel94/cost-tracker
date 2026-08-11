export const investmentQueryKeys = {
  all: ['investments'] as const,
  portfolio: () => [...investmentQueryKeys.all, 'portfolio'] as const,
  instruments: () => [...investmentQueryKeys.all, 'instruments'] as const,
  instrument: (id: string) => [...investmentQueryKeys.instruments(), id] as const,
  transactions: (id: string) => [...investmentQueryKeys.instrument(id), 'transactions'] as const,
  marketDataStatus: () => [...investmentQueryKeys.all, 'market-data-status'] as const,
  contributionPlans: () => [...investmentQueryKeys.all, 'contribution-plans'] as const,
  contributionPlan: (id: string) => [...investmentQueryKeys.contributionPlans(), id] as const
};
