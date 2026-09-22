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
import { fetchTerminology } from "@/lib/api";
import { listRateCards } from "@/lib/rate-cards-server";
import {
  parsePage,
  parsePageSize,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";

type SearchParams = Promise<{
  partyType?: string;
  mode?: string;
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
  const billLabel = "Bill";

  const partyType =
    sp.partyType === "vendor" || sp.partyType === "customer"
      ? sp.partyType
      : undefined;
  const mode = sp.mode?.trim() || undefined;
  const active =
    sp.active === "1" ? true : sp.active === "0" ? false : undefined;
  const q = sp.q?.trim() || undefined;

  const pageSize = parsePageSize(sp.pageSize);
  const pageHint = Math.max(
    1,
    Number.parseInt(String(sp.page ?? "1"), 10) || 1
  );

  const [listRes, kpiRes] = await Promise.all([
    listRateCards({ q, partyType, transportMode: mode, active, page: pageHint, pageSize }),
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
          lede="Quản lý bảng giá mua, bảng giá bán và cấu hình tính giá"
          action={
            <Link className="btn" href="/rate-cards/new">
              ＋ Tạo bảng giá
            </Link>
          }
        />

        {kpiRes.ok ? (
          <StatCardGrid
            cards={[
              { key: "total", label: "Tổng số bảng giá", value: allCards.length },
              {
                key: "buy",
                label: "Giá mua",
                value: buyCount,
                tone: "warning",
              },
              {
                key: "sell",
                label: "Giá bán",
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
          resetHref={sp.q || sp.partyType || sp.mode || sp.active ? "/rate-cards" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm bảng giá",
              placeholder: "Tên bảng giá, hãng, tuyến...",
              defaultValue: sp.q,
            },
            {
              kind: "select",
              name: "partyType",
              label: "Loại giá",
              defaultValue: sp.partyType,
              emptyLabel: "Tất cả",
              options: [
                { value: "vendor", label: "Giá mua" },
                { value: "customer", label: "Giá bán" },
              ],
            },
            {
              kind: "select",
              name: "mode",
              label: "Phương thức",
              defaultValue: sp.mode,
              emptyLabel: "Tất cả",
              options: [
                { value: "air", label: "Air" },
                { value: "sea", label: "Sea" },
                { value: "road", label: "Road" },
              ],
            },
            {
              kind: "select",
              name: "active",
              label: "Trạng thái",
              defaultValue: sp.active,
              emptyLabel: "Tất cả",
              options: [
                { value: "1", label: "Đang hiệu lực" },
                { value: "0", label: "Ngưng" },
              ],
            },
          ]}
        />

        <form className="form-grid" action="/rate-cards/compare" style={{ marginTop: "0.75rem" }}>
          <p className="field field-span" style={{ margin: 0 }}>
            <strong>Tính giá nhanh</strong>
          </p>
          <div className="field">
            <label htmlFor="quick-mode">Phương thức</label>
            <select id="quick-mode" name="transportMode" defaultValue="air">
              <option value="air">Air</option>
              <option value="sea">Sea</option>
              <option value="road">Road</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="quick-from">Từ</label>
            <input id="quick-from" name="originCode" placeholder="SGN" />
          </div>
          <div className="field">
            <label htmlFor="quick-to">Đến</label>
            <input id="quick-to" name="destinationCode" placeholder="FRA" />
          </div>
          <div className="field">
            <label htmlFor="quick-date">Ngày đi</label>
            <input id="quick-date" name="rateDate" type="date" />
          </div>
          <div className="field">
            <label htmlFor="quick-qty">Trọng lượng</label>
            <input id="quick-qty" name="quantity" inputMode="decimal" placeholder="100" />
          </div>
          <div className="field" style={{ alignSelf: "end" }}>
            <button className="btn" type="submit">Tính giá</button>
          </div>
        </form>

        {!listRes.ok ? (
          <div className="alert alert-error" role="alert">
            {listRes.message}
          </div>
        ) : totalCount === 0 ? (
          <div className="empty-state" role="status">
            {sp.q || sp.partyType || sp.mode || sp.active
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
                mode: sp.mode,
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
