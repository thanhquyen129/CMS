import type { ReactNode } from "react";
import { cookies } from "next/headers";
import { ShellChrome } from "./ShellChrome";
import { NavGroup } from "./NavGroup";
import { NavLink } from "./NavLink";
import { TopbarAccount } from "./TopbarAccount";
import { DISPLAY_NAME_COOKIE } from "@/lib/auth";
import { term, type TerminologyMap } from "@/lib/terminology";
import { getTenantLicense, listInbox } from "@/lib/tenant-admin";

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
  navChild?:
    | "orders"
    | "orders-new"
    | "bills"
    | "bills-new"
    | "shipments"
    | "shipments-new"
    | "operations";
  children: ReactNode;
  topbarRight?: ReactNode;
};

export async function AppShell({ terms, active, navChild, children, topbarRight }: AppShellProps) {
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
  const license = await getTenantLicense();
  const inbox = await listInbox(true);
  const unreadNotifications = inbox.ok ? inbox.data.filter((n) => !n.isRead).length : 0;
  const enabled = new Set(
    license.ok
      ? license.data.modules.filter((m) => m.isEnabled && m.includedInPlan).map((m) => m.code)
      : []
  );
  const show = (code: string) => !license.ok || enabled.has(code);

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
      {show("dashboard") ? (
        <NavLink href="/dashboard" icon="home" active={active === "dashboard"}>
          Trang chủ
        </NavLink>
      ) : null}

      {show("bills") ? (
        <NavGroup
          label="Đơn hàng vận chuyển"
          icon="bills"
          openByDefault={active === "bills"}
          active={active === "bills"}
        >
          <NavLink href="/orders" active={navChild === "orders"}>
            Danh sách đơn hàng
          </NavLink>
          <NavLink href="/orders/new" active={navChild === "orders-new"}>
            Tạo đơn hàng
          </NavLink>
          <NavLink href="/bills" active={navChild === "bills" || (!navChild && active === "bills")}>
            Danh sách {billLabel}
          </NavLink>
          <NavLink href="/bills/new" active={navChild === "bills-new"}>
            Tạo {billLabel}
          </NavLink>
          <NavLink href="/shipments" active={navChild === "shipments"}>
            Danh sách Shipment
          </NavLink>
          <NavLink href="/shipments/new" active={navChild === "shipments-new"}>
            Tạo Shipment
          </NavLink>
          <NavLink href="/operations" active={navChild === "operations"}>
            Chặng &amp; Chuyến
          </NavLink>
        </NavGroup>
      ) : null}

      {show("rates") ? (
        <NavLink href="/rate-cards" icon="rates" active={active === "rate-cards"}>
          Bảng giá &amp; Tính giá
        </NavLink>
      ) : null}

      {show("costs") ? (
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
      ) : null}

      {show("revenues") ? (
        <NavLink href="/revenues" icon="revenues" active={active === "revenues"}>
          {revenueLabel} &amp; Lợi nhuận
        </NavLink>
      ) : null}

      {show("documents") ? (
        <NavLink href="/documents" icon="documents" active={active === "documents"}>
          {docLabel}
        </NavLink>
      ) : null}

      {show("ap") ? (
        <NavLink href="/ap-ar?tab=ap" icon="ap" active={active === "ap"}>
          {apLabel} (AP)
        </NavLink>
      ) : null}

      {show("ar") ? (
        <NavLink href="/ap-ar?tab=ar" icon="ar" active={active === "ar"}>
          {arLabel} (AR)
        </NavLink>
      ) : null}

      {show("settlements") ? (
        <NavLink href="/settlements" icon="settlements" active={active === "settlements"}>
          {paymentLabel} &amp; {collectionLabel}
        </NavLink>
      ) : null}

      {show("control") ? (
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
      ) : null}

      {show("closes") ? (
        <NavLink href="/financial-closes" icon="close" active={active === "financial-closes"}>
          {closeLabel}
        </NavLink>
      ) : null}

      {show("reports") ? (
        <NavLink href="/reports" icon="reports" active={active === "reports"}>
          Báo cáo &amp; Phân tích
        </NavLink>
      ) : null}

      {show("master") ? (
        <NavGroup
          label="Danh mục dữ liệu"
          icon="admin"
          openByDefault={active === "admin"}
          active={active === "admin"}
        >
          <NavLink href="/admin/parties?role=customer">Khách hàng</NavLink>
          <NavLink href="/admin/parties?role=vendor">Nhà cung cấp</NavLink>
          <NavLink href="/admin/catalog?kind=service_type">Dịch vụ</NavLink>
          <NavLink href="/admin/catalog?kind=cost_type">Loại chi phí</NavLink>
          <NavLink href="/admin/catalog?kind=revenue_type">Loại doanh thu</NavLink>
          <NavLink href="/admin/catalog?kind=transport_route">Tuyến vận chuyển</NavLink>
          <NavLink href="/admin/catalog?kind=transport_mode">Phương thức vận chuyển</NavLink>
          <NavLink href="/admin/catalog?kind=location">Cảng / Sân bay / Cửa khẩu</NavLink>
          <NavLink href="/admin/currencies">Tiền tệ &amp; Tỷ giá</NavLink>
          <NavLink href="/admin/catalog?kind=other">Danh mục khác</NavLink>
        </NavGroup>
      ) : null}

      {show("admin") ? (
        <NavGroup
          label="Hệ thống &amp; Cài đặt"
          icon="settings"
          openByDefault={active === "settings"}
          active={active === "settings"}
        >
          <NavLink href="/settings/company">Thông tin doanh nghiệp</NavLink>
          <NavLink href="/settings/users">Người dùng</NavLink>
          <NavLink href="/admin/access">Vai trò &amp; Phân quyền</NavLink>
          <NavLink href="/settings/business">Cấu hình nghiệp vụ</NavLink>
          <NavLink href="/settings" active={active === "settings"}>
            {settingsLabel}
          </NavLink>
          <NavLink href="/settings/integrations">Tích hợp API</NavLink>
          <NavLink href="/settings/audit">Nhật ký hệ thống</NavLink>
          <NavLink href="/settings/license">Quản lý license</NavLink>
          <NavLink href="/settings/notifications">Cài đặt thông báo</NavLink>
          <NavLink href="/settings/backup">Sao lưu &amp; Khôi phục</NavLink>
        </NavGroup>
      ) : null}
    </nav>
  );

  return (
    <ShellChrome
      brand={brand}
      nav={nav}
      topbarRight={
        <>
          {topbarRight}
          <TopbarAccount
            displayName={displayName}
            roleLabel="Đã đăng nhập"
            unreadNotifications={unreadNotifications}
          />
        </>
      }
    >
      {children}
    </ShellChrome>
  );
}
