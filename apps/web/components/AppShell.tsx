import type { ReactNode } from "react";
import { cookies } from "next/headers";
import { PageTopbar } from "./PageTopbar";
import { ShellChrome } from "./ShellChrome";
import { NavGroup } from "./NavGroup";
import { NavLink } from "./NavLink";
import { TopbarAccount } from "./TopbarAccount";
import { DISPLAY_NAME_COOKIE } from "@/lib/auth";
import { term, type TerminologyMap } from "@/lib/terminology";
import { getDashboardSummary } from "@/lib/control-desk";
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
  active?: NavKey;
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
  persist?: boolean;
};

export async function AppShell({
  terms,
  children,
  topbarRight,
  persist = false,
}: AppShellProps) {
  if (!persist) {
    if (!topbarRight) return children;
    return (
      <>
        <PageTopbar>{topbarRight}</PageTopbar>
        {children}
      </>
    );
  }
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
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");
  const reconQueueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const settingsLabel = term(terms, "SETTINGS", "Cài đặt");
  const [license, inbox, summary] = await Promise.all([
    getTenantLicense(),
    listInbox(true),
    getDashboardSummary(),
  ]);
  const unreadNotifications = inbox.ok ? inbox.data.filter((n) => !n.isRead).length : 0;
  const enabled = new Set(
    license.ok
      ? license.data.modules.filter((m) => m.isEnabled && m.includedInPlan).map((m) => m.code)
      : []
  );
  const show = (code: string) => !license.ok || enabled.has(code);
  // H-009: license opens the module; RBAC still gates cost ≠ revenue visibility.
  const vis = summary.ok
    ? summary.data.financialVisibility ?? {
        canViewCost: false,
        canViewRevenue: false,
        canViewMargin: false,
      }
    : { canViewCost: false, canViewRevenue: false, canViewMargin: false };
  const showCosts = show("costs") && vis.canViewCost;
  const showRevenues = show("revenues") && vis.canViewRevenue;
  const showProfitReport = showRevenues && vis.canViewMargin;

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
        <NavLink href="/dashboard" icon="home">
          Trang chủ
        </NavLink>
      ) : null}

      {show("bills") ? (
        <NavGroup
          label="Đơn hàng vận chuyển"
          icon="bills"
          match={["/orders", "/bills", "/shipments", "/operations"]}
        >
          <NavLink href="/orders">Danh sách đơn hàng</NavLink>
          <NavLink href="/orders/new">Tạo đơn hàng</NavLink>
          <NavLink href="/bills">Danh sách {billLabel}</NavLink>
          <NavLink href="/bills/new">Tạo {billLabel}</NavLink>
          <NavLink href="/shipments">Danh sách Shipment</NavLink>
          <NavLink href="/shipments/new">Tạo Shipment</NavLink>
          <NavLink href="/operations">Chặng & Chuyến</NavLink>
          <NavLink href="/operations/legs/new">Tạo chặng</NavLink>
          <NavLink href="/operations/movements/new">Tạo chuyến</NavLink>
          <NavLink href="/operations/import">Nhập nghiệp vụ</NavLink>
        </NavGroup>
      ) : null}

      {show("rates") ? (
        <NavGroup label="Bảng giá & Tính giá" icon="rates" match={["/rate-cards"]}>
          <NavLink href="/rate-cards">Danh sách bảng giá</NavLink>
          <NavLink href="/rate-cards/import">Nhập bảng giá</NavLink>
          <NavLink href="/rate-cards/rate">Tính giá</NavLink>
          <NavLink href="/rate-cards/compare">So sánh giá</NavLink>
          <NavLink href="/rate-cards/surcharges">Quản lý phụ phí</NavLink>
          <NavLink href="/rate-cards/fx">Quản lý tỷ giá</NavLink>
          <NavLink href="/rate-cards/appendices">Quản lý phụ lục giá</NavLink>
          <NavLink href="/rate-cards/history">Lịch sử giá</NavLink>
        </NavGroup>
      ) : null}

      {showCosts ? (
        <NavGroup label={costLabel} icon="costs" match={["/costs"]}>
          <NavLink href="/costs">Danh sách {costLabel.toLowerCase()}</NavLink>
          <NavLink href="/costs/shared/new">Tạo {costLabel.toLowerCase()}</NavLink>
          <NavLink href="/costs/shared">Phân bổ {costLabel.toLowerCase()}</NavLink>
        </NavGroup>
      ) : null}

      {showRevenues ? (
        <NavGroup label={`${revenueLabel} & Lợi nhuận`} icon="revenues" match={["/revenues"]}>
          <NavLink href="/revenues">Danh sách {revenueLabel.toLowerCase()}</NavLink>
          <NavLink href="/revenues/new">Tạo {revenueLabel.toLowerCase()}</NavLink>
          {showProfitReport ? (
            <NavLink href="/revenues/report">Báo cáo {revenueLabel.toLowerCase()}</NavLink>
          ) : null}
        </NavGroup>
      ) : null}

      {show("documents") ? (
        <NavLink href="/documents" icon="documents">
          {docLabel}
        </NavLink>
      ) : null}

      {show("ap") ? (
        <NavLink href="/ap-ar?tab=ap" icon="ap">
          {apLabel} (AP)
        </NavLink>
      ) : null}

      {show("ar") ? (
        <NavLink href="/ap-ar?tab=ar" icon="ar">
          {arLabel} (AR)
        </NavLink>
      ) : null}

      {show("settlements") ? (
        <NavLink href="/settlements" icon="settlements">
          {paymentLabel} & {collectionLabel}
        </NavLink>
      ) : null}

      {show("control") ? (
        <NavGroup
          label="Kiểm soát tài chính"
          icon="control"
          match={[
            "/control",
            "/queues",
            "/bank-feed",
            "/reconciliations",
          ]}
        >
          <NavLink href="/control">Tổng quan kiểm soát</NavLink>
          <NavLink href="/queues/reconciliations">{reconQueueLabel}</NavLink>
          <NavLink href="/queues/variances">
            Hàng đợi {varianceLabel.toLowerCase()}
          </NavLink>
          <NavLink href="/queues/exceptions">Chênh lệch & Ngoại lệ</NavLink>
          <NavLink href="/queues/approvals">Phê duyệt chứng từ</NavLink>
          <NavLink href="/bank-feed">{bankFeedLabel}</NavLink>
          <NavLink href="/reconciliations">Đối soát & Matching</NavLink>
        </NavGroup>
      ) : null}

      {show("closes") ? (
        <NavLink href="/financial-closes" icon="close">
          {closeLabel}
        </NavLink>
      ) : null}

      {show("reports") ? (
        <NavLink href="/reports" icon="reports">
          Báo cáo & Phân tích
        </NavLink>
      ) : null}

      {show("master") ? (
        <NavGroup
          label="Danh mục dữ liệu"
          icon="admin"
          match={[
            "/admin/parties",
            "/admin/catalog",
            "/admin/locations",
            "/admin/routes",
            "/admin/commodities",
            "/admin/currencies",
            "/admin/organizations",
          ]}
        >
          <NavLink href="/admin/parties?role=customer">Khách hàng</NavLink>
          <NavLink href="/admin/parties?role=vendor">Nhà cung cấp</NavLink>
          <NavLink href="/admin/parties/import">Nhập đối tác</NavLink>
          <NavLink href="/admin/catalog?kind=service_type">Dịch vụ</NavLink>
          <NavLink href="/admin/catalog?kind=cost_type">Loại chi phí</NavLink>
          <NavLink href="/admin/catalog?kind=revenue_type">Loại doanh thu</NavLink>
          <NavLink href="/admin/routes">Tuyến vận chuyển</NavLink>
          <NavLink href="/admin/catalog?kind=transport_mode">Phương thức vận chuyển</NavLink>
          <NavLink href="/admin/locations">Địa điểm</NavLink>
          <NavLink href="/admin/commodities">Loại hàng</NavLink>
          <NavLink href="/admin/currencies">Tiền tệ & Tỷ giá</NavLink>
          <NavLink href="/admin/catalog?kind=other">Danh mục khác</NavLink>
        </NavGroup>
      ) : null}

      {show("admin") ? (
        <NavGroup
          label="Hệ thống & Cài đặt"
          icon="settings"
          match={["/settings", "/admin/access", "/workflow"]}
        >
          <NavLink href="/settings/company">Thông tin doanh nghiệp</NavLink>
          <NavLink href="/settings/users">Người dùng</NavLink>
          <NavLink href="/admin/access">Vai trò & Phân quyền</NavLink>
          <NavLink href="/settings/business">Cấu hình nghiệp vụ</NavLink>
          <NavLink href="/settings/policies">Sổ chính sách</NavLink>
          <NavLink href="/settings">{settingsLabel}</NavLink>
          <NavLink href="/settings/integrations">Tích hợp API</NavLink>
          <NavLink href="/settings/audit">Nhật ký hệ thống</NavLink>
          <NavLink href="/settings/license">Quản lý license</NavLink>
          <NavLink href="/settings/notifications">Cài đặt thông báo</NavLink>
          <NavLink href="/settings/backup">Sao lưu & Khôi phục</NavLink>
          <NavLink href="/workflow">Bản đồ luồng nghiệp vụ</NavLink>
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
