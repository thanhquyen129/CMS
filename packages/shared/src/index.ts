export type TerminologyMap = Record<string, string>;

export const DEFAULT_TERMINOLOGY_VI: TerminologyMap = {
  // Maturity
  "maturity.expected": "Dự kiến (Expected)",
  "maturity.confirmed": "Đã xác nhận (Confirmed)",
  "maturity.actual": "Thực tế (Actual)",

  // Cost / Revenue Attribution
  "attribution.direct": "Trực tiếp theo Bill",
  "attribution.shared": "Chi phí dùng chung (Shared)",

  // Approval Statuses
  "approval.not_required": "Không cần duyệt",
  "approval.pending": "Chờ phê duyệt",
  "approval.approved": "Đã phê duyệt",
  "approval.rejected": "Đã từ chối",

  // Exception Statuses & Severities
  "exception.open": "Đang mở",
  "exception.in_progress": "Đang xử lý",
  "exception.escalated": "Đã leo thang (Escalated)",
  "exception.resolved": "Đã xử lý",
  "exception.ignored": "Đã bỏ qua (có lý do)",
  "severity.low": "Thấp",
  "severity.medium": "Trung bình",
  "severity.high": "Cao",
  "severity.critical": "Nghiêm trọng",

  // Document 3-axis Statuses
  "receipt.draft": "Nháp",
  "receipt.received": "Đã nhận chứng từ",
  "acceptance.not_accepted": "Chưa nghiệm thu",
  "acceptance.accepted": "Đã nghiệm thu",
  "acceptance.rejected": "Từ chối nghiệm thu",
  "matching.unmatched": "Chưa đối chiếu (Unmatched)",
  "matching.partially_matched": "Đối chiếu một phần",
  "matching.matched": "Đã đối chiếu hoàn tất",

  // AP / AR Settlement Statuses
  "settlement.unsettled": "Chưa thanh toán",
  "settlement.partially_settled": "Thanh toán một phần",
  "settlement.settled": "Đã tất toán",
  "settlement.written_off": "Đã xóa nợ (Write-off)",

  // FX Statuses
  "fx.ok": "Tỷ giá hợp lệ",
  "fx.missing": "Thiếu tỷ giá",
  "fx.requires_review": "Cần rà soát tỷ giá",
  "fx.manual_override": "Tỷ giá ghi đè thủ công",

  // Operational Statuses
  "bill.draft": "Nháp",
  "bill.in_transit": "Đang vận chuyển",
  "bill.completed": "Hoàn tất vận hành",
  "bill.cancelled": "Đã hủy",
};

export function term(
  map: TerminologyMap | undefined | null,
  key: string,
  fallback?: string
): string {
  if (map && map[key]) {
    return map[key];
  }
  if (DEFAULT_TERMINOLOGY_VI[key]) {
    return DEFAULT_TERMINOLOGY_VI[key];
  }
  return fallback ?? key;
}

export type MobilePersona =
  | "executive_control"
  | "cost_accountant"
  | "revenue_accountant"
  | "field_ops"
  | "master_data"
  | "viewer";

export interface MobileUserDto {
  id: string;
  email: string;
  displayName: string;
  organizationId?: string | null;
}

export interface MobileTenantDto {
  id: string;
  code: string;
  name: string;
  defaultCurrencyCode: string;
  timeZoneId: string;
  dateFormat: string;
}

export interface MobileFinancialVisibilityDto {
  canViewCost: boolean;
  canViewRevenue: boolean;
  canViewMargin: boolean;
}

export interface MobileTabDto {
  key: string;
  labelVi: string;
  icon: string;
  route: string;
  badgeCount: number;
}

export interface MobileModuleDto {
  code: string;
  titleVi: string;
  subtitleVi: string;
  icon: string;
  route: string;
  category: "control" | "operations" | "cost_ap" | "revenue_ar" | "master_admin";
  badgeCount: number;
}

export interface MobileBadgesDto {
  unreadNotifications: number;
  pendingApprovals: number;
  openExceptions: number;
  openVariances: number;
  fxExceptions: number;
}

