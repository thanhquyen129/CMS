import type { ReactNode } from "react";
import Link from "next/link";
import { ShellChrome } from "./ShellChrome";
import { NavGroup } from "./NavGroup";
import { term, type TerminologyMap } from "@/lib/terminology";

/** Level-1 modules per PO UI-15 Navigation Contract (14 modules). */
export type NavKey =
  | "dashboard"
  | "bills"
  | "rate-cards"
  | "costs"
  | "revenues"
  | "documents"
  | "ap"
  | "ar"
  | "settlements"
  | "control"
  | "financial-closes"
  | "reports"
  | "admin"
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
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const exceptionQueueLabel = term(terms, "EXCEPTION_QUEUE", "Hàng đợi ngoại lệ");
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");
  const reconQueueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const settingsLabel = term(terms, "SETTINGS", "Cài đặt");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");

  const brand = (
    <>
      <span className="brand-mark" aria-hidden="true" />
      <div>
        LCMS
        <small>Logistics Cost Management System</small>
      </div>
    </>
  );

  const nav = (
    <nav className="nav" aria-label="Điều hướng chính">
      <Link
        className={active === "dashboard" ? "active" : undefined}
        href="/dashboard"
      >
        Trang chủ
      </Link>

      <NavGroup
        label="Đơn hàng vận chuyển"
        openByDefault={active === "bills"}
      >
        <Link
          className={active === "bills" ? "active" : undefined}
          href="/bills"
        >
          Danh sách {billLabel}
        </Link>
        <Link href="/bills/new">Tạo {billLabel}</Link>
      </NavGroup>

      <Link
        className={active === "rate-cards" ? "active" : undefined}
        href="/rate-cards"
      >
        Bảng giá &amp; Tính giá
      </Link>

      <NavGroup label={costLabel} openByDefault={active === "costs"}>
        <Link
          className={active === "costs" ? "active" : undefined}
          href="/costs"
        >
          Quản lý {costLabel.toLowerCase()}
        </Link>
        <Link href="/costs/shared">
          {costLabel} {sharedLabel.toLowerCase()}
        </Link>
      </NavGroup>

      <Link
        className={active === "revenues" ? "active" : undefined}
        href="/revenues"
      >
        {revenueLabel} &amp; Lợi nhuận
      </Link>

      <Link
        className={active === "documents" ? "active" : undefined}
        href="/documents"
      >
        {docLabel}
      </Link>

      <Link className={active === "ap" ? "active" : undefined} href="/ap-ar?tab=ap">
        {apLabel} (AP)
      </Link>

      <Link className={active === "ar" ? "active" : undefined} href="/ap-ar?tab=ar">
        {arLabel} (AR)
      </Link>

      <Link
        className={active === "settlements" ? "active" : undefined}
        href="/settlements"
      >
        {paymentLabel} &amp; {collectionLabel}
      </Link>

      <NavGroup
        label="Kiểm soát tài chính"
        openByDefault={active === "control"}
      >
        <Link
          className={active === "control" ? "active" : undefined}
          href="/control"
        >
          Tổng quan kiểm soát
        </Link>
        <Link href="/queues/reconciliations">{reconQueueLabel}</Link>
        <Link href="/queues/variances">
          Hàng đợi {varianceLabel.toLowerCase()}
        </Link>
        <Link href="/queues/exceptions">{exceptionQueueLabel}</Link>
        <Link href="/queues/approvals">{approvalQueueLabel}</Link>
        <Link href="/bank-feed">{bankFeedLabel}</Link>
        <Link href="/reconciliations">Đối soát &amp; Matching</Link>
      </NavGroup>

      <Link
        className={active === "financial-closes" ? "active" : undefined}
        href="/financial-closes"
      >
        {closeLabel}
      </Link>

      <Link
        className={active === "reports" ? "active" : undefined}
        href="/reports"
      >
        Báo cáo &amp; Phân tích
      </Link>

      <Link className={active === "admin" ? "active" : undefined} href="/admin">
        Danh mục dữ liệu
      </Link>

      <NavGroup
        label="Hệ thống &amp; Cài đặt"
        openByDefault={active === "settings"}
      >
        <Link
          className={active === "settings" ? "active" : undefined}
          href="/settings"
        >
          {settingsLabel}
        </Link>
        <Link href="/integration-errors">Lỗi tích hợp</Link>
        <Link href="/workflow">Bản đồ luồng hệ thống</Link>
      </NavGroup>

      <div className="sidebar-footer" aria-hidden="true">
        <p className="sidebar-tagline">
          Kiểm soát chi phí · Tối ưu lợi nhuận · Phát triển bền vững
        </p>
        <p className="sidebar-version">LCMS v1.0.0</p>
      </div>
    </nav>
  );

  return (
    <ShellChrome brand={brand} nav={nav} topbarRight={topbarRight}>
      {children}
    </ShellChrome>
  );
}
