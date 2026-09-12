import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

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
    <AppShell terms={terms} active="home">
      <section className="panel">
        <h1>Shell vận hành</h1>
        <p>
          Đây là lớp kiểm soát tài chính CMS. Mở danh sách {billLabel} để xem hồ sơ tài
          chính (Dự kiến / Đã xác nhận / Thực tế). Xác nhận {costLabel}/{revenueLabel} và{" "}
          {dashboardLabel} sẽ mở ở sprint sau — không có dữ liệu giả.
        </p>
        <p className="cta-row">
          <Link className="btn" href="/bills">
            Mở danh sách {billLabel}
          </Link>
        </p>
      </section>
    </AppShell>
  );
}
