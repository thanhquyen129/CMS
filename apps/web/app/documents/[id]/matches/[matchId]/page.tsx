import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import {
  AddMatchDetailForm,
  type MatchSourceLineOption,
  type MatchTargetOption,
} from "@/components/AddMatchDetailForm";
import { CancelDocumentMatchButton } from "@/components/CancelDocumentMatchButton";
import { ConfirmDocumentMatchButton } from "@/components/ConfirmDocumentMatchButton";
import { CreateExposuresFromMatchButton } from "@/components/CreateExposuresFromMatchButton";
import { MatchSuggestionsPanel } from "@/components/MatchSuggestionsPanel";
import { ReverseMatchDetailButton } from "@/components/ReverseMatchDetailButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { maturityLabelKey } from "@/lib/costs-revenues";
import {
  listCostsByBill,
  listRevenuesByBill,
} from "@/lib/costs-revenues-server";
import {
  detailStatusLabel,
  isActiveDetail,
  isDraftMatch,
  matchMethodLabel,
  matchStatusLabel,
} from "@/lib/document-matches";
import { getDocumentMatch } from "@/lib/document-matches-server";
import {
  getFinancialDocument,
  listFinancialDocuments,
  type FinancialDocumentListItem,
} from "@/lib/documents";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Params = Promise<{ id: string; matchId: string }>;
type SearchParams = Promise<{ targetDocumentId?: string }>;

