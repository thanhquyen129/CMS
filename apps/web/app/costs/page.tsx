import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CostListWorkspace } from "@/components/CostListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listCosts } from "@/lib/costs-revenues-server";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{
  maturity?: string;
  attribution?: string;
  page?: string;
  pageSize?: string;
}>;

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

  const sp = await searchParams;
  const maturityFilter =
    sp.maturity === "expected" ||
    sp.maturity === "confirmed" ||
    sp.maturity === "actual"
      ? sp.maturity
      : undefined;
  const attributionFilter =
    sp.attribution === "direct" || sp.attribution === "shared"
      ? sp.attribution
      : undefined;

  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const billLabel = term(terms, "BILL", "Bill");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");
  const directLabel = term(terms, "ATTRIBUTION_DIRECT", "Trực tiếp");

  const [result, kpiRes] = await Promise.all([
    listCosts({
      financialMaturity: maturityFilter,
      attributionType: attributionFilter,
    }),
    listCosts({ attributionType: attributionFilter }),
  ]);

  const kpiItems = kpiRes.ok ? kpiRes.data : [];
  const sumByMaturity = (m: string) =>
    kpiItems
      .filter((c) => c.financialMaturity?.toLowerCase() === m)
      .reduce((s, c) => s + (c.amount ?? 0), 0);
  const kpiCurrency = kpiItems[0]?.currencyCode ?? "VND";
  const sumExpected = sumByMaturity("expected");
  const sumConfirmed = sumByMaturity("confirmed");
  const sumActual = sumByMaturity("actual");
  const sumAll = sumExpected + sumConfirmed + sumActual;

  const allRows = result.ok ? result.data : [];
  const pageSize = parsePageSize(sp.pageSize);
  const pages = calcTotalPages(allRows.length, pageSize);
  const page = parsePage(sp.page, pages);
  const pageRows = slicePage(allRows, page, pageSize);
  const pageParams = {
    maturity: maturityFilter,
    attribution: attributionFilter,
  };

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          {costLabel}
        </p>
        <div className="page-header-row">
          <div>
            <h1>Danh sách {costLabel.toLowerCase()}</h1>
            <p className="lede">
              Vòng đời {costLabel.toLowerCase()}: {expectedLabel} → {confirmedLabel} →{" "}
              {actualLabel}. Phân bổ giữ tổng; không ghi đè độ chín. Chọn dòng để xem
              panel chi tiết.
            </p>
          </div>
          <Link className="btn" href="/costs/shared/new">
            + Tạo {costLabel.toLowerCase()} {sharedLabel.toLowerCase()}
          </Link>
        </div>

        {kpiRes.ok ? (
          <div className="stat-grid" style={{ marginTop: "0.85rem" }}>
            <div className="stat-card">
              <span className="stat-label">Tổng {costLabel.toLowerCase()}</span>
              <strong className="stat-value">
                {formatMoney(sumAll, kpiCurrency)}
              </strong>
              <span className="stat-hint">{kpiItems.length} dòng</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">{expectedLabel}</span>
              <strong className="stat-value">
                {formatMoney(sumExpected, kpiCurrency)}
              </strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{confirmedLabel}</span>
              <strong className="stat-value">
                {formatMoney(sumConfirmed, kpiCurrency)}
              </strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{actualLabel}</span>
              <strong className="stat-value">
                {formatMoney(sumActual, kpiCurrency)}
              </strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{billLabel}</span>
              <strong className="stat-value">
                <Link className="row-link" href="/bills">
                  Ghi trực tiếp →
                </Link>
              </strong>
            </div>
          </div>
        ) : null}

        <p className="cta-row" style={{ marginTop: "0.75rem" }}>
          <Link className="btn btn-ghost" href="/bills">
            Ghi {costLabel.toLowerCase()} trên {billLabel}
          </Link>
          <Link className="btn btn-ghost" href="/costs/shared">
            {costLabel} {sharedLabel.toLowerCase()}
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
            href={costsHref({
              maturity: "expected",
              attribution: attributionFilter,
            })}
          >
            {expectedLabel}
          </Link>
          <Link
            className={maturityFilter === "confirmed" ? "active" : undefined}
            href={costsHref({
              maturity: "confirmed",
              attribution: attributionFilter,
            })}
          >
            {confirmedLabel}
          </Link>
          <Link
            className={maturityFilter === "actual" ? "active" : undefined}
            href={costsHref({
              maturity: "actual",
              attribution: attributionFilter,
            })}
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
        ) : allRows.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {costLabel.toLowerCase()} trong phạm vi lọc. Mở một {billLabel} để
            ghi chi phí trực tiếp, hoặc tạo chi phí chung.
          </div>
        ) : (
          <>
            <CostListWorkspace
              terms={terms}
              costs={pageRows}
              billLabel={billLabel}
              costLabel={costLabel}
              expectedLabel={expectedLabel}
              confirmedLabel={confirmedLabel}
              actualLabel={actualLabel}
              directLabel={directLabel}
              sharedLabel={sharedLabel}
            />
            <ListPagination
              basePath="/costs"
              params={pageParams}
              page={page}
              pageSize={pageSize}
              totalCount={allRows.length}
              totalPages={pages}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
