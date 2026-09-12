import type { ReactNode } from "react";
import Link from "next/link";
import { LogoutButton } from "./LogoutButton";
import { term, type TerminologyMap } from "@/lib/terminology";

type NavKey =
  | "dashboard"
  | "bills"
  | "documents"
  | "ap-ar"
  | "exceptions"
  | "approvals";

type AppShellProps = {
  terms: TerminologyMap;
  active: NavKey;
  children: ReactNode;
  topbarRight?: ReactNode;
};

export function AppShell({ terms, active, children, topbarRight }: AppShellProps) {
  const billLabel = term(terms, "BILL", "Bill");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const exceptionQueueLabel = term(terms, "EXCEPTION_QUEUE", "Hàng đợi ngoại lệ");
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          CMS
          <small>Kiểm soát chi phí &amp; lợi nhuận</small>
        </div>
        <nav className="nav" aria-label="Điều hướng chính">
          <Link
            className={active === "dashboard" ? "active" : undefined}
            href="/dashboard"
          >
            {dashboardLabel}
          </Link>
          <Link className={active === "bills" ? "active" : undefined} href="/bills">
            {billLabel}
          </Link>
          <Link
            className={active === "documents" ? "active" : undefined}
            href="/documents"
          >
            {docLabel}
          </Link>
          <Link className={active === "ap-ar" ? "active" : undefined} href="/ap-ar">
            {apLabel} / {arLabel}
          </Link>
          <Link
            className={active === "exceptions" ? "active" : undefined}
            href="/queues/exceptions"
          >
            {exceptionQueueLabel}
          </Link>
          <Link
            className={active === "approvals" ? "active" : undefined}
            href="/queues/approvals"
          >
            {approvalQueueLabel}
          </Link>
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
