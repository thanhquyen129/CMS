/** Client-safe bill types and display labels (no next/headers). */

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
  costLineCount?: number | null;
  revenueLineCount?: number | null;
  documentCount?: number | null;
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
};

/** Vietnamese labels for operational status — never show raw enum to end users. */
export function operationalStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
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

export function billTypeLabel(billType: string): string {
  switch (billType?.toLowerCase()) {
    case "air":
      return "Hàng không";
    case "sea":
    case "ocean":
      return "Đường biển";
    case "road":
    case "truck":
      return "Đường bộ";
    case "rail":
      return "Đường sắt";
    case "multimodal":
      return "Đa phương thức";
    default:
      return billType || "—";
  }
}
