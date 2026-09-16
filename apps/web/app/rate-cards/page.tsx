import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { partyTypeLabel } from "@/lib/rate-cards";
import { listRateCards } from "@/lib/rate-cards-server";

export default async function RateCardsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");
  const actual = term(terms, "ACTUAL", "Thực tế");

  const listRes = await listRateCards();
  const cards = listRes.ok ? listRes.data : [];
  const activeCount = cards.filter((c) => c.isActive).length;
  const buyCount = cards.filter((c) => c.partyType?.toLowerCase() === "vendor").length;
  const sellCount = cards.filter(
    (c) => c.partyType?.toLowerCase() === "customer"
  ).length;

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

        {listRes.ok ? (
          <div className="stat-grid" style={{ marginTop: "0.85rem" }}>
            <div className="stat-card">
              <span className="stat-label">Tổng bảng giá</span>
              <strong className="stat-value">{cards.length}</strong>
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

        <p className="cta-row" style={{ marginTop: "0.75rem" }}>
          <Link className="btn btn-ghost" href="/bills">
            Tính giá trên {billLabel}
          </Link>
        </p>

        {!listRes.ok ? (
          <div className="alert alert-error" role="alert">
            {listRes.message}
          </div>
        ) : cards.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có bảng giá. Tạo mới để bắt đầu tính giá và seed chi phí dự kiến.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Mã bảng giá</th>
                  <th scope="col">Tên bảng giá</th>
                  <th scope="col">Loại giá</th>
                  <th scope="col">Tiền tệ</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">
                    <span className="sr-only">Mở</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {cards.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <Link className="row-link" href={`/rate-cards/${c.id}`}>
                        <code>{c.code}</code>
                      </Link>
                    </td>
                    <td>{c.name}</td>
                    <td>
                      <span className="status-pill">{partyTypeLabel(c.partyType)}</span>
                    </td>
                    <td>{c.currencyCode}</td>
                    <td>
                      <span
                        className={`maturity-pill ${c.isActive ? "maturity-confirmed" : ""}`}
                      >
                        {c.isActive ? "Đang hiệu lực" : "Ngưng"}
                      </span>
                    </td>
                    <td>
                      <Link className="btn btn-ghost btn-sm" href={`/rate-cards/${c.id}`}>
                        Chi tiết
                      </Link>
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
