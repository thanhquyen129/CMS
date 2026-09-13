import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type DashboardCurrencyTotals = {
  currencyCode: string;
  costBestAvailable: number | null;
  revenueBestAvailable: number | null;
  profitBestAvailable: number | null;
};

export type DashboardBaseCurrencyRollUp = {
  baseCurrency: string;
  costBestAvailableBase: number | null;
  revenueBestAvailableBase: number | null;
  profitBestAvailableBase: number | null;
  fxStubNote: string;
};

export type DashboardFinancialVisibility = {
  canViewCost: boolean;
  canViewRevenue: boolean;
  canViewMargin: boolean;
};

export type DashboardDocumentCluster = {
  awaitingAcceptanceCount: number;
  acceptedUnmatchedCount: number;
  draftMatchCount: number;
};

export type DashboardApArCluster = {
  openAccountsPayableCount: number;
  openAccountsReceivableCount: number;
  openPayableExposureCount: number;
  openReceivableExposureCount: number;
};

export type DashboardSettlementCluster = {
  openPaymentCount: number;
  openCollectionCount: number;
};

export type DashboardMaturityPipeline = {
  costExpectedOnlyCount: number;
  costConfirmedOnlyCount: number;
  costActualCount: number;
  revenueExpectedOnlyCount: number;
  revenueConfirmedOnlyCount: number;
  revenueActualCount: number;
};

export type DashboardSummary = {
  asOfTimestamp: string;
  billCount: number;
  openExceptionCount: number;
  pendingApprovalCount: number;
  openCloseCount: number;
  openVarianceCount: number;
  overdueExceptionCount: number;
  totalsByCurrency: DashboardCurrencyTotals[];
  baseCurrencyRollUp: DashboardBaseCurrencyRollUp | null;
  hasMixedCurrencies: boolean;
  note: string;
  openReconciliationCount: number;
  unmatchedBankFeedCount: number;
  documents: DashboardDocumentCluster | null;
  apAr: DashboardApArCluster | null;
  settlements: DashboardSettlementCluster | null;
  maturityPipeline: DashboardMaturityPipeline | null;
  financialVisibility: DashboardFinancialVisibility | null;
};

export type ExceptionQueueItem = {
  id: string;
  ruleCode: string;
  severity: string;
  ownerId: string | null;
  status: string;
  dueAt: string | null;
  title: string;
  description: string | null;
  billId: string | null;
  reconciliationId: string | null;
  varianceId: string | null;
  objectType: string | null;
  objectId: string | null;
  resolvedAt: string | null;
  resolvedBy: string | null;
  resolutionNotes: string | null;
  closedAt: string | null;
  closedBy: string | null;
  escalatedAt: string | null;
  escalatedBy: string | null;
  escalationReason: string | null;
};

export type ApprovalQueueItem = {
  id: string;
  objectType: string;
  objectId: string;
  status: string;
  requiredLevel: number;
  currentLevel: number;
  requestedBy: string | null;
  requestedAt: string;
  requestReason: string | null;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionReason: string | null;
  notes: string | null;
};

async function apiGet<T>(path: string): Promise<ApiResult<T>> {
  const token = await getSessionToken();
  if (!token) {
    redirect("/login");
  }

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (res.status === 401) {
      redirect("/login");
    }

    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message:
          body.message ||
          (res.status === 403
            ? "Bạn không có quyền xem dữ liệu này."
            : "Không tải được dữ liệu từ máy chủ."),
      };
    }

    return { ok: true, data: (await res.json()) as T };
  } catch {
    return {
      ok: false,
      status: 0,
      message: "Không kết nối được máy chủ API. Thử lại sau.",
    };
  }
}

export function getDashboardSummary(): Promise<ApiResult<DashboardSummary>> {
  return apiGet<DashboardSummary>("/api/dashboard/summary");
}

export function listExceptionQueue(opts?: {
  overdueOnly?: boolean;
}): Promise<ApiResult<ExceptionQueueItem[]>> {
  const qs =
    opts?.overdueOnly === true ? "?overdueOnly=true" : "";
  return apiGet<ExceptionQueueItem[]>(`/api/queues/exceptions${qs}`);
}

export function listApprovalQueue(): Promise<ApiResult<ApprovalQueueItem[]>> {
  return apiGet<ApprovalQueueItem[]>("/api/queues/approvals");
}

export type VarianceItem = {
  id: string;
  reconciliationId: string | null;
  reconciliationDetailId: string | null;
  varianceType: string;
  amount: number;
  currencyCode: string;
  sourceType: string;
  sourceId: string;
  targetType: string | null;
  targetId: string | null;
  status: string;
  severity: string;
  explanation: string | null;
  exceptionId: string | null;
};

export function listVariances(opts?: {
  status?: string;
}): Promise<ApiResult<VarianceItem[]>> {
  const status = opts?.status?.trim() || "open";
  const qs = `?status=${encodeURIComponent(status)}`;
  return apiGet<VarianceItem[]>(`/api/variances${qs}`);
}