export interface MobileBootstrapDto {
  asOfUtc: string;
  user: MobileUserDto;
  tenant: MobileTenantDto;
  roleCodes: string[];
  primaryPersona: MobilePersona;
  permissions: string[];
  financialVisibility: MobileFinancialVisibilityDto;
  enabledModules: string[];
  bottomTabs: MobileTabDto[];
  modules: MobileModuleDto[];
  badges: MobileBadgesDto;
  terminology: TerminologyMap;
}

export type AttachmentObjectType =
  | "financial_document"
  | "bill"
  | "cost"
  | "revenue"
  | "payment"
  | "collection";

export interface AttachmentDto {
  id: string;
  objectType: AttachmentObjectType;
  objectId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256Hash: string;
  notes?: string | null;
  uploadedBy?: string | null;
  uploadedAt: string;
  contentUrl: string;
}

export interface PushDeviceDto {
  id: string;
  deviceToken: string;
  platform: "ios" | "android" | "web";
  deviceName?: string | null;
  appVersion?: string | null;
  isActive: boolean;
  lastSeenAt: string;
}

export interface DashboardCurrencyTotalsDto {
  currencyCode: string;
  costBestAvailable?: number | null;
  revenueBestAvailable?: number | null;
  profitBestAvailable?: number | null;
}

export interface DashboardBaseCurrencyRollUpDto {
  baseCurrency: string;
  costBestAvailableBase?: number | null;
  revenueBestAvailableBase?: number | null;
  profitBestAvailableBase?: number | null;
  fxStubNote: string;
}

export interface DashboardMaturityPipelineDto {
  costExpectedOnlyCount: number;
  costConfirmedOnlyCount: number;
  costActualCount: number;
  revenueExpectedOnlyCount: number;
  revenueConfirmedOnlyCount: number;
  revenueActualCount: number;
}

export interface DashboardSummaryDto {
  asOfTimestamp: string;
  billCount: number;
  openExceptionCount: number;
  pendingApprovalCount: number;
  openCloseCount: number;
  openVarianceCount: number;
  overdueExceptionCount: number;
  totalsByCurrency: DashboardCurrencyTotalsDto[];
  baseCurrencyRollUp?: DashboardBaseCurrencyRollUpDto | null;
  hasMixedCurrencies: boolean;
  note: string;
  openReconciliationCount: number;
  unmatchedBankFeedCount: number;
  maturityPipeline?: DashboardMaturityPipelineDto | null;
}

export interface BillListItemDto {
  id: string;
  billNo: string;
  billType: string;
  operationalStatus: string;
  transportMode?: string | null;
  originCode?: string | null;
  destinationCode?: string | null;
  customerReference?: string | null;
  masterBillNo?: string | null;
  etdAt?: string | null;
  etaAt?: string | null;
  rowVersion: string;
  createdAt: string;
}

export interface OperationalMeasurementSummaryDto {
  grossWeightKg?: number | null;
  volumeCbm?: number | null;
  chargeableWeightKg?: number | null;
  chargeableWeightBasis?: string | null;
  isChargeableWeightConfirmed: boolean;
  packageCount?: number | null;
  containerCount?: number | null;
  rowVersion?: string | null;
}

export interface CostItemDto {
  id: string;
  billId?: string | null;
  billNo?: string | null;
  attributionType: "direct" | "shared";
  financialMaturity: "expected" | "confirmed" | "actual";
  expectedAmount: number;
  confirmedAmount?: number | null;
  actualAmount?: number | null;
  amount: number;
  currencyCode: string;
  reportingCurrencyCode?: string | null;
  baseAmount?: number | null;
  fxRate?: number | null;
  fxStatus: string;
  costTypeCode?: string | null;
  vendorPartyId?: string | null;
  vendorName?: string | null;
  approvalStatus: string;
  effectiveDate: string;
  rowVersion: string;
}

export interface RevenueItemDto {
  id: string;
  billId: string;
  billNo?: string | null;
  financialMaturity: "expected" | "confirmed" | "actual";
  expectedAmount: number;
  confirmedAmount?: number | null;
  actualAmount?: number | null;
  amount: number;
  currencyCode: string;
  reportingCurrencyCode?: string | null;
  baseAmount?: number | null;
  fxRate?: number | null;
  fxStatus: string;
  revenueTypeCode?: string | null;
  customerPartyId?: string | null;
  customerName?: string | null;
  approvalStatus: string;
  effectiveDate: string;
  rowVersion: string;
}

