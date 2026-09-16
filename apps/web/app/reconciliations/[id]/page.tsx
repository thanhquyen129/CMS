import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AddReconciliationDetailForm } from "@/components/AddReconciliationDetailForm";
import { CompleteReconciliationButton } from "@/components/CompleteReconciliationButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  canEditReconciliation,
  getReconciliation,
  lineStatusLabel,
  reconObjectTypeLabel,
  reconciliationStatusLabel,
  reconciliationTypeLabel,
} from "@/lib/reconciliations";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Ctx = { params: Promise<{ id: string }> };

export default async function ReconciliationDetailPage({ params }: Ctx) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  const detailLabel = term(terms, "RECONCILIATION_DETAIL", "Chi tiết đối soát");
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");
  const billLabel = term(terms, "BILL", "Bill");

  const result = await getReconciliation(id);

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/reconciliations">{reconLabel}</Link>
          {" / "}
          Chi tiết
        </p>

        {!result.ok ? (
          <>
            <h1>{reconLabel}</h1>
            <div className="alert alert-error" role="alert">
              {result.message}
            </div>
          </>
        ) : (
          <>
            <h1>
              {reconciliationTypeLabel(terms, result.data.reconciliationType)}
            </h1>
            <p className="lede">
              Trạng thái:{" "}
              {reconciliationStatusLabel(terms, result.data.status)} · v
              {result.data.versionNo}
              {result.data.ruleCode ? ` · ${result.data.ruleCode}` : ""}
            </p>
            <p className="meta-line muted">
              {result.data.startedAt
                ? `Bắt đầu: ${formatDateTimeVi(result.data.startedAt)}`
                : "Chưa bắt đầu"}
              {result.data.completedAt
                ? ` · Hoàn tất: ${formatDateTimeVi(result.data.completedAt)}`
                : ""}
            </p>
            {result.data.notes ? (
              <p className="note">{result.data.notes}</p>
            ) : null}

            <div className="cta-row">
              {result.data.billId ? (
                <Link className="btn btn-ghost" href={`/bills/${result.data.billId}`}>
                  Mở {billLabel}
                </Link>
              ) : null}
              <Link className="btn btn-ghost" href="/bank-feed">
                {term(terms, "BANK_FEED", "Sao kê ngân hàng")}
              </Link>
              {canEditReconciliation(result.data.status) ? (
                <CompleteReconciliationButton
                  terms={terms}
                  reconciliationId={result.data.id}
                />
              ) : null}
            </div>

            <h2 className="section-title">{detailLabel}</h2>
            {result.data.details.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có dòng. Thêm chi tiết bên dưới.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Nguồn</th>
                      <th scope="col">Đích</th>
                      <th scope="col">Nguồn</th>
                      <th scope="col">Khớp</th>
                      <th scope="col">{varianceLabel}</th>
                      <th scope="col">Trạng thái dòng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.data.details.map((d) => (
                      <tr key={d.id}>
                        <td>
                          <div className="queue-title">
                            {reconObjectTypeLabel(terms, d.sourceType)}
                          </div>
                          <span className="muted small block mono-id">
                            {d.sourceId}
                          </span>
                        </td>
                        <td>
                          {d.targetType ? (
                            <>
                              <div className="queue-title">
                                {reconObjectTypeLabel(terms, d.targetType)}
                              </div>
                              <span className="muted small block mono-id">
                                {d.targetId}
                              </span>
                            </>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td>
                          {formatMoney(d.sourceAmount, d.currencyCode)}
                        </td>
                        <td>
                          {formatMoney(d.matchedAmount, d.currencyCode)}
                        </td>
                        <td>
                          {d.varianceAmount !== 0 ? (
                            <span className="stat-warn">
                              {formatMoney(d.varianceAmount, d.currencyCode)}
                            </span>
                          ) : (
                            formatMoney(0, d.currencyCode)
                          )}
                        </td>
                        <td>{lineStatusLabel(d.lineStatus)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {canEditReconciliation(result.data.status) ? (
              <>
                <h2 className="section-title">Thêm {detailLabel.toLowerCase()}</h2>
                <AddReconciliationDetailForm
                  terms={terms}
                  reconciliationId={result.data.id}
                />
              </>
            ) : (
              <p className="note muted">
                Phiên đã khóa — không thêm dòng.
              </p>
            )}
          </>
        )}
      </section>
    </AppShell>
  );
}
