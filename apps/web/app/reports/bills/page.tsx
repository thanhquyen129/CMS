import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listBillProfit } from "@/lib/costs-revenues-server";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ view?: string; reportingCurrency?: string; asOf?: string }>;

const views = [
  ["expected", "Dự kiến"],
  ["confirmed", "Đã xác nhận"],
  ["actual", "Thực tế"],
  ["best", "Giá trị tốt nhất"],
] as const;

const currencies = ["", "VND", "USD"] as const;

export default async function BillProfitReportPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const view = views.some((v) => v[0] === sp.view) ? sp.view! : "best";
  const reportingRaw = (sp.reportingCurrency ?? "VND").trim().toUpperCase();
  const reportingCurrency = reportingRaw === "NATIVE" ? "" : reportingRaw;
  const asOf = sp.asOf?.trim() || "";
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const rows = await listBillProfit({ view, page: 1, pageSize: 50 });

  function href(next: { view?: string; reportingCurrency?: string }) {
    const params = new URLSearchParams();
    params.set("view", next.view ?? view);
    const ccy =
      next.reportingCurrency !== undefined ? next.reportingCurrency : reportingCurrency;
    if (ccy) params.set("reportingCurrency", ccy);
    if (asOf) params.set("asOf", asOf);
    return `/reports/bills?${params.toString()}`;
  }

  return (
    <AppShell terms={terms} active="reports">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/reports">Báo cáo</Link>
          <span aria-hidden="true"> / </span>
          <span>Lợi nhuận theo {billLabel}</span>
        </p>
        <h1>Lợi nhuận theo {billLabel}</h1>
        <p className="lede">
          Maturity {views.find((v) => v[0] === view)?.[1]}. Tiền tệ báo cáo{" "}
          {reportingCurrency}. Mỗi dòng giữ tiền gốc; mở hồ sơ {billLabel} để quy đổi và đối chiếu.
          {asOf ? ` Mốc as-of ${asOf} được giữ khi mở hồ sơ.` : ""}
        </p>
        <p className="cta-row">
          {views.map(([id, label]) => (
            <Link
              key={id}
              className={id === view ? "btn btn-sm" : "btn btn-sm btn-ghost"}
              href={href({ view: id })}
            >
              {label}
            </Link>
          ))}
        </p>
        <p className="cta-row">
          {currencies.map((code) => {
            const label = code ? `Báo cáo ${code}` : "Theo tiền gốc";
            const active = code ? reportingCurrency === code : reportingCurrency === "";
            return (
              <Link
                key={label}
                className={active ? "btn btn-sm" : "btn btn-sm btn-ghost"}
                href={href({ reportingCurrency: code || "native" })}
              >
                {label}
              </Link>
            );
          })}
        </p>
        {!rows.ok ? (
          <div className="alert alert-error" role="alert">
            {rows.message}
          </div>
        ) : rows.data.items.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {billLabel} để tính {profitLabel.toLowerCase()}.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">{billLabel}</th>
                  <th scope="col">Khách</th>
                  <th scope="col">Tiền gốc</th>
                  <th scope="col" className="num">
                    {profitLabel}
                  </th>
                  <th scope="col">Hồ sơ</th>
                </tr>
              </thead>
              <tbody>
                {rows.data.items.map((row) => {
                  const q = new URLSearchParams({ view });
                  if (reportingCurrency) q.set("reportingCurrency", reportingCurrency);
                  if (asOf) q.set("asOf", asOf);
                  return (
                    <tr key={row.billId}>
                      <td>{row.billNo}</td>
                      <td>{row.customerName || "—"}</td>
                      <td>
                        {row.currencyCode}
                        {row.hasMixedCurrencies ? " · nhiều tiền tệ" : ""}
                      </td>
                      <td className={`num ${row.profitAmount < 0 ? "neg" : ""}`}>
                        {formatMoney(row.profitAmount, row.currencyCode)}
                      </td>
                      <td>
                        <Link href={`/bills/${row.billId}?${q.toString()}`}>
                          Đối chiếu
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
