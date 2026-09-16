import type { TerminologyMap } from "@/lib/terminology";
import { term } from "@/lib/terminology";
import {
  acceptanceStatusLabel,
  matchingStatusLabel,
  receiptStatusLabel,
} from "@/lib/documents-shared";

type Props = {
  terms: TerminologyMap;
  receiptStatus: string;
  acceptanceStatus: string;
  matchingStatus: string;
  compact?: boolean;
};

/** Three independent dimensions — never collapse Received ≠ Accepted ≠ Matched. */
export function DocumentStatusTriad({
  terms,
  receiptStatus,
  acceptanceStatus,
  matchingStatus,
  compact = false,
}: Props) {
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const acceptedLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");

  return (
    <div
      className={compact ? "status-triad compact" : "status-triad"}
      role="group"
      aria-label="Trạng thái chứng từ theo ba chiều độc lập"
    >
      <div className="status-dim">
        {!compact ? <span className="status-dim-label">{receivedLabel}</span> : null}
        <span
          className={`status-pill receipt-${receiptStatus?.toLowerCase() || "unknown"}`}
          title={receivedLabel}
        >
          {receiptStatusLabel(terms, receiptStatus)}
        </span>
      </div>
      <span className="status-neq" aria-hidden="true">
        ≠
      </span>
      <div className="status-dim">
        {!compact ? (
          <span className="status-dim-label">{acceptedLabel}</span>
        ) : null}
        <span
          className={`status-pill acceptance-${acceptanceStatus?.toLowerCase() || "unknown"}`}
          title={acceptedLabel}
        >
          {acceptanceStatusLabel(terms, acceptanceStatus)}
        </span>
      </div>
      <span className="status-neq" aria-hidden="true">
        ≠
      </span>
      <div className="status-dim">
        {!compact ? <span className="status-dim-label">{matchedLabel}</span> : null}
        <span
          className={`status-pill matching-${matchingStatus?.toLowerCase() || "unknown"}`}
          title={matchedLabel}
        >
          {matchingStatusLabel(terms, matchingStatus)}
        </span>
      </div>
    </div>
  );
}
