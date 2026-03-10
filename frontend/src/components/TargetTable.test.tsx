import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { TargetsResponseDto } from '../api/types';
import { TargetTable } from './TargetTable';

const targets: TargetsResponseDto = {
  monthId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  referenceMonth: '2026-03',
  items: [
    {
      groupName: 'Essenciais',
      targetPercent: 0.6,
      currentPlannedPercent: 0.55,
      currentSpentPercent: 0.5,
      plannedDifference: -0.05,
      plannedStatus: 'Abaixo',
      spentDifference: -0.1,
      spentStatus: 'Abaixo'
    },
    {
      groupName: 'Buffer',
      targetPercent: 0,
      currentPlannedPercent: 0,
      currentSpentPercent: 0,
      plannedDifference: 0,
      plannedStatus: 'OK',
      spentDifference: 0,
      spentStatus: 'OK'
    }
  ]
};

describe('TargetTable', () => {
  it('edits target percentages as 0 to 100 and persists fractions', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn(async () => {});

    render(<TargetTable targets={targets} readOnly={false} onSave={onSave} />);

    const [bufferInput, essenciaisInput] = screen.getAllByRole('spinbutton');
    await user.clear(essenciaisInput);
    await user.type(essenciaisInput, '75');

    await user.clear(bufferInput);
    await user.type(bufferInput, '120');

    await user.click(screen.getByRole('button', { name: /salvar metas/i }));

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith({
        items: [
          { groupName: 'Essenciais', targetPercent: 0.75 },
          { groupName: 'Buffer', targetPercent: 1 }
        ]
      });
    });
  });
});
