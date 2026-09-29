import Link from "next/link";
import { basisLabel, formatChargeable, type RatingMissingField, type RatingReadiness } from "@/lib/opref-edit";

function actionLink(item: RatingMissingField, editAnchor: string) {
  switch (item.action) {
    case "edit_bill":
      return (
        <a className="row-link" href={`#${editAnchor}`}>
          Sửa thông tin Bill
        </a>
      );
    case "rate_card":
      return (
        <Link className="row-link" href="/rate-cards">
          Kiểm tra bảng giá
        </Link>
      );
    default:
      return <span className="muted small">Bổ sung ở form tính giá</span>;
  }
}

export function MissingFieldList({ missing, editAnchor }: { missing: RatingMissingField[]; editAnchor: string }) {
  return (
    <ul className="readiness-list">
      {missing.map((m) => (
        <li key={`${m.field}-${m.ruleCodes.join(",")}`}>
          <strong>{m.label}</strong> — {m.message.replace(/^[A-Z_]+:\s*/, "")} {actionLink(m, editAnchor)}
        </li>
      ))}
    </ul>
  );
}

/** Readiness pre-check result (ADR-0039 D03): lists missing fields and the rule needing each. */
export function RatingReadinessBox({
  readiness,
  error,
  checking,
  editAnchor,
}: {
  readiness: RatingReadiness | null;
  error: string | null;
  checking: boolean;
  editAnchor: string;
}) {
  if (error) {
    return (
      <div className="alert alert-error" role="alert">
        {error}
      </div>
    );
  }
  if (!readiness) {
    return checking ? (
      <p className="muted small" role="status">
        Đang kiểm tra dữ liệu tính giá…
      </p>
    ) : null;
  }
  if (!readiness.ready) {
    return (
      <div className="alert alert-warning" role="status">
        <strong>Chưa đủ dữ liệu để tính giá</strong>
        <MissingFieldList missing={readiness.missing} editAnchor={editAnchor} />
      </div>
    );
  }
  return (
    <div className="alert alert-info" role="status">
      <strong>Đủ dữ liệu để tính giá</strong>
      {checking ? <span className="muted small"> · đang kiểm tra lại…</span> : null}
      <div className="muted small">
        {readiness.quantityBasis === "not_required"
          ? "Quy tắc áp dụng không cần Trọng lượng tính cước."
          : `Khối tính cước: ${formatChargeable(readiness.quantity, readiness.quantityUom)} · ${basisLabel(readiness.quantityBasis)}`}
        {readiness.quantityRuleCode ? ` (${readiness.quantityRuleCode})` : ""}
        {readiness.ruleCodes.length > 0 ? ` · Quy tắc: ${readiness.ruleCodes.join(", ")}` : ""}
      </div>
    </div>
  );
}
