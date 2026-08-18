import { z } from 'zod';
import { PERCENT_TOTAL } from './constants';

const decimalTextPattern = /^(?:\d+(?:\.\d*)?|\.\d+)$/;

export function decimalTextSchema({
  allowEmpty = false,
  allowZero = false,
  message = 'Use somente números e ponto como separador decimal.'
}: { allowEmpty?: boolean; allowZero?: boolean; message?: string } = {}) {
  return z.string().trim().refine((value) => {
    if (allowEmpty && value === '') return true;
    if (!decimalTextPattern.test(value)) return false;
    const parsed = Number(value);
    return Number.isFinite(parsed) && (allowZero ? parsed >= 0 : parsed > 0);
  }, message);
}

const investableAssetClassSchema = z.enum([
  'STOCKS',
  'REITS',
  'BRAZIL_FIXED_INCOME',
  'INTERNATIONAL_FIXED_INCOME'
]);

const allocationAssetClassSchema = z.enum([
  'STOCKS',
  'REITS',
  'BRAZIL_FIXED_INCOME',
  'INTERNATIONAL_FIXED_INCOME',
  'CRYPTOCURRENCIES'
]);

export const allocationSchema = z.object({
  targets: z.record(allocationAssetClassSchema, z.number().int().min(0).max(PERCENT_TOTAL))
}).superRefine(({ targets }, context) => {
  const total = Object.values(targets).reduce<number>((sum, value) => sum + value, 0);
  if (total !== PERCENT_TOTAL) {
    context.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['targets'],
      message: 'A soma das cinco categorias precisa ser exatamente 100%.'
    });
  }
});

export const instrumentFormSchema = z.object({
  assetClass: investableAssetClassSchema,
  kind: z.enum(['STOCK', 'ETF', 'ADR', 'REIT', 'BOND', 'ACCOUNT']),
  name: z.string().trim().min(2, 'Informe um nome com pelo menos 2 caracteres.'),
  ticker: z.string().trim().max(24),
  mic: z.string().trim().max(12),
  isin: z.string().trim().max(12).refine((value) => !value || /^[a-z0-9]{12}$/i.test(value), 'O ISIN deve ter exatamente 12 letras ou números.'),
  identityConfirmed: z.boolean(),
  nativeCurrency: z.string().trim().length(3, 'Use o código ISO de 3 letras.'),
  allocationScore: z.coerce.number().int().min(0).max(100),
  quantityStep: z.coerce.number().positive().max(1),
  openingQuantity: z.union([z.literal(''), z.coerce.number().positive()]),
  openingUnitCost: decimalTextSchema({ allowEmpty: true }),
  openingBalance: z.union([z.literal(''), z.coerce.number().nonnegative()]),
  asOf: z.string().min(1, 'Informe a data de referência.')
}).superRefine((values, context) => {
  const isMarket = values.assetClass === 'STOCKS' || values.assetClass === 'REITS';
  if (isMarket && !values.ticker) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['ticker'], message: 'Informe o ticker.' });
  }
  if (isMarket && !values.mic) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['mic'], message: 'Confirme a bolsa/MIC.' });
  }
  if (isMarket && !values.identityConfirmed) {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['identityConfirmed'], message: 'Confirme a identidade e a moeda do instrumento.' });
  }
  if (!isMarket && values.openingBalance === '') {
    context.addIssue({ code: z.ZodIssueCode.custom, path: ['openingBalance'], message: 'Informe o saldo resgatável atual.' });
  }
});

export const contributionAmountSchema = z.object({
  amount: z.coerce.number().positive('Informe um aporte maior que zero.').max(100_000_000)
});

export type AllocationFormValues = z.infer<typeof allocationSchema>;
export type InstrumentFormValues = z.input<typeof instrumentFormSchema>;
export type ContributionAmountFormValues = z.input<typeof contributionAmountSchema>;
