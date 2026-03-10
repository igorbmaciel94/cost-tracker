export const CANONICAL_GROUP_NAMES = ['Essenciais', 'Desejos', 'Investimento', 'Saving', 'Buffer'] as const;

export function buildGroupOptions(groupNames: string[]): string[] {
  const extras = [...new Set(groupNames.map((groupName) => groupName.trim()).filter(Boolean))]
    .filter((groupName) => !CANONICAL_GROUP_NAMES.includes(groupName as (typeof CANONICAL_GROUP_NAMES)[number]))
    .sort((left, right) => left.localeCompare(right, 'pt-PT'));

  return [...CANONICAL_GROUP_NAMES, ...extras];
}
