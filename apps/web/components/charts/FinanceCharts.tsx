/** Lightweight SVG finance charts — no chart-lib dependency. Server-friendly. */

export type ChartSegment = {
  key: string;
  label: string;
  value: number;
  color: string;
};

function formatCompact(n: number): string {
  const abs = Math.abs(n);
  if (abs >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
  if (abs >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
  if (Number.isInteger(n)) return String(n);
  return n.toFixed(0);
}

/** Grouped vertical bars — Cost / Revenue / Profit (or any series). */
export function GroupedBarChart({
  caption,
  series,
  valueFormatter = formatCompact,
  height = 200,
}: {
  caption: string;
  series: ChartSegment[];
  valueFormatter?: (n: number) => string;
  height?: number;
}) {
  const width = 420;
  const padL = 44;
  const padR = 16;
  const padT = 28;
  const padB = 36;
  const plotW = width - padL - padR;
  const plotH = height - padT - padB;
  const maxAbs = Math.max(1, ...series.map((s) => Math.abs(s.value)));
  const barW = Math.min(56, (plotW / Math.max(series.length, 1)) * 0.55);
  const gap = plotW / Math.max(series.length, 1);

  return (
    <figure className="fin-chart">
      <figcaption className="fin-chart-caption">{caption}</figcaption>
      <svg
        className="fin-chart-svg"
        viewBox={`0 0 ${width} ${height}`}
        role="img"
        aria-label={caption}
      >
        <line
          x1={padL}
          y1={padT + plotH}
          x2={width - padR}
          y2={padT + plotH}
          className="fin-chart-axis"
        />
        {series.map((s, i) => {
          const cx = padL + gap * i + gap / 2;
          const h = (Math.abs(s.value) / maxAbs) * (plotH * 0.92);
          const y = s.value >= 0 ? padT + plotH - h : padT + plotH;
          return (
            <g key={s.key}>
              <rect
                x={cx - barW / 2}
                y={y}
                width={barW}
                height={Math.max(h, s.value === 0 ? 0 : 2)}
                rx={3}
                fill={s.color}
              />
              <text
                x={cx}
                y={y - 6}
                textAnchor="middle"
                className="fin-chart-val"
              >
                {valueFormatter(s.value)}
              </text>
              <text
                x={cx}
                y={height - 12}
                textAnchor="middle"
                className="fin-chart-label"
              >
                {s.label}
              </text>
            </g>
          );
        })}
      </svg>
      <ul className="fin-chart-legend" aria-hidden="true">
        {series.map((s) => (
          <li key={s.key}>
            <span className="fin-swatch" style={{ background: s.color }} />
            {s.label}
          </li>
        ))}
      </ul>
    </figure>
  );
}

/** Horizontal bars — workload / aging buckets. */
export function HorizontalBarChart({
  caption,
  series,
  valueFormatter = formatCompact,
  emptyLabel = "Chưa có dữ liệu để vẽ.",
}: {
  caption: string;
  series: ChartSegment[];
  valueFormatter?: (n: number) => string;
  emptyLabel?: string;
}) {
  const max = Math.max(1, ...series.map((s) => Math.abs(s.value)));
  const hasData = series.some((s) => s.value !== 0);

  return (
    <figure className="fin-chart">
      <figcaption className="fin-chart-caption">{caption}</figcaption>
      {!hasData ? (
        <p className="fin-chart-empty" role="status">
          {emptyLabel}
        </p>
      ) : (
        <ul className="fin-hbar-list" aria-label={caption}>
          {series.map((s) => {
            const pct = Math.max(0, (Math.abs(s.value) / max) * 100);
            return (
              <li key={s.key} className="fin-hbar-row">
                <span className="fin-hbar-label">{s.label}</span>
                <span className="fin-hbar-track" aria-hidden="true">
                  <span
                    className="fin-hbar-fill"
                    style={{ width: `${pct}%`, background: s.color }}
                  />
                </span>
                <span className="fin-hbar-val">{valueFormatter(s.value)}</span>
              </li>
            );
          })}
        </ul>
      )}
    </figure>
  );
}

/** Single stacked row — maturity Expected | Confirmed | Actual. */
export function StackedCompositionBar({
  caption,
  segments,
  emptyLabel = "Chưa có dòng để phân tách.",
}: {
  caption: string;
  segments: ChartSegment[];
  emptyLabel?: string;
}) {
  const total = segments.reduce((s, x) => s + Math.max(0, x.value), 0);

  return (
    <figure className="fin-chart fin-chart-stack">
      <figcaption className="fin-chart-caption">{caption}</figcaption>
      {total === 0 ? (
        <p className="fin-chart-empty" role="status">
          {emptyLabel}
        </p>
      ) : (
        <>
          <div
            className="fin-stack-track"
            role="img"
            aria-label={`${caption}: tổng ${total}`}
          >
            {segments.map((s) => {
              if (s.value <= 0) return null;
              const pct = (s.value / total) * 100;
              return (
                <span
                  key={s.key}
                  className="fin-stack-seg"
                  style={{ width: `${pct}%`, background: s.color }}
                  title={`${s.label}: ${s.value}`}
                />
              );
            })}
          </div>
          <ul className="fin-chart-legend">
            {segments.map((s) => (
              <li key={s.key}>
                <span className="fin-swatch" style={{ background: s.color }} />
                {s.label}: <strong>{s.value}</strong>
              </li>
            ))}
          </ul>
        </>
      )}
    </figure>
  );
}

/** Document / cash funnel — decreasing steps (visual only, honest counts). */
export function FunnelSteps({
  caption,
  steps,
}: {
  caption: string;
  steps: ChartSegment[];
}) {
  const max = Math.max(1, ...steps.map((s) => s.value));

  return (
    <figure className="fin-chart">
      <figcaption className="fin-chart-caption">{caption}</figcaption>
      <ol className="fin-funnel" aria-label={caption}>
        {steps.map((s, i) => {
          const pct = 42 + (Math.abs(s.value) / max) * 58;
          return (
            <li key={s.key} className="fin-funnel-step">
              <span
                className="fin-funnel-bar"
                style={{
                  width: `${pct}%`,
                  background: s.color,
                  opacity: 1 - i * 0.08,
                }}
              >
                <span className="fin-funnel-label">{s.label}</span>
                <span className="fin-funnel-val">{s.value}</span>
              </span>
            </li>
          );
        })}
      </ol>
    </figure>
  );
}

/** Shared finance palette (Ledger / Harbor-safe). */
export const FinColors = {
  cost: "#5c6b7a",
  revenue: "#0f6b58",
  profit: "#15202b",
  profitNeg: "#a61b1b",
  expected: "#94a3b8",
  confirmed: "#0f6b58",
  actual: "#1a7a5c",
  work: "#0f6b58",
  workWarn: "#9a5b12",
  workDanger: "#a61b1b",
  workMuted: "#5c6b7a",
  doc1: "#64748b",
  doc2: "#0f6b58",
  doc3: "#c47a2a",
  agingCurrent: "#1a7a5c",
  agingMid: "#9a5b12",
  agingLate: "#a61b1b",
  agingNone: "#94a3b8",
} as const;
