import type { ReactNode } from "react";
import { cookies } from "next/headers";
import { ShellChrome } from "./ShellChrome";
import { NavGroup } from "./NavGroup";
import { NavLink } from "./NavLink";
import { TopbarAccount } from "./TopbarAccount";
import { DISPLAY_NAME_COOKIE } from "@/lib/auth";
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

export async function AppShell({ terms, active, children, topbarRight }: AppShellProps) {
  const jar = await cookies();
  const displayName = jar.get(DISPLAY_NAME_COOKIE)?.value?.trim() || "";
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
      <NavLink href="/dashboard" icon="home" active={active === "dashboard"}>
        Trang chủ
      </NavLink>

      <NavGroup
        label="Đơn hàng vận chuyển"
        icon="bills"
        openByDefault={active === "bills"}
        active={active === "bills"}
      >
        <NavLink href="/bills" active={active === "bills"}>
          Danh sách {billLabel}
        </NavLink>
        <NavLink href="/bills/new">Tạo {billLabel}</NavLink>
      </NavGroup>

      <NavLink
        href="/rate-cards"
        icon="rates"
        active={active === "rate-cards"}
      >
        Bảng giá &amp; Tính giá
      </NavLink>

      <NavGroup
        label={costLabel}
        icon="costs"
        openByDefault={active === "costs"}
        active={active === "costs"}
      >
        <NavLink href="/costs" active={active === "costs"}>
          Quản lý {costLabel.toLowerCase()}
        </NavLink>
        <NavLink href="/costs/shared">
          {costLabel} {sharedLabel.toLowerCase()}
        </NavLink>
      </NavGroup>

      <NavLink href="/revenues" icon="revenues" active={active === "revenues"}>
        {revenueLabel} &amp; Lợi nhuận
      </NavLink>

      <NavLink href="/documents" icon="documents" active={active === "documents"}>
        {docLabel}
      </NavLink>

      <NavLink href="/ap-ar?tab=ap" icon="ap" active={active === "ap"}>
        {apLabel} (AP)
      </NavLink>

      <NavLink href="/ap-ar?tab=ar" icon="ar" active={active === "ar"}>
        {arLabel} (AR)
      </NavLink>

      <NavLink
        href="/settlements"
        icon="settlements"
        active={active === "settlements"}
      >
        {paymentLabel} &amp; {collectionLabel}
      </NavLink>

      <NavGroup
        label="Kiểm soát tài chính"
        icon="control"
        openByDefault={active === "control"}
        active={active === "control"}
      >
        <NavLink href="/control" active={active === "control"}>
          Tổng quan kiểm soát
        </NavLink>
        <NavLink href="/queues/reconciliations">{reconQueueLabel}</NavLink>
        <NavLink href="/queues/variances">
          Hàng đợi {varianceLabel.toLowerCase()}
        </NavLink>
        <NavLink href="/queues/exceptions">{exceptionQueueLabel}</NavLink>
        <NavLink href="/queues/approvals">{approvalQueueLabel}</NavLink>
        <NavLink href="/bank-feed">{bankFeedLabel}</NavLink>
        <NavLink href="/reconciliations">Đối soát &amp; Matching</NavLink>
      </NavGroup>

      <NavLink
        href="/financial-closes"
        icon="close"
        active={active === "financial-closes"}
      >
        {closeLabel}
      </NavLink>

      <NavLink href="/reports" icon="reports" active={active === "reports"}>
        Báo cáo &amp; Phân tích
      </NavLink>

      <NavLink href="/admin" icon="admin" active={active === "admin"}>
        Danh mục dữ liệu
      </NavLink>

      <NavGroup
        label="Hệ thống &amp; Cài đặt"
        icon="settings"
        openByDefault={active === "settings"}
        active={active === "settings"}
      >
        <NavLink href="/settings" active={active === "settings"}>
          {settingsLabel}
        </NavLink>
        <NavLink href="/integration-errors">Lỗi tích hợp</NavLink>
        <NavLink href="/workflow">Bản đồ luồng hệ thống</NavLink>
      </NavGroup>
    </nav>
  );

  return (
    <ShellChrome
      brand={brand}
      nav={nav}
      topbarRight={
        <>
          {topbarRight}
          <TopbarAccount displayName={displayName} roleLabel="Đã đăng nhập" />
        </>
      }
    >
      {children}
    </ShellChrome>
  );
}
