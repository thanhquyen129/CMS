import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { DocumentAcceptButton } from "@/components/DocumentAcceptButton";
import { DocumentStatusTriad } from "@/components/DocumentStatusTriad";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  canAcceptDocument,
  directionLabel,
  documentTypeLabel,
  getFinancialDocument,
  recordStatusLabel,
} from "@/lib/documents";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

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

  const result = await getFinancialDocument(id);

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
                  Mở {billLabel}
                </Link>
              ) : (
                "—"
              )}
            </dd>
          </div>
        </dl>

        {doc.notes ? <p className="note">Ghi chú: {doc.notes}</p> : null}

        <div className="cta-row">
          {canAccept ? (
            <DocumentAcceptButton
              terms={terms}
              documentId={doc.id}
              documentNo={doc.documentNo}
              totalAmount={doc.totalAmount}
              currencyCode={doc.currencyCode}
              canAccept
            />
          ) : (
            <p className="muted small">
              {doc.acceptanceStatus?.toLowerCase() === "accepted"
                ? `Đã chấp nhận. Khớp chứng từ (${matchedLabel}) chưa có trên UI U4 — follow-up.`
                : doc.receiptStatus?.toLowerCase() !== "received"
                  ? "Chỉ chấp nhận được khi đã nhận."
                  : "Không thể chấp nhận ở trạng thái hiện tại."}
            </p>
          )}
        </div>

        <h2 className="section-title">{lineLabel}</h2>
        {doc.lines.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có dòng chứng từ. (Thêm dòng qua API — không bắt buộc để chấp
            nhận.)
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">#</th>
                  <th scope="col">Mô tả</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col" className="num">
                    Đã khớp
                  </th>
                  <th scope="col" className="num">
                    {outstandingLabel} (mở)
                  </th>
                </tr>
              </thead>
              <tbody>
                {doc.lines.map((line) => (
                  <tr key={line.id}>
                    <td>{line.lineNo}</td>
                    <td>{line.description || "—"}</td>
                    <td className="num">
                      {formatMoney(line.amount, line.currencyCode)}
                    </td>
                    <td className="num">
                      {formatMoney(line.matchedAmount, line.currencyCode)}
                    </td>
                    <td className="num">
                      {formatMoney(line.openAmount, line.currencyCode)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}
