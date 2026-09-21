/** Client-safe bill types and display labels (no next/headers). */

import type { OperationalContext } from "./create-workspace";

export type BillListItem = {
  id: string;
  billNo: string;
  billType: string;
  operationalStatus: string;
  isActive: boolean;
  organizationId: string | null;
  createdBy: string | null;
  createdAt: string;
  /** Primary currency for list financial rollup (from API batch summary). */
  summaryCurrencyCode?: string | null;
  revenueBestAvailable?: number | null;
  costBestAvailable?: number | null;
  profitBestAvailable?: number | null;
  revenueExpectedTotal?: number | null;
  revenueConfirmedTotal?: number | null;
  revenueActualTotal?: number | null;
  costExpectedTotal?: number | null;
  costConfirmedTotal?: number | null;
  costActualTotal?: number | null;
  customerName?: string | null;
  routeCode?: string | null;
  transportMode?: string | null;
  costLineCount?: number | null;
  revenueLineCount?: number | null;
  documentCount?: number | null;
  etdAt?: string | null;
  etaAt?: string | null;
};

export type BillDto = BillListItem & {
  tenantId: string;
  sourceSystem: string | null;
  externalId: string | null;
  customerPartyId?: string | null;
  etdAt?: string | null;
  etaAt?: string | null;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  description?: string | null;
  internalNote?: string | null;
  originCode?: string | null;
  destinationCode?: string | null;
  customerReference?: string | null;
  context?: OperationalContext | null;
};

/** Vietnamese labels for operational status — never show raw enum to end users. */
export function operationalStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return "Nháp";
    case "active":
      return "Đang xử lý";
    case "confirmed":
      return "Đã xác nhận";
    case "completed":
    case "delivered":
      return "Đã giao";
    case "pending_document":
    case "awaiting_document":
      return "Chờ chứng từ";
    case "pending_approval":
      return "Chờ phê duyệt";
    case "recognized":
      return "Đã ghi nhận";
    case "closed":
      return "Đã đóng";
    case "cancelled":
    case "canceled":
      return "Đã hủy";
    default:
      return status || "—";
  }
}

/** Status pill tone class — maps operational status to mockup warn/ok/bad. */
export function operationalStatusPillClass(status: string): string {
  const s = (status ?? "").toLowerCase();
  return `status-pill status-${s || "unknown"}`;
}

export function billTypeLabel(billType: string): string {
  switch (billType?.toLowerCase()) {
    case "house":
    case "house_bill":
      return "House Bill";
    case "master":
    case "master_bill":
      return "Master Bill";
    case "air":
      return "Air";
    case "sea":
    case "ocean":
      return "Sea";
    case "road":
    case "truck":
      return "Road";
    case "rail":
      return "Rail";
    case "multimodal":
      return "Đa phương thức";
    case "parcel":
    case "postal":
    case "courier":
      return "Bưu kiện";
    case "freight":
      return "Vận tải";
    default:
      return billType || "—";
  }
}

/** Transport mode for list/create — distinct from House/Master Bill type. */
export function transportModeLabel(mode: string | null | undefined): string {
  switch ((mode ?? "").toLowerCase()) {
    case "air":
      return "Air";
    case "sea":
    case "ocean":
      return "Sea";
    case "road":
    case "truck":
      return "Road";
    case "rail":
      return "Rail";
    default:
      return mode || "—";
  }
}
