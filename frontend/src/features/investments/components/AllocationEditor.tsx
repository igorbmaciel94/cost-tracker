import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { ASSET_CLASS_META, ASSET_CLASSES, BASIS_POINTS_TOTAL } from '../constants';
import { allocationSchema, type AllocationFormValues } from '../schemas';
import type { AllocationTargetDto, AssetClass } from '../types';
import { allocationToBasisPoints, basisPointsToWeight } from '../utils';
import { AllocationDonut } from './AllocationDonut';

interface AllocationEditorProps {
  targets: AllocationTargetDto[];
  currentValues?: Partial<Record<AssetClass, number>>;
  disabled?: boolean;
  submitLabel?: string;
  onSubmit: (values: Record<AssetClass, number>) => Promise<void> | void;
}

export function AllocationEditor({
  targets,
  currentValues,
  disabled,
  submitLabel = 'Guardar alocação',
  onSubmit
}: AllocationEditorProps) {
  const initialTargets = allocationToBasisPoints(targets);
  const {
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting, isDirty }
  } = useForm<AllocationFormValues>({
    resolver: zodResolver(allocationSchema),
    defaultValues: { targets: initialTargets },
    mode: 'onChange'
  });

  useEffect(() => {
    reset({ targets: allocationToBasisPoints(targets) });
  }, [reset, targets]);

  const values = watch('targets');
  const total = ASSET_CLASSES.reduce((sum, assetClass) => sum + (values?.[assetClass] ?? 0), 0);
  const balanced = total === BASIS_POINTS_TOTAL;
  const donutValues = Object.fromEntries(
    ASSET_CLASSES.map((assetClass) => [assetClass, basisPointsToWeight(values?.[assetClass] ?? 0)])
  ) as Record<AssetClass, number>;

  return (
    <form
      className="allocation-editor"
      onSubmit={handleSubmit(async ({ targets: nextTargets }) => {
        await onSubmit(nextTargets as Record<AssetClass, number>);
      })}
    >
      <div className="allocation-editor-summary">
        <div className="allocation-total" data-balanced={balanced} aria-live="polite">
          <span>Total definido</span>
          <strong>{(total / 100).toFixed(2).replace('.', ',')}%</strong>
          <small>{balanced ? 'Pronto para guardar' : `Faltam ${((BASIS_POINTS_TOTAL - total) / 100).toFixed(2).replace('.', ',')} pontos percentuais`}</small>
        </div>
        <AllocationDonut values={donutValues} centerLabel="Meta" centerValue={`${(total / 100).toFixed(2)}%`} title="Metas de alocação" />
      </div>

      <div className="allocation-editor-fields">
        <header>
          <div>
            <h2>Defina as quatro metas</h2>
            <p>A soma precisa ser exatamente 100%. A precisão é de 0,01 ponto percentual.</p>
          </div>
        </header>

        {ASSET_CLASSES.map((assetClass) => {
          const basisPoints = values?.[assetClass] ?? 0;
          const id = `allocation-${assetClass.toLowerCase()}`;
          return (
            <fieldset className="allocation-field" key={assetClass} style={{ '--asset-color': ASSET_CLASS_META[assetClass].color } as React.CSSProperties}>
              <legend>{ASSET_CLASS_META[assetClass].label}</legend>
              <p>{ASSET_CLASS_META[assetClass].description}</p>
              <div className="allocation-input-row">
                <input
                  id={id}
                  className="allocation-range"
                  type="range"
                  min={0}
                  max={BASIS_POINTS_TOTAL}
                  step={1}
                  value={basisPoints}
                  disabled={disabled || isSubmitting}
                  aria-label={`Meta de ${ASSET_CLASS_META[assetClass].label}`}
                  onChange={(event) => {
                    setValue(`targets.${assetClass}`, Number(event.target.value), { shouldDirty: true, shouldValidate: true });
                  }}
                />
                <label className="allocation-number" htmlFor={`${id}-number`}>
                  <span className="sr-only">Percentagem de {ASSET_CLASS_META[assetClass].label}</span>
                  <input
                    id={`${id}-number`}
                    type="number"
                    min={0}
                    max={100}
                    step={0.01}
                    inputMode="decimal"
                    value={(basisPoints / 100).toFixed(2)}
                    disabled={disabled || isSubmitting}
                    onChange={(event) => {
                      const percent = Number(event.target.value);
                      const next = Number.isFinite(percent) ? Math.round(Math.min(100, Math.max(0, percent)) * 100) : 0;
                      setValue(`targets.${assetClass}`, next, { shouldDirty: true, shouldValidate: true });
                    }}
                  />
                  <span>%</span>
                </label>
              </div>
              {currentValues && (
                <small>
                  Atual: {((currentValues[assetClass] ?? 0) * 100).toFixed(2).replace('.', ',')}% · desvio:{' '}
                  {(((currentValues[assetClass] ?? 0) - basisPointsToWeight(basisPoints)) * 100).toFixed(2).replace('.', ',')} p.p.
                </small>
              )}
            </fieldset>
          );
        })}

        {errors.targets?.message && <p className="inline-error" role="alert">{errors.targets.message}</p>}
        <div className="investment-form-actions">
          <button type="submit" disabled={disabled || isSubmitting || !balanced || (!isDirty && targets.length > 0)}>
            {isSubmitting ? 'A guardar…' : submitLabel}
          </button>
        </div>
      </div>
    </form>
  );
}
