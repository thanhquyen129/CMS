import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { LogoutButton } from "@/components/LogoutButton";

export default async function HomePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          CMS
          <small>Kiểm soát chi phí &amp; lợi nhuận</small>
        </div>
        <nav className="nav" aria-label="Điều hướng chính">
          <Link className="active" href="/">
            Trang chính
          </Link>
          <span className="soon" title="Sẽ có ở sprint U1">
            {billLabel}
            <em>sắp có</em>
          </span>
          <span className="soon" title="Sẽ có ở sprint U3">
            {dashboardLabel}
            <em>sắp có</em>
          </span>
        </nav>
      </aside>
      <div className="main">
        <div className="topbar">
          <div className="muted">Đã đăng nhập</div>
          <LogoutButton />
        </div>
        <section className="panel">
          <h1>Shell vận hành</h1>
          <p>
            Đây là lớp kiểm soát tài chính CMS. Màn {billLabel}, xác nhận {costLabel}/
            {revenueLabel} và {dashboardLabel} sẽ mở dần theo sprint UI — không có dữ liệu giả.
          </p>
        </section>
      </div>
    </div>
  );
}
