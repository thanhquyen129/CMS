import type { ReactNode } from "react";
import Link from "next/link";
import { LogoutButton } from "./LogoutButton";
import { term, type TerminologyMap } from "@/lib/terminology";

type NavKey = "home" | "bills" | "dashboard";

type AppShellProps = {
  terms: TerminologyMap;
  active: NavKey;
  children: ReactNode;
  topbarRight?: ReactNode;
};

export function AppShell({ terms, active, children, topbarRight }: AppShellProps) {
  const billLabel = term(terms, "BILL", "Bill");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          CMS
          <small>Kiểm soát chi phí &amp; lợi nhuận</small>
        </div>
        <nav className="nav" aria-label="Điều hướng chính">
          <Link className={active === "home" ? "active" : undefined} href="/">
            Trang chính
          </Link>
          <Link className={active === "bills" ? "active" : undefined} href="/bills">
            {billLabel}
          </Link>
          <span className="soon" title="Sẽ có ở sprint U3">
            {dashboardLabel}
            <em>sắp có</em>
          </span>
        </nav>
      </aside>
      <div className="main">
        <div className="topbar">
          <div className="muted">Đã đăng nhập</div>
          <div className="topbar-actions">
            {topbarRight}
            <LogoutButton />
          </div>
        </div>
        {children}
      </div>
    </div>
  );
}
