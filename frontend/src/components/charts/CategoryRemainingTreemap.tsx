import { ResponsiveContainer, Tooltip, Treemap } from 'recharts';
import type { DashboardCategoryPointDto } from '../../api/types';
import { formatCurrency } from '../../utils/format';

const COLORS = ['#2463eb', '#0f766e', '#f59e0b', '#ef4444', '#6366f1', '#0891b2'];

interface CategoryRemainingTreemapProps {
  data: DashboardCategoryPointDto[];
}

interface CategoryTreemapCellProps {
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  depth?: number;
  index?: number;
  category?: string;
  name?: string;
  value?: number;
}

function chunkWord(word: string, maxCharsPerLine: number): string[] {
  if (word.length <= maxCharsPerLine) {
    return [word];
  }

  const chunks: string[] = [];
  let remaining = word;

  while (remaining.length > maxCharsPerLine) {
    chunks.push(remaining.slice(0, maxCharsPerLine));
    remaining = remaining.slice(maxCharsPerLine);
  }

  if (remaining) {
    chunks.push(remaining);
  }

  return chunks;
}

function wrapLabel(label: string, maxCharsPerLine: number, maxLines: number): string[] {
  const words = label.split(' ').flatMap((word) => chunkWord(word, maxCharsPerLine));
  const lines: string[] = [];
  let currentLine = '';

  for (const word of words) {
    if (!currentLine) {
      currentLine = word;
      continue;
    }

    if (`${currentLine} ${word}`.length <= maxCharsPerLine) {
      currentLine = `${currentLine} ${word}`;
      continue;
    }

    lines.push(currentLine);
    currentLine = word;

    if (lines.length === maxLines) {
      break;
    }
  }

  if (currentLine && lines.length < maxLines) {
    lines.push(currentLine);
  }

  if (lines.length === 0) {
    return [label];
  }

  if (words.join(' ').length > lines.join(' ').length) {
    const lastLine = lines[lines.length - 1];
    lines[lines.length - 1] = lastLine.length > maxCharsPerLine - 1
      ? `${lastLine.slice(0, Math.max(1, maxCharsPerLine - 2))}…`
      : `${lastLine}…`;
  }

  return lines;
}

function CategoryTreemapCell({
  x = 0,
  y = 0,
  width = 0,
  height = 0,
  depth = 0,
  index = 0,
  category,
  name = '',
  value = 0
}: CategoryTreemapCellProps) {
  if (depth < 1 || width <= 0 || height <= 0) {
    return null;
  }

  const fill = COLORS[index % COLORS.length];
  const label = category ?? name;
  const showValue = width > 92 && height > 64;
  const showLabel = width > 38 && height > 26;
  const reservedTopSpace = showValue ? 34 : 0;
  const availableHeight = Math.max(16, height - reservedTopSpace - 10);
  const maxLabelLines = Math.max(1, Math.min(3, Math.floor(availableHeight / 13)));
  const labelLines = wrapLabel(
    label,
    Math.max(4, Math.floor((width - 16) / 7)),
    maxLabelLines
  );
  const labelFontSize = width < 56 ? 10 : 11;
  const labelLineHeight = labelFontSize + 2;
  const valueY = y + 24;
  const labelX = x + width / 2;
  const labelAreaCenterY = y + reservedTopSpace + (height - reservedTopSpace) / 2;
  const labelStartY = labelAreaCenterY - ((labelLines.length - 1) * labelLineHeight) / 2;
  const labelStroke = 'rgba(12, 23, 49, 0.18)';

  return (
    <g>
      <rect
        x={x + 2}
        y={y + 2}
        width={Math.max(0, width - 4)}
        height={Math.max(0, height - 4)}
        rx={12}
        ry={12}
        fill={fill}
        stroke="#f8fbff"
        strokeWidth={2}
      />
      {showLabel && (
        <text
          x={labelX}
          y={labelStartY}
          fill="#ffffff"
          fontSize={labelFontSize}
          fontWeight={600}
          textAnchor="middle"
          stroke={labelStroke}
          strokeWidth={2}
          paintOrder="stroke"
        >
          {labelLines.map((line, lineIndex) => (
            <tspan key={`${label}-${lineIndex}`} x={labelX} dy={lineIndex === 0 ? 0 : labelLineHeight}>
              {line}
            </tspan>
          ))}
        </text>
      )}
      {showValue && (
        <text x={x + 14} y={valueY} fill="#ffffff" fontSize={14} fontWeight={700}>
          {formatCurrency(value)}
        </text>
      )}
    </g>
  );
}

export function CategoryRemainingTreemap({ data }: CategoryRemainingTreemapProps) {
  const chartData = [...data].sort((left, right) => right.remaining - left.remaining);
  const chartHeight = Math.max(360, Math.ceil(chartData.length / 3) * 120);

  if (chartData.length === 0) {
    return (
      <section className="chart-card">
        <h3>Saldo disponível por categoria</h3>
        <p className="chart-empty">Nenhuma categoria com saldo disponível neste mês.</p>
      </section>
    );
  }

  return (
    <section className="chart-card">
      <h3>Saldo disponível por categoria</h3>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height={chartHeight}>
          <Treemap
            data={chartData}
            dataKey="remaining"
            nameKey="category"
            aspectRatio={4 / 3}
            stroke="#f8fbff"
            isAnimationActive={false}
            content={<CategoryTreemapCell />}
          >
            <Tooltip
              formatter={(value: number) => [formatCurrency(value), 'Saldo disponível']}
              labelFormatter={(_, payload) => payload?.[0]?.payload?.category ?? ''}
              contentStyle={{
                borderRadius: 12,
                border: '1px solid #d9e2f4',
                backgroundColor: '#ffffff'
              }}
            />
          </Treemap>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
