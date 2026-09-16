import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listBills } from "@/lib/bills";
import { formatDateTimeVi } from "@/lib/money";

type SearchParams = Promise<{ q?: string }>;

export default async function BillsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { q } = await searchParams;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const result = await listBills(q);

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Đơn hàng vận chuyển
          {" / "}
          Danh sách {billLabel}
        </p>
        <h1>Danh sách {billLabel}</h1>
        <p className="lede">
          Quản lý vận đơn ({billLabel}) và thông tin tài chính liên quan —{" "}
          {term(terms, "EXPECTED", "Dự kiến")} /{" "}
          {term(terms, "CONFIRMED", "Đã xác nhận")} /{" "}
          {term(terms, "ACTUAL", "Thực tế")}. {billLabel} là neo tài chính (Financial
          Anchor).
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn" href="/bills/new">
            Tạo {billLabel}
          </Link>
        </p>

        <form className="search-bar" method="get" action="/bills" role="search">
          <label className="sr-only" htmlFor="q">
            Tìm {billLabel}
          </label>
          <input
            id="q"
            name="q"
            type="search"
            placeholder={`Số ${billLabel}…`}
            defaultValue={q ?? ""}
            autoComplete="off"
          />
          <button className="btn" type="submit">
            Tìm kiếm
          </button>
          {q ? (
            <Link className="btn btn-ghost" href="/bills">
              Xóa lọc
            </Link>
          ) : null}
        </form>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            {q?.trim() ? (
              `Không có ${billLabel} khớp “${q.trim()}”. Thử số hiệu khác.`
            ) : (
              <>
                Chưa có {billLabel} nào trong phạm vi của bạn.{" "}
                <Link className="row-link" href="/bills/new">
                  Tạo {billLabel} mới
                </Link>
                .
              </>
            )}
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Số {billLabel}</th>
                  <th scope="col">Loại</th>
                  <th scope="col">Trạng thái vận hành</th>
                  <th scope="col">Tạo lúc</th>
                  <th scope="col">
                    <span className="sr-only">Mở</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((b) => (
                  <tr key={b.id}>
                    <td>
                      <Link className="row-link" href={`/bills/${b.id}`}>
                        {b.billNo}
                      </Link>
                    </td>
                    <td>{b.billType}</td>
                    <td>{b.operationalStatus}</td>
                    <td>{formatDateTimeVi(b.createdAt)}</td>
                    <td>
                      <Link className="btn btn-ghost btn-sm" href={`/bills/${b.id}`}>
                        Mở hồ sơ
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
