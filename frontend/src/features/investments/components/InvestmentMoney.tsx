import { PrivacyMask } from '../../../contexts/PrivacyContext';
import { formatCurrency } from '../../../utils/format';
import type { CurrencyCode } from '../types';

interface InvestmentMoneyProps {
  value?: number | null;
  currency?: CurrencyCode;
  unavailableLabel?: string;
}

export function InvestmentMoney({
  value,
  currency = 'EUR',
  unavailableLabel = 'Indisponível'
}: InvestmentMoneyProps) {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return <span className="investment-unavailable">{unavailableLabel}</span>;
  }
  return <PrivacyMask value={formatCurrency(value, currency)} />;
}
