import { z } from 'zod';

export const categorySchema = z.object({
  name: z.string().trim().min(1, 'Informe o nome da categoria'),
  groupName: z.string().trim().min(1, 'Informe o grupo'),
  plannedAmount: z.coerce.number().min(0, 'Valor deve ser >= 0')
});

export const salarySchema = z.object({
  salary: z.coerce.number().min(0, 'Salário deve ser >= 0')
});

export const entrySchema = z.object({
  categoryBudgetId: z.string().uuid('Categoria inválida'),
  entryDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Data inválida'),
  description: z.string().trim().min(1, 'Descrição obrigatória'),
  amount: z.coerce.number().min(0, 'Valor deve ser >= 0')
});

export const targetSchema = z.object({
  groupName: z.string().trim().min(1),
  targetPercent: z.coerce.number().min(0).max(100)
});
