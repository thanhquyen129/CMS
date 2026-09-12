import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  agingBucketLabel,
  getAgingSummary,
  type AgingBucketSummary,
  type AgingReport,
} from "@/lib/ap-ar";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ asOf?: string }>;

function BucketTable({
  title,
  report,
  exportHref,
}: {
  title: string;
  report: AgingReport;
  exportHref: string;
}) {
  const buckets = report.buckets ?? [];
  const total = buckets.reduce((s, b) => s + b.outstanding, 0);
  const currencyGuess =
    report.payableItems?.[0]?.currencyCode ||
    report.receivableItems?.[0]?.currencyCode ||
    "VND";

  return (
    <div className="stack" style={{ marginTop: "1rem" }}>
      <div className="cta-row" style={{ marginTop: 0 }}>
        <h2 className="section-title sm" style={{ margin: 0, flex: 1 }}>
          {title}
        </h2>
        <a className="btn btn-ghost btn-sm" href={exportHref}>
          Xuất CSV
        </a>
      </div>
      {buckets.length === 0 ? (
        <div className="empty-state" role="status">
          Không có dòng outstanding trong phạm vi lọc.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Nhóm tuổi</th>
                <th scope="col" className="num">
                  Số dòng
                </th>
                <th scope="col" className="num">
                  Dư nợ
                </th>
              </tr>
            </thead>
            <tbody>
              {buckets.map((b: AgingBucketSummary) => (
                <tr key={b.bucket}>
                  <td>{agingBucketLabel(b.bucket)}</td>
                  <td className="num">{b.count}</td>
                  <td className="num">
                    {formatMoney(b.outstanding, currencyGuess)}
                  </td>
                </tr>
              ))}
              <tr>
                <th scope="row">Tổng</th>
                <td className="num">
                  {buckets.reduce((s, b) => s + b.count, 0)}
                </td>
                <td className="num">{formatMoney(total, currencyGuess)}</td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
      <p className="note muted small">
        Tổng theo tiền tệ gốc từng dòng — không cộng gộp chéo nếu nhiều loại tiền.
        Số trên là projection theo bucket; xem chi tiết trên sổ AP/AR.
      </p>
    </div>
  );
}

export default async function AgingSummaryPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const agingLabel = term(terms, "AGING", "Tuổi nợ");
  const asOfLabel = term(terms, "AS_OF", "Tại thời điểm");

  const summaryRes = await getAgingSummary({
    asOf: sp.asOf?.trim() || undefined,
  });

  return (
    <AppShell terms={terms} active="ap-ar">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/ap-ar">
            {apLabel} / {arLabel}
          </Link>
          <span aria-hidden="true"> / </span>
          <span>{agingLabel}</span>
        </p>
        <h1>
          Tóm tắt {agingLabel.toLowerCase()}
        </h1>
        <p className="lede">
          Nhóm bucket derived (ADR-0006): trong hạn / 1–30 / 31–60 / 61–90 / 90+.
          Quyền xem chi phí mở AP; quyền xem doanh thu mở AR — không suy ra biên từ
          một phía.
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn btn-ghost" href="/ap-ar">
            ← Sổ {apLabel} / {arLabel}
          </Link>
          <Link className="btn btn-ghost" href="/dashboard">
            Bảng điều khiển
          </Link>
        </p>

        {!summaryRes.ok ? (
          <div className="alert alert-error" role="alert">
            {summaryRes.message}
          </div>
        ) : (
          <>
            <p className="meta-line muted">
              {asOfLabel}: {summaryRes.data.asOf}
            </p>
            <p className="note">{summaryRes.data.note}</p>

            {!summaryRes.data.canViewPayable &&
            !summaryRes.data.canViewReceivable ? (
              <div className="empty-state" role="status">
                Không có quyền xem tuổi nợ. Cần <code>cost.read</code> và/hoặc{" "}
                <code>revenue.read</code>.
              </div>
            ) : null}

            {summaryRes.data.canViewPayable && summaryRes.data.payable ? (
              <BucketTable
                title={apLabel}
                report={summaryRes.data.payable}
                exportHref={`/bff/aging/export?side=payable&asOf=${encodeURIComponent(summaryRes.data.asOf)}`}
              />
            ) : null}

            {summaryRes.data.canViewReceivable && summaryRes.data.receivable ? (
              <BucketTable
                title={arLabel}
                report={summaryRes.data.receivable}
                exportHref={`/bff/aging/export?side=receivable&asOf=${encodeURIComponent(summaryRes.data.asOf)}`}
              />
            ) : null}
          </>
        )}
      </section>
    </AppShell>
  );
}
