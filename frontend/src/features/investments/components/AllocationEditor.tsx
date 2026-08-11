import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { ASSET_CLASS_META, ASSET_CLASSES, PERCENT_TOTAL } from '../constants';
import { allocationSchema, type AllocationFormValues } from '../schemas';
import type { AllocationTargetDto, AssetClass } from '../types';
import { allocationToPercentages, percentageToWeight } from '../utils';
import { AllocationDonut } from './AllocationDonut';

interface AllocationEditorProps {
  targets: AllocationTargetDto[];
  currentValues?: Partial<Record<AssetClass, number>>;
  disabled?: boolean;
  allowUnchangedSubmit?: boolean;
  submitLabel?: string;
  onSubmit: (values: Record<AssetClass, number>) => Promise<void> | void;
}

export function AllocationEditor({
  targets,
  currentValues,
  disabled,
  allowUnchangedSubmit = false,
  submitLabel = 'Guardar alocação',
  onSubmit
}: AllocationEditorProps) {
  const initialTargets = allocationToPercentages(targets);
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
    reset({ targets: allocationToPercentages(targets) });
  }, [reset, targets]);

  const values = watch('targets');
  const total = ASSET_CLASSES.reduce((sum, assetClass) => sum + (values?.[assetClass] ?? 0), 0);
  const balanced = total === PERCENT_TOTAL;
  const donutValues = Object.fromEntries(
    ASSET_CLASSES.map((assetClass) => [assetClass, percentageToWeight(values?.[assetClass] ?? 0)])
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
          <strong>{total}%</strong>
          <small>{balanced ? 'Pronto para guardar' : total < PERCENT_TOTAL ? `Faltam ${PERCENT_TOTAL - total} pontos percentuais` : `Reduza ${total - PERCENT_TOTAL} pontos percentuais`}</small>
        </div>
        <AllocationDonut values={donutValues} centerLabel="Meta" centerValue={`${total}%`} title="Metas de alocação" />
      </div>

      <div className="allocation-editor-fields">
        <header>
          <div>
            <h2>Defina as cinco metas</h2>
            <p>Arraste os controles em números inteiros. A soma precisa ser exatamente 100%.</p>
          </div>
        </header>

        {ASSET_CLASSES.map((assetClass) => {
          const percent = values?.[assetClass] ?? 0;
          const id = `allocation-${assetClass.toLowerCase()}`;
          const sliderStyle = {
            '--target-color': ASSET_CLASS_META[assetClass].color,
            '--target-progress': `${percent}%`
          } as React.CSSProperties;
          return (
            <fieldset className="allocation-field" key={assetClass} style={{ '--asset-color': ASSET_CLASS_META[assetClass].color } as React.CSSProperties}>
              <legend>{ASSET_CLASS_META[assetClass].label}</legend>
              <p>{ASSET_CLASS_META[assetClass].description}</p>
              <div className="allocation-slider">
                <strong className="allocation-slider-value" aria-live="polite">{percent}%</strong>
                <input
                  id={id}
                  className="allocation-range target-slider"
                  type="range"
                  min={0}
                  max={PERCENT_TOTAL}
                  step={1}
                  value={percent}
                  style={sliderStyle}
                  disabled={disabled || isSubmitting}
                  aria-label={`Meta de ${ASSET_CLASS_META[assetClass].label}`}
                  onChange={(event) => {
                    const next = Math.min(PERCENT_TOTAL, Math.max(0, Math.round(Number(event.target.value))));
                    setValue(`targets.${assetClass}`, next, { shouldDirty: true, shouldValidate: true });
                  }}
                />
                <span className="target-slider-scale" aria-hidden="true"><span>0%</span><span>100%</span></span>
              </div>
              {currentValues && (
                <small>
                  Atual: {((currentValues[assetClass] ?? 0) * 100).toFixed(2).replace('.', ',')}% · desvio:{' '}
                  {(((currentValues[assetClass] ?? 0) - percentageToWeight(percent)) * 100).toFixed(2).replace('.', ',')} p.p.
                </small>
              )}
            </fieldset>
          );
        })}

        {errors.targets?.message && <p className="inline-error" role="alert">{errors.targets.message}</p>}
        <div className="investment-form-actions">
          <button type="submit" disabled={disabled || isSubmitting || !balanced || (!allowUnchangedSubmit && !isDirty && targets.length > 0)}>
            {isSubmitting ? 'A guardar…' : submitLabel}
          </button>
        </div>
      </div>
    </form>
  );
}
