import type { ReactNode } from "react";
import Link from "next/link";
import { LogoutButton } from "./LogoutButton";
import { term, type TerminologyMap } from "@/lib/terminology";

type NavKey =
  | "dashboard"
  | "bills"
  | "costs"
  | "documents"
  | "ap-ar"
  | "settlements"
  | "financial-closes"
  | "exceptions"
  | "approvals"
  | "reconciliations"
  | "bank-feed"
  | "settings";

type AppShellProps = {
  terms: TerminologyMap;
  active: NavKey;
  children: ReactNode;
  topbarRight?: ReactNode;
};

export function AppShell({ terms, active, children, topbarRight }: AppShellProps) {
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const exceptionQueueLabel = term(terms, "EXCEPTION_QUEUE", "Hàng đợi ngoại lệ");
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");
  const reconQueueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const settingsLabel = term(terms, "SETTINGS", "Cài đặt");

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true" />
          <div>
            CMS
            <small>Kiểm soát chi phí &amp; lợi nhuận</small>
          </div>
        </div>
        <nav className="nav" aria-label="Điều hướng chính">
          <div className="nav-section">
            <div className="nav-section-label">Chính</div>
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
              className={active === "costs" ? "active" : undefined}
              href="/costs/shared"
            >
              {costLabel} {sharedLabel.toLowerCase()}
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
              className={active === "settlements" ? "active" : undefined}
              href="/settlements"
            >
              {paymentLabel} / {collectionLabel}
            </Link>
            <Link
              className={active === "bank-feed" ? "active" : undefined}
              href="/bank-feed"
            >
              {bankFeedLabel}
            </Link>
            <Link
              className={active === "financial-closes" ? "active" : undefined}
              href="/financial-closes"
            >
              {closeLabel}
            </Link>
          </div>
          <div className="nav-section nav-section-queues">
            <div className="nav-section-label">Hàng đợi</div>
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
            <Link
              className={active === "reconciliations" ? "active" : undefined}
              href="/queues/reconciliations"
            >
              {reconQueueLabel}
            </Link>
          </div>
          <div className="nav-section">
            <div className="nav-section-label">Hệ thống</div>
            <Link
              className={active === "settings" ? "active" : undefined}
              href="/settings"
            >
              {settingsLabel}
            </Link>
          </div>
        </nav>
        <div className="sidebar-footer">
          <LogoutButton />
        </div>
      </aside>
      <div className="main">
        <div className="topbar">
          <div className="muted topbar-status">Đã đăng nhập</div>
          <div className="topbar-actions">
            {topbarRight}
            <span className="topbar-logout">
              <LogoutButton />
            </span>
          </div>
        </div>
        {children}
      </div>
    </div>
  );
}
