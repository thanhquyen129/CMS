import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPagination } from "@/components/ListPagination";
import { RateCardListWorkspace } from "@/components/RateCardListWorkspace";
import {
  FilterBar,
  ListPageHeader,
  StatCardGrid,
} from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listRateCards } from "@/lib/rate-cards-server";
import {
  parsePage,
  parsePageSize,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";

type SearchParams = Promise<{
  partyType?: string;
  active?: string;
  q?: string;
  page?: string;
  pageSize?: string;
}>;

export default async function RateCardsPage({
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
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");
  const actual = term(terms, "ACTUAL", "Thực tế");

  const partyType =
    sp.partyType === "vendor" || sp.partyType === "customer"
      ? sp.partyType
      : undefined;
  const active =
    sp.active === "1" ? true : sp.active === "0" ? false : undefined;
  const q = sp.q?.trim() || undefined;

  const pageSize = parsePageSize(sp.pageSize);
  const pageHint = Math.max(
    1,
    Number.parseInt(String(sp.page ?? "1"), 10) || 1
  );

  const [listRes, kpiRes] = await Promise.all([
    listRateCards({ q, partyType, active, page: pageHint, pageSize }),
    listRateCards(),
  ]);

  const allCards = kpiRes.ok ? kpiRes.data.items : [];
  const activeCount = allCards.filter((c) => c.isActive).length;
  const buyCount = allCards.filter(
    (c) => c.partyType?.toLowerCase() === "vendor"
  ).length;
  const sellCount = allCards.filter(
    (c) => c.partyType?.toLowerCase() === "customer"
  ).length;

  const totalCount = listRes.ok ? listRes.data.totalCount : 0;
  const pages = calcTotalPages(totalCount, pageSize);
  const page = parsePage(sp.page, pages);
  const pageRows = listRes.ok ? listRes.data.items : [];

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Bảng giá & Tính giá" },
          ]}
          title="Danh sách bảng giá"
          lede={`Rate card → phiên bản → quy tắc → phát hành → tính giá trên ${billLabel} → seed ${costLabel.toLowerCase()} ${expected.toLowerCase()}. Rating tạo kỳ vọng tài chính, không tạo ${actual.toLowerCase()}.`}
          action={
            <Link className="btn" href="/rate-cards/new">
              + Tạo bảng giá
            </Link>
          }
        />

        {kpiRes.ok ? (
          <StatCardGrid
            cards={[
              { key: "total", label: "Tổng bảng giá", value: allCards.length },
              {
                key: "buy",
                label: "Giá mua (NCC)",
                value: buyCount,
                tone: "warning",
              },
              {
                key: "sell",
                label: "Giá bán (KH)",
                value: sellCount,
                tone: "success",
              },
              {
                key: "active",
                label: "Đang hiệu lực",
                value: activeCount,
                tone: "primary",
              },
            ]}
          />
        ) : null}

        <FilterBar
          action="/rate-cards"
          resetHref={sp.q || sp.partyType || sp.active ? "/rate-cards" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm bảng giá",
              placeholder: "Mã hoặc tên bảng giá…",
              defaultValue: sp.q,
            },
            {
              kind: "select",
              name: "partyType",
              label: "Loại giá",
              defaultValue: sp.partyType,
              emptyLabel: "Tất cả loại giá",
              options: [
                { value: "vendor", label: "Giá mua (NCC)" },
                { value: "customer", label: "Giá bán (KH)" },
              ],
            },
            {
              kind: "select",
              name: "active",
              label: "Trạng thái",
              defaultValue: sp.active,
              emptyLabel: "Mọi trạng thái",
              options: [
                { value: "1", label: "Đang hiệu lực" },
                { value: "0", label: "Ngưng" },
              ],
            },
          ]}
        />

        <p className="cta-row" style={{ marginTop: "0.75rem" }}>
          <Link className="btn btn-ghost" href="/bills">
            Tính giá trên {billLabel}
          </Link>
        </p>

        {!listRes.ok ? (
          <div className="alert alert-error" role="alert">
            {listRes.message}
          </div>
        ) : totalCount === 0 ? (
          <div className="empty-state" role="status">
            {sp.q || sp.partyType || sp.active
              ? "Không có bảng giá khớp bộ lọc."
              : "Chưa có bảng giá. Tạo mới để bắt đầu tính giá và seed chi phí dự kiến."}
          </div>
        ) : (
          <>
            <RateCardListWorkspace cards={pageRows} billLabel={billLabel} />
            <ListPagination
              basePath="/rate-cards"
              params={{
                q: sp.q,
                partyType: sp.partyType,
                active: sp.active,
              }}
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              totalPages={pages}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