export default async function DocumentMatchSessionPage({
  params,
  searchParams,
}: {
  params: Params;
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id, matchId } = await params;
  const sp = await searchParams;
  const terms = await fetchTerminology();
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const matchLabel = term(terms, "MATCHED", "Khớp");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const billLabel = term(terms, "BILL", "Bill");

  const [docResult, matchResult] = await Promise.all([
    getFinancialDocument(id),
    getDocumentMatch(matchId),
  ]);

  if (!docResult.ok) {
    return (
      <AppShell terms={terms} active="documents">
        <section className="panel">
          <h1>
            {docResult.status === 404
              ? `Không tìm thấy ${docLabel.toLowerCase()}`
              : matchLabel}
          </h1>
          <div className="alert alert-error" role="alert">
            {docResult.message}
          </div>
          <Link className="btn" href="/documents">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  if (!matchResult.ok) {
    return (
      <AppShell terms={terms} active="documents">
        <section className="panel">
          <h1>
            {matchResult.status === 404
              ? "Không tìm thấy phiên khớp"
              : matchLabel}
          </h1>
          <div className="alert alert-error" role="alert">
            {matchResult.message}
          </div>
          <Link className="btn" href={`/documents/${id}`}>
            Quay lại chứng từ
          </Link>
        </section>
      </AppShell>
    );
  }

  const doc = docResult.data;
  const match = matchResult.data;

  if (
    match.primaryDocumentId &&
    match.primaryDocumentId.toLowerCase() !== id.toLowerCase()
  ) {
    return (
      <AppShell terms={terms} active="documents">
        <section className="panel">
          <h1>Phiên khớp không thuộc chứng từ này</h1>
          <p className="lede">
            Primary document lệch URL. Mở đúng chứng từ neo phiên.
          </p>
          <Link
            className="btn"
            href={`/documents/${match.primaryDocumentId}/matches/${match.id}`}
          >
            Mở đúng chứng từ
          </Link>
        </section>
      </AppShell>
    );
  }

  const draft = isDraftMatch(match.matchStatus);
  const method = match.matchMethod?.toLowerCase() ?? "";
  const currency = doc.currencyCode || "VND";

  const sourceLines: MatchSourceLineOption[] = doc.lines
    .filter((l) => Number(l.openAmount) > 0)
    .map((l) => ({
      id: l.id,
      label: `#${l.lineNo} · ${l.description || "—"} · mở ${formatMoney(l.openAmount, l.currencyCode)}`,
      openAmount: l.openAmount,
      currencyCode: l.currencyCode,
    }));

  let targets: MatchTargetOption[] = [];
  let targetDocHint: string | null = null;
  let acceptedDocs: FinancialDocumentListItem[] = [];
  const targetDocumentId = sp.targetDocumentId?.trim() || "";

  if (method === "line_to_cost") {
    if (!doc.billId) {
      targetDocHint = `Chứng từ chưa gắn ${billLabel} — không tải được danh sách ${costLabel.toLowerCase()}.`;
    } else {
      const costs = await listCostsByBill(doc.billId);
      if (costs.ok) {
        targets = costs.data
          .filter((c) => c.recordStatus?.toLowerCase() === "active")
          .map((c) => ({
            id: c.id,
            label: `${c.costTypeCode || costLabel} · ${formatMoney(c.amount, c.currencyCode)} · ${term(terms, maturityLabelKey(c.financialMaturity), c.financialMaturity)}`,
          }));
      } else {
        targetDocHint = costs.message;
      }
    }
  } else if (method === "line_to_revenue") {
    if (!doc.billId) {
      targetDocHint = `Chứng từ chưa gắn ${billLabel} — không tải được danh sách ${revenueLabel.toLowerCase()}.`;
    } else {
      const revs = await listRevenuesByBill(doc.billId);
      if (revs.ok) {
        targets = revs.data
          .filter((r) => r.recordStatus?.toLowerCase() === "active")
          .map((r) => ({
            id: r.id,
            label: `${r.revenueTypeCode || revenueLabel} · ${formatMoney(r.amount, r.currencyCode)} · ${term(terms, maturityLabelKey(r.financialMaturity), r.financialMaturity)}`,
          }));
      } else {
        targetDocHint = revs.message;
      }
    }
  } else if (method === "line_to_line") {
    const others = await listFinancialDocuments({
      acceptanceStatus: "accepted",
    });
    if (others.ok) {
      acceptedDocs = others.data.items.filter((d) => d.id !== doc.id);
    } else {
      targetDocHint = others.message;
    }

    if (targetDocumentId) {
      const targetDoc = await getFinancialDocument(targetDocumentId);
      if (!targetDoc.ok) {
        targetDocHint = targetDoc.message;
      } else if (targetDoc.data.id === doc.id) {
        targetDocHint = "Chọn chứng từ đích khác chứng từ nguồn.";
      } else {
        targets = targetDoc.data.lines
          .filter((l) => Number(l.openAmount) > 0)
          .map((l) => ({
            id: l.id,
            label: `${targetDoc.data.documentNo} #${l.lineNo} · mở ${formatMoney(l.openAmount, l.currencyCode)}`,
          }));
        if (targets.length === 0) {
          targetDocHint = `Chứng từ ${targetDoc.data.documentNo} không còn dòng mở.`;
        }
      }
    } else if (!targetDocHint) {
      targetDocHint =
        acceptedDocs.length === 0
          ? "Chưa có chứng từ đã chấp nhận khác để chọn dòng đích."
          : "Chọn chứng từ đích bên dưới rồi tải dòng.";
    }
  }

  const activeDetailCount = match.details.filter((d) =>
    isActiveDetail(d.detailStatus)
  ).length;
  const confirmed =
    match.matchStatus?.toLowerCase() === "confirmed";
  const canConfirm = draft && activeDetailCount > 0;
  const canReverseDetail = draft || confirmed;
  const canCancel =
    (draft || confirmed) && activeDetailCount === 0;
  const lineById = new Map(doc.lines.map((l) => [l.id, l]));

  return (
    <AppShell
      terms={terms}
      active="documents"
      topbarRight={
        <Link className="btn btn-ghost btn-sm" href={`/documents/${id}`}>
          ← {doc.documentNo}
        </Link>
      }
    >
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/documents">{docLabel}</Link>
          <span aria-hidden="true"> / </span>
          <Link href={`/documents/${id}`}>{doc.documentNo}</Link>
          <span aria-hidden="true"> / </span>
          <span>Phiên {matchLabel.toLowerCase()}</span>
        </p>
        <h1>
          Phiên {matchLabel.toLowerCase()} v{match.versionNo}
        </h1>
        <p className="lede meta-line">
          {matchMethodLabel(terms, match.matchMethod)} ·{" "}
          {matchStatusLabel(terms, match.matchStatus)} · Dung sai{" "}
          {formatMoney(match.toleranceAmount, currency)} /{" "}
          {match.tolerancePercent}%
        </p>
        {match.notes ? <p className="note">Ghi chú: {match.notes}</p> : null}

        <h2 className="section-title">Chi tiết khớp</h2>
        {match.details.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có chi tiết. Thêm liên kết bên dưới (hành động chính khi phiên
            nháp).
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Nguồn</th>
                  <th scope="col">Đích</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {match.details.map((d) => {
                  const src = lineById.get(d.sourceLineId);
                  const target = d.targetCostId
                    ? `${costLabel} ${d.targetCostId.slice(0, 8)}…`
                    : d.targetRevenueId
                      ? `${revenueLabel} ${d.targetRevenueId.slice(0, 8)}…`
                      : d.targetLineId
                        ? `Dòng ${d.targetLineId.slice(0, 8)}…`
                        : "—";
                  return (
                    <tr key={d.id}>
                      <td>
                        {src
                          ? `#${src.lineNo} ${src.description || ""}`
                          : `${d.sourceLineId.slice(0, 8)}…`}
                      </td>
                      <td>{target}</td>
                      <td className="num">
                        {formatMoney(d.matchedAmount, currency)}
                      </td>
                      <td>
                        {detailStatusLabel(terms, d.detailStatus)}
                        {d.reversedAt ? (
                          <span className="muted small">
                            {" "}
                            · {formatDateTimeVi(d.reversedAt)}
                          </span>
                        ) : null}
                        {d.reverseReason ? (
                          <div className="muted small">{d.reverseReason}</div>
                        ) : null}
                      </td>
                      <td>
                        {canReverseDetail && isActiveDetail(d.detailStatus) ? (
                          <ReverseMatchDetailButton
                            terms={terms}
                            matchId={match.id}
                            detailId={d.id}
                            matchedAmount={d.matchedAmount}
                            currencyCode={currency}
                          />
                        ) : (
                          "—"
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        <MatchSuggestionsPanel matchId={match.id} draft={draft} />

        <CreateExposuresFromMatchButton matchId={match.id} documentId={doc.id} />

        {draft ? (
          <>
            <h2 className="section-title">Thêm chi tiết khớp</h2>
            {method === "line_to_line" ? (
              <form className="receive-form" method="get">
                <div className="form-grid">
                  <div className="field field-span">
                    <label htmlFor="targetDocumentId">
                      {docLabel} đích (đã chấp nhận)
                    </label>
                    {acceptedDocs.length > 0 ? (
                      <select
                        id="targetDocumentId"
                        name="targetDocumentId"
                        defaultValue={targetDocumentId}
                        required={!targetDocumentId}
                      >
                        <option value="">— Chọn chứng từ —</option>
                        {acceptedDocs.map((d) => (
                          <option key={d.id} value={d.id}>
                            {d.documentNo} · {d.documentType} ·{" "}
                            {formatMoney(d.totalAmount, d.currencyCode)}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        id="targetDocumentId"
                        name="targetDocumentId"
                        type="text"
                        defaultValue={targetDocumentId}
                        placeholder="UUID chứng từ đích"
                      />
                    )}
                  </div>
                </div>
                <div className="cta-row">
                  <button type="submit" className="btn btn-ghost">
                    Tải dòng đích
                  </button>
                </div>
              </form>
            ) : null}
            <AddMatchDetailForm
              terms={terms}
              matchId={match.id}
              matchMethod={match.matchMethod}
              sourceLines={sourceLines}
              targets={targets}
              targetDocHint={targetDocHint}
              documentId={doc.id}
            />
          </>
        ) : (
          <p className="note">
            Phiên không còn nháp — không thêm chi tiết từ UI này.
            {confirmed
              ? " Có thể đảo chi tiết rồi hủy phiên nếu cần."
              : null}
            {match.cancelReason ? ` Lý do hủy: ${match.cancelReason}.` : null}
          </p>
        )}

        <div className="cta-row" style={{ marginTop: "1.25rem" }}>
          <ConfirmDocumentMatchButton
            terms={terms}
            matchId={match.id}
            canConfirm={canConfirm}
            rowVersion={match.rowVersion}
          />
          <CancelDocumentMatchButton
            terms={terms}
            matchId={match.id}
            documentId={doc.id}
            canCancel={canCancel}
          />
          <Link className="btn btn-ghost" href={`/documents/${id}`}>
            Xem chứng từ (trạng thái khớp)
          </Link>
        </div>
      </section>
    </AppShell>
  );
}
