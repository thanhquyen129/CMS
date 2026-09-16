import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPagination } from "@/components/ListPagination";
import { RateCardListWorkspace } from "@/components/RateCardListWorkspace";
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
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Bảng giá &amp; Tính giá
        </p>
        <div className="page-header-row">
          <div>
            <h1>Danh sách bảng giá</h1>
            <p className="lede">
              Rate card → phiên bản → quy tắc → phát hành → tính giá trên {billLabel} →
              seed {costLabel.toLowerCase()} {expected.toLowerCase()}. Rating tạo kỳ vọng
              tài chính, không tạo {actual.toLowerCase()}.
            </p>
          </div>
          <Link className="btn" href="/rate-cards/new">
            + Tạo bảng giá
          </Link>
        </div>

        {kpiRes.ok ? (
          <div className="stat-grid" style={{ marginTop: "0.85rem" }}>
            <div className="stat-card">
              <span className="stat-label">Tổng bảng giá</span>
              <strong className="stat-value">{allCards.length}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">Giá mua (NCC)</span>
              <strong className="stat-value">{buyCount}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">Giá bán (KH)</span>
              <strong className="stat-value">{sellCount}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">Đang hiệu lực</span>
              <strong className="stat-value">{activeCount}</strong>
            </div>
          </div>
        ) : null}

        <form
          className="search-bar denser-filters"
          method="get"
          action="/rate-cards"
          style={{ marginTop: "0.85rem" }}
        >
          <label className="sr-only" htmlFor="q">
            Tìm bảng giá
          </label>
          <input
            id="q"
            name="q"
            type="search"
            placeholder="Mã hoặc tên bảng giá…"
            defaultValue={sp.q ?? ""}
            autoComplete="off"
          />
          <select id="partyType" name="partyType" defaultValue={sp.partyType ?? ""}>
            <option value="">Tất cả loại giá</option>
            <option value="vendor">Giá mua (NCC)</option>
            <option value="customer">Giá bán (KH)</option>
          </select>
          <select id="active" name="active" defaultValue={sp.active ?? ""}>
            <option value="">Mọi trạng thái</option>
            <option value="1">Đang hiệu lực</option>
            <option value="0">Ngưng</option>
          </select>
          <button className="btn" type="submit">
            Lọc
          </button>
          {sp.q || sp.partyType || sp.active ? (
            <Link className="btn btn-ghost" href="/rate-cards">
              Làm mới
            </Link>
          ) : null}
        </form>

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