export function varianceStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status.toLowerCase()) {
    case "open":
      return term(terms, "VARIANCE_OPEN", "Chênh lệch đang mở");
    case "accepted":
      return "Đã chấp nhận";
    case "written_off":
      return "Đã xóa nợ chênh lệch";
    case "cleared":
      return "Đã xóa / khớp";
    default:
      return status;
  }
}

export function varianceTypeLabel(varianceType: string): string {
  switch (varianceType.toLowerCase()) {
    case "amount":
      return "Số tiền";
    case "quantity":
      return "Số lượng";
    case "rate":
      return "Đơn giá";
    default:
      return varianceType || "—";
  }
}

/** Resolve object type CodeKey → Vietnamese UI term (CP6.5). */
export function objectTypeLabel(
  terms: TerminologyMap,
  objectType: string | null | undefined
): string {
  if (!objectType) return "—";
  const key = objectType.trim().toUpperCase();
  const map: Record<string, string> = {
    BILL: term(terms, "BILL", "Bill"),
    COST: term(terms, "COST", "Chi phí"),
    REVENUE: term(terms, "REVENUE", "Doanh thu"),
    VARIANCE: term(terms, "VARIANCE", "Chênh lệch"),
    RECONCILIATION: term(terms, "RECONCILIATION", "Đối soát"),
    FINANCIAL_DOCUMENT: term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính"),
    DOCUMENT: term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính"),
    PAYMENT: term(terms, "PAYMENT", "Thanh toán"),
    COLLECTION: term(terms, "COLLECTION", "Thu tiền"),
    EXCEPTION: term(terms, "EXCEPTION", "Ngoại lệ"),
    ACCOUNTS_PAYABLE: term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả"),
    ACCOUNTS_RECEIVABLE: term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu"),
  };
  return map[key] ?? objectType;
}

export function exceptionStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status.toLowerCase()) {
    case "open":
      return term(terms, "EXCEPTION_OPEN", "Ngoại lệ mở");
    case "in_progress":
      return "Đang xử lý";
    case "escalated":
      return term(terms, "EXCEPTION_ESCALATED", "Ngoại lệ đã leo thang");
    case "resolved":
      return term(terms, "EXCEPTION_RESOLVED", "Ngoại lệ đã xử lý");
    case "closed":
      return term(terms, "EXCEPTION_CLOSED", "Ngoại lệ đã đóng");
    default:
      return status;
  }
}

export function approvalStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status.toLowerCase()) {
    case "pending":
      return term(terms, "APPROVAL_PENDING", "Chờ phê duyệt");
    case "approved":
      return term(terms, "APPROVAL_APPROVED", "Đã phê duyệt");
    case "rejected":
      return term(terms, "APPROVAL_REJECTED", "Từ chối phê duyệt");
    default:
      return status;
  }
}

/** Severity is CodeKey — map to Vietnamese; avoid leaking English enums. */
export function severityLabel(severity: string): string {
  switch (severity.toLowerCase()) {
    case "critical":
      return "Nghiêm trọng";
    case "high":
      return "Cao";
    case "medium":
      return "Trung bình";
    case "low":
      return "Thấp";
    default:
      return severity;
  }
}

export function isOverdue(dueAt: string | null | undefined, now = Date.now()): boolean {
  if (!dueAt) return false;
  const t = new Date(dueAt).getTime();
  return !Number.isNaN(t) && t < now;
}

/** Best href when we can deep-link to an operable UI screen. */
export function billHrefFromException(item: ExceptionQueueItem): string | null {
  if (item.billId) return `/bills/${item.billId}`;
  if (item.objectType?.toLowerCase() === "bill" && item.objectId) {
    return `/bills/${item.objectId}`;
  }
  return null;
}

export function billHrefFromApproval(item: ApprovalQueueItem): string | null {
  if (item.objectType.toLowerCase() === "bill") {
    return `/bills/${item.objectId}`;
  }
  return null;
}

/** Deep-link when U4+ has a detail screen for the object type. */
export function objectHrefFromApproval(item: ApprovalQueueItem): string | null {
  const t = item.objectType.toLowerCase();
  if (t === "bill") return `/bills/${item.objectId}`;
  if (t === "financial_document" || t === "document") {
    return `/documents/${item.objectId}`;
  }
  if (t === "payment") return `/settlements/payments/${item.objectId}`;
  if (t === "collection") return `/settlements/collections/${item.objectId}`;
  if (t === "accounts_payable" || t === "accounts_receivable") {
    return "/ap-ar";
  }
  return null;
}

/** Pending approval can still be decided from the queue. */
export function isPendingApproval(status: string): boolean {
  return status?.toLowerCase() === "pending";
}

/** Open-ish exception statuses still shown on the open queue. */
export function canActOnException(status: string): boolean {
  const s = status?.toLowerCase();
  return (
    s === "open" ||
    s === "in_progress" ||
    s === "escalated" ||
    s === "resolved"
  );
}