export interface ApprovalQueueItemDto {
  id: string;
  objectType: string;
  objectId: string;
  objectDisplayRef?: string | null;
  actionType: string;
  requiredLevel: number;
  status: string;
  requestedBy?: string | null;
  requestedAt: string;
  reason?: string | null;
  amount?: number | null;
  currencyCode?: string | null;
  beforeSummary?: string | null;
  afterSummary?: string | null;
  rowVersion: string;
}

export interface ExceptionQueueItemDto {
  id: string;
  objectType: string;
  objectId: string;
  exceptionType: string;
  severity: "low" | "medium" | "high" | "critical";
  status: string;
  message: string;
  dueAt?: string | null;
  isOverdue: boolean;
  rowVersion: string;
  createdAt: string;
}

export interface ApArItemDto {
  id: string;
  documentId?: string | null;
  documentNo?: string | null;
  partyId: string;
  partyName?: string | null;
  currencyCode: string;
  recognizedAmount: number;
  settledAmount: number;
  writtenOffAmount: number;
  remainingBalance: number;
  settlementStatus: string;
  dueDate?: string | null;
  rowVersion: string;
}

/**
 * Formats a money amount with its ISO 4217 currency code.
 * Never mixes currencies or strips the currency code (Golden Rule #4).
 */
export function formatMoney(
  amount: number | null | undefined,
  currencyCode: string = "VND"
): string {
  if (amount === null || amount === undefined || Number.isNaN(amount)) {
    return "—";
  }
  const code = (currencyCode || "VND").toUpperCase();
  const isZeroDecimal = code === "VND" || code === "JPY" || code === "KRW";
  const formatted = new Intl.NumberFormat("vi-VN", {
    minimumFractionDigits: isZeroDecimal ? 0 : 2,
    maximumFractionDigits: isZeroDecimal ? 0 : 2,
  }).format(amount);
  return `${formatted} ${code}`;
}

/**
 * Generates a deterministic/UUID v4 Idempotency-Key for money mutations and offline outbox replay.
 */
export function createIdempotencyKey(prefix: string = "mob"): string {
  const rand = "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
  return `${prefix}-${rand}`;
}

export interface ParsedApiError {
  status: number;
  code: string;
  messageVi: string;
  isConcurrencyConflict: boolean;
  isRatingNotReady: boolean;
  fieldErrors?: Record<string, string[]>;
}

export function parseApiError(status: number, body: unknown): ParsedApiError {
  const obj = (body && typeof body === "object" ? body : {}) as Record<string, unknown>;
  const code = typeof obj.code === "string" ? obj.code : typeof obj.error === "string" ? obj.error : `http_${status}`;
  const isConcurrencyConflict = status === 409 && (code === "concurrency_conflict" || String(obj.detail ?? "").includes("rowVersion"));
  const isRatingNotReady = status === 409 && code === "rating_not_ready";

  let messageVi =
    (typeof obj.detail === "string" && obj.detail) ||
    (typeof obj.message === "string" && obj.message) ||
    (typeof obj.title === "string" && obj.title) ||
    "Đã xảy ra lỗi khi kết nối máy chủ.";

  if (isConcurrencyConflict) {
    messageVi = "Dữ liệu đã thay đổi bởi người dùng khác (xung đột phiên bản rowVersion). Vui lòng tải lại trước khi thực hiện.";
  } else if (isRatingNotReady) {
    messageVi = "Bill chưa đủ điều kiện tính cước tự động (CW chưa được xác nhận hoặc thiếu thông tin tuyến/đối tác).";
  } else if (status === 401) {
    messageVi = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  } else if (status === 403) {
    messageVi = typeof obj.detail === "string" ? obj.detail : "Bạn không có quyền thực hiện thao tác này (Kiểm soát phân tách nhiệm vụ Chi phí ≠ Doanh thu).";
  }

  return {
    status,
    code,
    messageVi,
    isConcurrencyConflict,
    isRatingNotReady,
    fieldErrors: (obj.errors && typeof obj.errors === "object" ? (obj.errors as Record<string, string[]>) : undefined),
  };
}

