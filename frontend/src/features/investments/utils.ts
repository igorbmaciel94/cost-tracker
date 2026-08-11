import { ASSET_CLASSES, DEFAULT_ALLOCATION_PERCENT } from './constants';
import type { AllocationTargetDto, AssetClass, FreshnessStatus } from './types';

export function allocationToPercentages(targets: AllocationTargetDto[]) {
  const result: Record<AssetClass, number> = { ...DEFAULT_ALLOCATION_PERCENT };
  for (const target of targets) {
    result[target.assetClass] = Math.round(target.weight * 100);
  }
  return result;
}

export function percentageToWeight(value: number) {
  return value / 100;
}

export function freshnessRank(status: FreshnessStatus) {
  return { FRESH: 0, STALE: 1, BLOCKED: 2, MISSING: 3 }[status];
}

export function worstFreshness(statuses: FreshnessStatus[]): FreshnessStatus {
  return statuses.reduce<FreshnessStatus>(
    (worst, status) => freshnessRank(status) > freshnessRank(worst) ? status : worst,
    'FRESH'
  );
}

export function todayIso() {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
}

export function createIdempotencyKey(prefix: string) {
  const randomPart = typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `${prefix}-${randomPart}`;
}

export function ensureAllAssetClasses<T>(factory: (assetClass: AssetClass) => T): T[] {
  return ASSET_CLASSES.map(factory);
}
