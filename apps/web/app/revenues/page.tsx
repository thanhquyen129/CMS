import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listRevenues } from "@/lib/costs-revenues-server";
import { maturityLabelKey } from "@/lib/costs-revenues";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type SearchParams = Promise<{ maturity?: string }>;

function revenuesHref(maturity?: string): string {
  if (!maturity) return "/revenues";
  return `/revenues?maturity=${encodeURIComponent(maturity)}`;
}

export default async function RevenuesPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { maturity } = await searchParams;
  const maturityFilter =
    maturity === "expected" || maturity === "confirmed" || maturity === "actual"
      ? maturity
      : undefined;

  const terms = await fetchTerminology();
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const billLabel = term(terms, "BILL", "Bill");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");

  const result = await listRevenues({ financialMaturity: maturityFilter });

  return (
    <AppShell terms={terms} active="revenues">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          {revenueLabel} &amp; {profitLabel}
        </p>
        <h1>
          {revenueLabel} &amp; {profitLabel}
        </h1>
        <p className="lede">
          {revenueLabel} ≠ hóa đơn / AR / thu tiền. {profitLabel} suy ra từ{" "}
          {term(terms, "COST", "Chi phí")} và {revenueLabel} theo {billLabel} — xem hồ sơ
          tài chính Bill để drill-down.
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn" href="/bills">
            Mở {billLabel} để ghi {revenueLabel.toLowerCase()}
          </Link>
          <Link className="btn btn-ghost" href="/reports">
            Báo cáo &amp; Phân tích
          </Link>
        </p>

        <div className="filter-tabs" role="tablist" aria-label="Lọc độ chín">
          <Link className={!maturityFilter ? "active" : undefined} href={revenuesHref()}>
            Tất cả độ chín
          </Link>
          <Link
            className={maturityFilter === "expected" ? "active" : undefined}
            href={revenuesHref("expected")}
          >
            {expectedLabel}
          </Link>
          <Link
            className={maturityFilter === "confirmed" ? "active" : undefined}
            href={revenuesHref("confirmed")}
          >
            {confirmedLabel}
          </Link>
          <Link
            className={maturityFilter === "actual" ? "active" : undefined}
            href={revenuesHref("actual")}
          >
            {actualLabel}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có dòng {revenueLabel.toLowerCase()}. Mở một {billLabel} và thêm doanh thu
            (Dự kiến → Đã xác nhận → Thực tế).
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Loại</th>
                  <th scope="col">{billLabel}</th>
                  <th scope="col">Độ chín</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col">Hiệu lực</th>
                  <th scope="col">
                    <span className="sr-only">Mở</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((r) => {
                  const maturityVi = term(
                    terms,
                    maturityLabelKey(r.financialMaturity),
                    r.financialMaturity === "expected"
                      ? expectedLabel
                      : r.financialMaturity === "confirmed"
                        ? confirmedLabel
                        : actualLabel
                  );
                  return (
                    <tr key={r.id}>
                      <td>
                        <Link className="row-link" href={`/revenues/${r.id}`}>
                          {r.revenueTypeCode || r.id.slice(0, 8)}
                        </Link>
                      </td>
                      <td>
                        <Link className="row-link" href={`/bills/${r.billId}`}>
                          {r.billId.slice(0, 8)}…
                        </Link>
                      </td>
                      <td>
                        <span
                          className={`maturity-pill maturity-${r.financialMaturity?.toLowerCase()}`}
                        >
                          {maturityVi}
                        </span>
                      </td>
                      <td className="num">
                        {formatMoney(r.amount, r.currencyCode)}
                      </td>
                      <td>{formatDateTimeVi(r.effectiveDate)}</td>
                      <td>
                        <Link className="row-link" href={`/revenues/${r.id}`}>
                          Mở
                        </Link>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}
