import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AddDocumentLineForm } from "@/components/AddDocumentLineForm";
import { AppShell } from "@/components/AppShell";
import { CorrectDocumentHeaderForm } from "@/components/CorrectDocumentHeaderForm";
import { DocumentAcceptButton } from "@/components/DocumentAcceptButton";
import { DocumentStatusTriad } from "@/components/DocumentStatusTriad";
import { EditDocumentLineForm } from "@/components/EditDocumentLineForm";
import { VoidDocumentButton } from "@/components/VoidDocumentButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { canStartMatch } from "@/lib/document-matches";
import {
  canAcceptDocument,
  canAddDocumentLine,
  canMutateDocumentLine,
  directionLabel,
  documentLineCoverage,
  documentTypeLabel,
  getFinancialDocument,
  recordStatusLabel,
} from "@/lib/documents";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { listBusinessParties, partyLabel } from "@/lib/parties";

type Params = Promise<{ id: string }>;

export default async function DocumentDetailPage({
  params,
}: {
  params: Params;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const lineLabel = term(terms, "FINANCIAL_DOCUMENT_LINE", "Dòng chứng từ");
  const billLabel = term(terms, "BILL", "Bill");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");
  const outstandingLabel = term(terms, "OUTSTANDING", "Số dư còn lại");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  const result = await getFinancialDocument(id);
  const partiesResult = await listBusinessParties();
  const parties = partiesResult.ok ? partiesResult.data : [];
  const partyById = new Map(parties.map((p) => [p.id, p]));

  if (!result.ok && result.status === 404) {
    return (
      <AppShell terms={terms} active="documents">
        <section className="panel">
          <h1>Không tìm thấy {docLabel.toLowerCase()}</h1>
          <p className="lede">{result.message}</p>
          <Link className="btn" href="/documents">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  if (!result.ok) {
    return (
      <AppShell terms={terms} active="documents">
        <section className="panel">
          <h1>{docLabel}</h1>
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
          <Link className="btn btn-ghost" href="/documents">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  const doc = result.data;
  const canAccept = canAcceptDocument(doc);
  const canMatch = canStartMatch(doc);
  const canAddLine = canAddDocumentLine(doc);
  const coverage = documentLineCoverage(doc);
  const remainingTowardTotal = Math.max(0, coverage.remainingTowardTotal);
  const acceptBlockedBySum = canAccept && !coverage.sumsEqual;
  const canCorrectHeader =
    doc.recordStatus?.toLowerCase() === "active" &&
    doc.receiptStatus?.toLowerCase() === "received" &&
    doc.acceptanceStatus?.toLowerCase() === "not_accepted" &&
    doc.matchingStatus?.toLowerCase() === "unmatched";
  const counterparty = doc.counterpartyId
    ? partyById.get(doc.counterpartyId)
    : null;

  return (
    <AppShell
      terms={terms}
      active="documents"
      topbarRight={
        <Link className="btn btn-ghost btn-sm" href="/documents">
          ← Danh sách
        </Link>
      }
    >
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/documents">{docLabel}</Link>
          <span aria-hidden="true"> / </span>
          <span>{doc.documentNo}</span>
        </p>
        <h1>
          {docLabel} {doc.documentNo}
        </h1>
        <p className="lede meta-line">
          {documentTypeLabel(doc.documentType)} ·{" "}
          {directionLabel(terms, doc.direction)} ·{" "}
          {formatMoney(doc.totalAmount, doc.currencyCode)} · Ngày{" "}
          {doc.documentDate} · {recordStatusLabel(terms, doc.recordStatus)}
        </p>

        <h2 className="section-title">Ba chiều trạng thái</h2>
        <p className="note">
          Nhận ≠ Chấp nhận ≠ Khớp — độc lập. Chứng từ ≠ {costLabel}; chấp nhận ≠{" "}
          {paymentLabel}.
        </p>
        <DocumentStatusTriad
          terms={terms}
          receiptStatus={doc.receiptStatus}
          acceptanceStatus={doc.acceptanceStatus}
          matchingStatus={doc.matchingStatus}
        />

        <dl className="metric-grid" style={{ marginTop: "1rem" }}>
          <div>
            <dt>Nhận lúc</dt>
            <dd>{doc.receivedAt ? formatDateTimeVi(doc.receivedAt) : "—"}</dd>
          </div>
          <div>
            <dt>Chấp nhận lúc</dt>
            <dd>{doc.acceptedAt ? formatDateTimeVi(doc.acceptedAt) : "—"}</dd>
          </div>
          <div>
            <dt>{billLabel}</dt>
            <dd>
              {doc.billId ? (
                <Link className="row-link" href={`/bills/${doc.billId}`}>
                  {doc.billNo || billLabel}
                </Link>
              ) : (
                "—"
              )}
            </dd>
          </div>
          <div>
            <dt>Đối tác</dt>
            <dd>
              {counterparty
                ? partyLabel(counterparty)
                : doc.counterpartyId
                  ? doc.counterpartyId
                  : "—"}
            </dd>
          </div>
        </dl>

        {doc.notes ? <p className="note">Ghi chú: {doc.notes}</p> : null}

        <div className="cta-row">
          {canAccept && !acceptBlockedBySum ? (
            <DocumentAcceptButton
              terms={terms}
              documentId={doc.id}
              documentNo={doc.documentNo}
              totalAmount={doc.totalAmount}
              linesSum={coverage.linesSum}
              currencyCode={doc.currencyCode}
              canAccept
            />
          ) : null}
          {acceptBlockedBySum ? (
            <p className="note" style={{ margin: 0 }}>
              Chấp nhận yêu cầu tổng dòng = tổng chứng từ (ADR-0012). Hiện{" "}
              {formatMoney(coverage.linesSum, doc.currencyCode)} ≠{" "}
              {formatMoney(doc.totalAmount, doc.currencyCode)} — thêm/sửa dòng
              bên dưới.
            </p>
          ) : null}
          {canMatch ? (
            <Link className="btn" href={`/documents/${doc.id}/match`}>
              Khớp chứng từ
            </Link>
          ) : null}
          {canCorrectHeader ? (
            <>
              <CorrectDocumentHeaderForm
                documentId={doc.id}
                currencyCode={doc.currencyCode}
                billId={doc.billId}
                billNo={doc.billNo}
                billLabel={billLabel}
              />
              <VoidDocumentButton documentId={doc.id} />
            </>
          ) : null}
          {!canAccept && !canMatch && !acceptBlockedBySum ? (
            <p className="muted small">
              {doc.acceptanceStatus?.toLowerCase() === "accepted"
                ? doc.lines.every((l) => Number(l.openAmount) <= 0)
                  ? `Đã ${matchedLabel.toLowerCase()} hết số mở — hoặc chưa có dòng mở.`
                  : "Không mở khớp được ở trạng thái hiện tại."
                : doc.receiptStatus?.toLowerCase() !== "received"
                  ? "Chỉ chấp nhận / khớp được khi đã nhận."
                  : "Chấp nhận chứng từ trước khi khớp (Nhận ≠ Chấp nhận ≠ Khớp)."}
            </p>
          ) : null}
        </div>

        <h2 className="section-title">{lineLabel}</h2>
        <dl className="metric-grid" style={{ marginBottom: "1rem" }}>
          <div>
            <dt>Tổng chứng từ</dt>
            <dd>{formatMoney(doc.totalAmount, doc.currencyCode)}</dd>
          </div>
          <div>
            <dt>Tổng dòng</dt>
            <dd>{formatMoney(coverage.linesSum, doc.currencyCode)}</dd>
          </div>
          <div>
            <dt>Còn theo header</dt>
            <dd>
              {coverage.remainingTowardTotal < -0.0000001 ? (
                <span className="neg">
                  {formatMoney(coverage.remainingTowardTotal, doc.currencyCode)}
                </span>
              ) : (
                formatMoney(
                  Math.max(0, coverage.remainingTowardTotal),
                  doc.currencyCode
                )
              )}
            </dd>
          </div>
          <div>
            <dt>Tổng số mở</dt>
            <dd>{formatMoney(coverage.openSum, doc.currencyCode)}</dd>
          </div>
        </dl>
        {Math.abs(coverage.remainingTowardTotal) > 0.0000001 ? (
          <p className="note">
            Tổng dòng {coverage.remainingTowardTotal > 0 ? "thấp hơn" : "cao hơn"}{" "}
            tổng chứng từ — Accept yêu cầu bằng nhau (ADR-0012). Draft: không
            vượt tổng.
          </p>
        ) : (
          <p className="note">Tổng dòng khớp tổng chứng từ — có thể chấp nhận.</p>
        )}

        {doc.lines.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {lineLabel.toLowerCase()}. Cần đủ tổng dòng = tổng chứng từ
            trước khi chấp nhận; cần dòng mở để khớp.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">#</th>
                  <th scope="col">Mô tả</th>
                  <th scope="col">Loại</th>
                  <th scope="col">{billLabel}</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col" className="num">
                    Đã khớp
                  </th>
                  <th scope="col" className="num">
                    {outstandingLabel} (mở)
                  </th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {doc.lines.map((line) => {
                  const canMutate = canMutateDocumentLine(doc, line);
                  return (
                    <tr key={line.id}>
                      <td>{line.lineNo}</td>
                      <td>{line.description || "—"}</td>
                      <td>
                        {line.costTypeCode
                          ? `${costLabel}: ${line.costTypeCode}`
                          : line.revenueTypeCode
                            ? `${revenueLabel}: ${line.revenueTypeCode}`
                            : "—"}
                      </td>
                      <td>
                        {line.billId ? (
                          <Link
                            className="row-link"
                            href={`/bills/${line.billId}`}
                          >
                            Mở
                          </Link>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td className="num">
                        {formatMoney(line.amount, line.currencyCode)}
                      </td>
                      <td className="num">
                        {formatMoney(line.matchedAmount, line.currencyCode)}
                      </td>
                      <td className="num">
                        {formatMoney(line.openAmount, line.currencyCode)}
                      </td>
                      <td>
                        {canMutate ? (
                          <EditDocumentLineForm
                            terms={terms}
                            documentId={doc.id}
                            lineId={line.id}
                            lineNo={line.lineNo}
                            currencyCode={line.currencyCode}
                            amount={line.amount}
                            description={line.description}
                            billId={line.billId}
                            costTypeCode={line.costTypeCode}
                            revenueTypeCode={line.revenueTypeCode}
                            direction={doc.direction}
                            documentTotal={doc.totalAmount}
                            otherLinesSum={
                              coverage.linesSum - Number(line.amount)
                            }
                            canDelete
                          />
                        ) : (
                          <span className="muted small">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {canAddLine ? (
          <>
            <h2 className="section-title">Thêm {lineLabel.toLowerCase()}</h2>
            <AddDocumentLineForm
              terms={terms}
              documentId={doc.id}
              currencyCode={doc.currencyCode}
              documentTotal={doc.totalAmount}
              linesSum={coverage.linesSum}
              defaultAmount={remainingTowardTotal}
              defaultBillId={doc.billId}
              defaultBillNo={doc.billNo}
              direction={doc.direction}
              formKey={`${doc.lines.length}-${remainingTowardTotal}`}
            />
          </>
        ) : (
          <p className="muted small" style={{ marginTop: "1rem" }}>
            {doc.recordStatus?.toLowerCase() !== "active"
              ? "Chứng từ không còn hiệu lực — không thêm dòng."
              : doc.acceptanceStatus?.toLowerCase() === "accepted"
                ? "Đã chấp nhận — không thêm/xóa/đổi số dòng (ADR-0012)."
                : "Chỉ thêm dòng khi chứng từ đã nhận."}
          </p>
        )}
      </section>
    </AppShell>
  );
}
