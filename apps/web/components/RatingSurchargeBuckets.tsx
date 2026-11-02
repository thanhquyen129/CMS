import { formatMoney } from "@/lib/money";

type ChargeRow = {
  code?: string;
  versionNo?: number;
  versionId?: string;
  amount?: number;
  currency?: string;
  formula?: string;
  activationPolicy?: string;
  evidence?: string | null;
  reasonCode?: string;
  reasonMessageVi?: string;
  quantity?: number;
  basis?: string | null;
};

type ContextBuckets = {
  appliedCharges?: ChargeRow[];
  eligibleNotActivatedCharges?: ChargeRow[];
  excludedCharges?: ChargeRow[];
  unpricedServices?: string[];
  unclassifiedWarnings?: string[];
};

function policyLabel(policy?: string) {
  switch ((policy ?? "").toUpperCase()) {
    case "MANDATORY_BY_POLICY":
      return "Bắt buộc theo chính sách";
    case "AUTO_CONDITIONAL":
      return "Tự động theo dữ liệu";
    case "OPTIONAL_OPT_IN":
      return "Tùy chọn có xác nhận";
    case "UNCLASSIFIED_LEGACY":
      return "Chưa phân loại (legacy)";
    default:
      return policy || "—";
  }
}

function parseContext(contextJson?: string | null): ContextBuckets {
  if (!contextJson) return {};
  try {
    return JSON.parse(contextJson) as ContextBuckets;
  } catch {
    return {};
  }
}

export function RatingSurchargeBuckets({
  contextJson,
  currencyCode,
}: {
  contextJson?: string | null;
  currencyCode: string;
}) {
  const ctx = parseContext(contextJson);
  const applied = ctx.appliedCharges ?? [];
  const eligible = ctx.eligibleNotActivatedCharges ?? [];
  const excluded = ctx.excludedCharges ?? [];
  const unpriced = ctx.unpricedServices ?? [];
  const unclassified = ctx.unclassifiedWarnings ?? [];

  if (applied.length === 0 && eligible.length === 0 && excluded.length === 0 && unpriced.length === 0) {
    return null;
  }

  return (
    <div className="stack" style={{ marginTop: "1rem" }} id="surcharge-buckets">
      <h4 className="section-title sm">Kết quả phụ phí (3 nhóm)</h4>
      {unpriced.length > 0 ? (
        <div className="alert alert-warning" role="status">
          <strong>UNPRICED</strong> — Dịch vụ đã chọn nhưng chưa có biểu giá phụ phí ở chiều này:{" "}
          {unpriced.join("; ")}
        </div>
      ) : null}
      {unclassified.length > 0 ? (
        <div className="alert alert-warning" role="status">
          Cảnh báo quản trị UNCLASSIFIED: {unclassified.join(", ")}
        </div>
      ) : null}

      <BucketTable
        title="1. Đã áp dụng (Applied)"
        empty="Không có phụ phí đã kích hoạt."
        rows={applied}
        currencyCode={currencyCode}
        showAmount
      />
      <BucketTable
        title="2. Có giá nhưng chưa phát sinh (Eligible but not activated)"
        empty="Không có phụ phí khớp giá nhưng chưa kích hoạt."
        rows={eligible}
        currencyCode={currencyCode}
      />
      <BucketTable
        title="3. Không đủ điều kiện / Bị chặn (Excluded)"
        empty="Không có phụ phí bị loại trừ trong lần tính này."
        rows={excluded}
        currencyCode={currencyCode}
      />
    </div>
  );
}

function BucketTable({
  title,
  empty,
  rows,
  currencyCode,
  showAmount = false,
}: {
  title: string;
  empty: string;
  rows: ChargeRow[];
  currencyCode: string;
  showAmount?: boolean;
}) {
  return (
    <div className="panel" style={{ padding: "12px", marginBottom: "8px" }}>
      <h5 style={{ margin: "0 0 8px" }}>{title}</h5>
      {rows.length === 0 ? (
        <p className="muted">{empty}</p>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Mã / Phiên bản</th>
                <th>Chính sách kích hoạt</th>
                <th>Lý do / Bằng chứng</th>
                {showAmount ? <th className="num">Số tiền</th> : null}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, idx) => (
                <tr key={`${row.code}-${row.versionId ?? idx}`}>
                  <td>
                    <code>{row.code}</code>
                    {row.versionNo != null ? ` · v${row.versionNo}` : ""}
                    {row.formula ? <span className="muted small block">{row.formula}</span> : null}
                    {row.quantity != null && showAmount ? (
                      <span className="muted small block">
                        qty {row.quantity}{row.basis ? ` · ${row.basis}` : ""}
                      </span>
                    ) : null}
                  </td>
                  <td>{policyLabel(row.activationPolicy)}</td>
                  <td>
                    {row.reasonCode ? <code>{row.reasonCode}</code> : null}
                    {row.reasonMessageVi ? (
                      <span className="muted small block">{row.reasonMessageVi}</span>
                    ) : null}
                    {row.evidence ? (
                      <span className="muted small block">evidence: {row.evidence}</span>
                    ) : null}
                  </td>
                  {showAmount ? (
                    <td className="num">
                      <strong>{formatMoney(row.amount ?? 0, row.currency || currencyCode)}</strong>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
