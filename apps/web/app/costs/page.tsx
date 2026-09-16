import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listCosts,
} from "@/lib/costs-revenues-server";
import {
  isSharedCost,
  maturityLabelKey,
} from "@/lib/costs-revenues";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type SearchParams = Promise<{ maturity?: string; attribution?: string }>;

function costsHref(opts: { maturity?: string; attribution?: string }): string {
  const p = new URLSearchParams();
  if (opts.maturity) p.set("maturity", opts.maturity);
  if (opts.attribution) p.set("attribution", opts.attribution);
  const qs = p.toString();
  return qs ? `/costs?${qs}` : "/costs";
}

export default async function CostsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { maturity, attribution } = await searchParams;
  const maturityFilter =
    maturity === "expected" || maturity === "confirmed" || maturity === "actual"
      ? maturity
      : undefined;
  const attributionFilter =
    attribution === "direct" || attribution === "shared" ? attribution : undefined;

  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const billLabel = term(terms, "BILL", "Bill");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");
  const directLabel = term(terms, "ATTRIBUTION_DIRECT", "Trực tiếp");

  const result = await listCosts({
    financialMaturity: maturityFilter,
    attributionType: attributionFilter,
  });

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          {costLabel}
        </p>
        <h1>Quản lý {costLabel}</h1>
        <p className="lede">
          Vòng đời {costLabel.toLowerCase()}: {expectedLabel} → {confirmedLabel} →{" "}
          {actualLabel}. Phân bổ giữ tổng; không ghi đè độ chín.
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn" href="/costs/shared/new">
            Tạo {costLabel.toLowerCase()} {sharedLabel.toLowerCase()}
          </Link>
          <Link className="btn btn-ghost" href="/bills">
            Ghi {costLabel.toLowerCase()} trên {billLabel}
          </Link>
        </p>

        <div className="filter-tabs" role="tablist" aria-label="Lọc độ chín">
          <Link
            className={!maturityFilter ? "active" : undefined}
            href={costsHref({ attribution: attributionFilter })}
          >
            Tất cả độ chín
          </Link>
          <Link
            className={maturityFilter === "expected" ? "active" : undefined}
            href={costsHref({ maturity: "expected", attribution: attributionFilter })}
          >
            {expectedLabel}
          </Link>
          <Link
            className={maturityFilter === "confirmed" ? "active" : undefined}
            href={costsHref({ maturity: "confirmed", attribution: attributionFilter })}
          >
            {confirmedLabel}
          </Link>
          <Link
            className={maturityFilter === "actual" ? "active" : undefined}
            href={costsHref({ maturity: "actual", attribution: attributionFilter })}
          >
            {actualLabel}
          </Link>
        </div>

        <div className="filter-tabs" role="tablist" aria-label="Lọc nguồn">
          <Link
            className={!attributionFilter ? "active" : undefined}
            href={costsHref({ maturity: maturityFilter })}
          >
            Mọi nguồn
          </Link>
          <Link
            className={attributionFilter === "direct" ? "active" : undefined}
            href={costsHref({ maturity: maturityFilter, attribution: "direct" })}
          >
            {directLabel}
          </Link>
          <Link
            className={attributionFilter === "shared" ? "active" : undefined}
            href={costsHref({ maturity: maturityFilter, attribution: "shared" })}
          >
            {sharedLabel}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {costLabel.toLowerCase()} trong phạm vi lọc. Mở một {billLabel} để ghi
            chi phí trực tiếp, hoặc tạo chi phí chung.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Mã</th>
                  <th scope="col">{billLabel}</th>
                  <th scope="col">Nguồn</th>
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
                {result.data.map((c) => {
                  const maturityVi = term(
                    terms,
                    maturityLabelKey(c.financialMaturity),
                    c.financialMaturity === "expected"
                      ? expectedLabel
                      : c.financialMaturity === "confirmed"
                        ? confirmedLabel
                        : actualLabel
                  );
                  const href = isSharedCost(c.attributionType)
                    ? `/costs/shared/${c.id}`
                    : `/costs/${c.id}`;
                  return (
                    <tr key={c.id}>
                      <td>
                        <Link className="row-link" href={href}>
                          {c.costTypeCode || c.id.slice(0, 8)}
                        </Link>
                      </td>
                      <td>
                        {c.billId ? (
                          <Link className="row-link" href={`/bills/${c.billId}`}>
                            {c.billId.slice(0, 8)}…
                          </Link>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td>
                        {isSharedCost(c.attributionType) ? sharedLabel : directLabel}
                      </td>
                      <td>
                        <span className={`maturity-pill maturity-${c.financialMaturity?.toLowerCase()}`}>
                          {maturityVi}
                        </span>
                      </td>
                      <td className="num">
                        {formatMoney(c.amount, c.currencyCode)}
                      </td>
                      <td>{formatDateTimeVi(c.effectiveDate)}</td>
                      <td>
                        <Link className="row-link" href={href}>
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
