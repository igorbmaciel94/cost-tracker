import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx)$/.test(entry) && !entry.endsWith('.test.ts') && !entry.endsWith('.test.tsx')
      ? [path]
      : [];
  });
}

describe('frontend dialogs', () => {
  it('uses application modals instead of native browser dialogs', () => {
    const violations = sourceFiles(join(process.cwd(), 'src')).flatMap((path) => {
      const source = readFileSync(path, 'utf8');
      return /\b(?:window\.)?(?:alert|confirm|prompt)\s*\(/.test(source) ? [path] : [];
    });

    expect(violations).toEqual([]);
  });
});
