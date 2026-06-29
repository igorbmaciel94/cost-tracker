import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { TargetsResponseDto } from '../api/types';
import { TargetTable } from './TargetTable';

const targets: TargetsResponseDto = {
  monthId: 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
  referenceMonth: '2026-03',
  items: [
    {
      groupName: 'Custos Fixos',
      targetPercent: 0.6,
      currentPlannedPercent: 0.55,
      currentSpentPercent: 0.5,
      currentPlannedAmount: 1100,
      currentSpentAmount: 500,
      plannedDifference: -0.05,
      plannedStatus: 'Abaixo',
      spentDifference: -700,
      spentStatus: 'Abaixo'
    },
    {
      groupName: 'Metas',
      targetPercent: 0,
      currentPlannedPercent: 0,
      currentSpentPercent: 0,
      currentPlannedAmount: 0,
      currentSpentAmount: 0,
      plannedDifference: 0,
      plannedStatus: 'OK',
      spentDifference: 0,
      spentStatus: 'OK'
    }
  ]
};

describe('TargetTable', () => {
  it('enables saving only when target percentages total 100 and persists fractions', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn(async () => {});

    render(<TargetTable targets={targets} readOnly={false} plannedTotal={2000} spentTotal={1000} onSave={onSave} />);

    const saveButton = screen.getByRole('button', { name: /salvar planejamento/i });
    expect(saveButton).toBeDisabled();
    expect(screen.getByText('Leitura do planejamento')).toBeInTheDocument();
    expect(screen.getByText(/500,00.*(?:1\s?)?200,00/)).toBeInTheDocument();
    expect(screen.getByText(/Restam.*700,00/)).toBeInTheDocument();

    const [custosSlider, metasSlider] = screen.getAllByRole('slider');
    fireEvent.change(custosSlider, { target: { value: '75' } });
    fireEvent.change(metasSlider, { target: { value: '25' } });

    expect(screen.getByText(/Total: 100%/i)).toBeInTheDocument();
    expect(saveButton).toBeEnabled();

    await user.click(saveButton);

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith({
        items: [
          { groupName: 'Custos Fixos', targetPercent: 0.75 },
          { groupName: 'Metas', targetPercent: 0.25 }
        ]
      });
    });
  });

  it('compares current spending with the fixed target amount instead of spent percentage share', () => {
    const skewedTargets: TargetsResponseDto = {
      ...targets,
      items: [
        {
          ...targets.items[0],
          targetPercent: 0.6,
          currentPlannedPercent: 0.6,
          currentSpentPercent: 0.75,
          currentPlannedAmount: 600,
          currentSpentAmount: 300,
          plannedDifference: 0,
          plannedStatus: 'OK',
          spentDifference: -300,
          spentStatus: 'Abaixo'
        },
        {
          ...targets.items[1],
          targetPercent: 0.4,
          currentPlannedPercent: 0.4,
          currentSpentPercent: 0.25,
          currentPlannedAmount: 400,
          currentSpentAmount: 100
        }
      ]
    };

    render(<TargetTable targets={skewedTargets} readOnly={false} plannedTotal={1000} spentTotal={400} onSave={vi.fn()} />);

    const spentCell = screen.getByText(/300,00.*600,00/);
    const row = spentCell.closest('tr');

    expect(spentCell).toBeInTheDocument();
    expect(row).not.toBeNull();
    expect(within(row as HTMLTableRowElement).getByText(/Restam.*300,00/)).toBeInTheDocument();
    expect(within(row as HTMLTableRowElement).getByText('Abaixo')).toBeInTheDocument();
    expect(within(row as HTMLTableRowElement).queryByText('Acima')).not.toBeInTheDocument();
  });
});
