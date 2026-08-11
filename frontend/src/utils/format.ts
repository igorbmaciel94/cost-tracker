import { format } from 'date-fns';

const moneyFormatters = new Map<string, Intl.NumberFormat>();

const percentFormatter = new Intl.NumberFormat('pt-PT', {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1
});

export function formatCurrency(value: number, currency = 'EUR'): string {
  const normalizedCurrency = currency.toUpperCase() === 'GBX' ? 'GBP' : currency.toUpperCase();
  let formatter = moneyFormatters.get(normalizedCurrency);
  if (!formatter) {
    try {
      formatter = new Intl.NumberFormat('pt-PT', {
        style: 'currency',
        currency: normalizedCurrency,
        maximumFractionDigits: normalizedCurrency === 'EUR' ? 2 : undefined
      });
    } catch {
      formatter = new Intl.NumberFormat('pt-PT', {
        style: 'decimal',
        maximumFractionDigits: 2
      });
    }
    moneyFormatters.set(normalizedCurrency, formatter);
  }
  const displayValue = currency.toUpperCase() === 'GBX' ? value / 100 : value;
  const formatted = formatter.format(displayValue);
  return currency.toUpperCase() === 'GBX' ? `${formatted} (GBX ${value.toLocaleString('pt-PT')})` : formatted;
}

export function formatPercent(value: number): string {
  return percentFormatter.format(value);
}

export function formatDateIsoToPt(value: string): string {
  try {
    return format(new Date(`${value}T00:00:00`), 'dd/MM/yyyy');
  } catch {
    return value;
  }
}
