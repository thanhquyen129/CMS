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

  const listRes = await listRateCards();
  const cards = listRes.ok ? listRes.data : [];

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
            <h1>Bảng giá &amp; Tính giá</h1>
            <p className="lede">
              Rate card → phiên bản → quy tắc → phát hành → tính giá trên {billLabel} →
              seed {costLabel.toLowerCase()} {expected.toLowerCase()}. Rating tạo kỳ vọng
              tài chính, không tạo {term(terms, "ACTUAL", "Thực tế").toLowerCase()}.
            </p>
          </div>
          <Link className="btn" href="/rate-cards/new">
            + Tạo bảng giá
          </Link>
        </div>

        <p className="cta-row" style={{ marginTop: "0.75rem" }}>
          <Link className="btn btn-ghost" href="/bills">
            Danh sách {billLabel}
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
                  <th scope="col">Mã</th>
                  <th scope="col">Tên</th>
                  <th scope="col">Đối tác</th>
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
                      <code>{c.code}</code>
                    </td>
                    <td>{c.name}</td>
                    <td>{partyTypeLabel(c.partyType)}</td>
                    <td>{c.currencyCode}</td>
                    <td>{c.isActive ? "Đang dùng" : "Ngưng"}</td>
                    <td>
                      <Link className="row-link" href={`/rate-cards/${c.id}`}>
                        Mở
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
