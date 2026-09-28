import { formatMoney } from "@/lib/money";

type Props = {
  label?: string;
  before: number;
  /** Null when the input is not a valid amount yet. */
  after: number | null;
  currencyCode: string;
};

/** Before → After preview on dialogs that change an AR/AP balance. */
export function BalanceImpact({ label = "Số dư công nợ", before, after, currencyCode }: Props) {
  const invalid = after != null && after < -0.0000001;
  return (
    <div className={invalid ? "balance-impact is-invalid" : "balance-impact"} aria-live="polite">
      <span className="balance-impact-label">{label}</span>
      <span className="balance-impact-value">{formatMoney(before, currencyCode)}</span>
      <span className="balance-impact-arrow" aria-hidden="true">
        →
      </span>
      <span className="sr-only">sau giao dịch</span>
      <span className="balance-impact-value">
        {after == null ? "—" : formatMoney(after, currencyCode)}
      </span>
    </div>
  );
}
