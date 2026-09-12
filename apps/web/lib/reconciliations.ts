import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type ReconciliationDetailItem = {
  id: string;
  reconciliationId: string;
  sourceType: string;
  sourceId: string;
  targetType: string | null;
  targetId: string | null;
  sourceAmount: number;
  targetAmount: number;
  matchedAmount: number;
  varianceAmount: number;
  currencyCode: string;
  lineStatus: string;
  varianceId: string | null;
  notes: string | null;
};

export type ReconciliationItem = {
  id: string;
  reconciliationType: string;
  ruleCode: string | null;
  versionNo: number;
  status: string;
  billId: string | null;
  notes: string | null;
  startedAt: string | null;
  startedBy: string | null;
  completedAt: string | null;
  completedBy: string | null;
  details: ReconciliationDetailItem[];
};

export type ReconciliationQueueItem = {
  id: string;
  reconciliationType: string;
  ruleCode: string | null;
  versionNo: number;
  status: string;
  billId: string | null;
  notes: string | null;
  startedAt: string | null;
  queueLabel: string;
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

export function listReconciliations(opts?: {
  status?: string;
}): Promise<ApiResult<ReconciliationItem[]>> {
  const qs = opts?.status ? `?status=${encodeURIComponent(opts.status)}` : "";
  return apiGet<ReconciliationItem[]>(`/api/reconciliations${qs}`);
}

export function getReconciliation(
  id: string
): Promise<ApiResult<ReconciliationItem>> {
  return apiGet<ReconciliationItem>(`/api/reconciliations/${id}`);
}

export function listReconciliationQueue(opts?: {
  status?: string;
}): Promise<ApiResult<ReconciliationQueueItem[]>> {
  const qs = opts?.status ? `?status=${encodeURIComponent(opts.status)}` : "";
  return apiGet<ReconciliationQueueItem[]>(`/api/queues/reconciliations${qs}`);
}

export function reconciliationStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status.toLowerCase()) {
    case "draft":
      return "Nháp";
    case "in_progress":
      return "Đang đối soát";
    case "completed":
      return "Đã hoàn tất";
    case "cancelled":
      return "Đã hủy";
    default:
      return status;
  }
}

export function reconciliationTypeLabel(
  terms: TerminologyMap,
  type: string
): string {
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  switch (type.toLowerCase()) {
    case "manual":
      return `${reconLabel} thủ công`;
    case "payment_ap":
      return "Thanh toán ↔ Phải trả";
    case "collection_ar":
      return "Thu tiền ↔ Phải thu";
    case "document":
      return term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
    case "cost_revenue":
      return "Chi phí ↔ Doanh thu";
    default:
      return type;
  }
}

export function reconObjectTypeLabel(
  terms: TerminologyMap,
  objectType: string | null | undefined
): string {
  if (!objectType) return "—";
  const key = objectType.trim().toLowerCase();
  const map: Record<string, string> = {
    payment: term(terms, "PAYMENT", "Thanh toán"),
    collection: term(terms, "COLLECTION", "Thu tiền"),
    cost: term(terms, "COST", "Chi phí"),
    revenue: term(terms, "REVENUE", "Doanh thu"),
    document: term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính"),
    accounts_payable: term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả"),
    accounts_receivable: term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu"),
    bank_line: term(terms, "BANK_FEED_LINE", "Dòng sao kê"),
    other: "Khác",
  };
  return map[key] ?? objectType;
}

export function lineStatusLabel(status: string): string {
  switch (status.toLowerCase()) {
    case "matched":
      return "Khớp";
    case "variance":
      return "Có chênh lệch";
    case "unmatched":
      return "Chưa khớp";
    default:
      return status;
  }
}

export function canEditReconciliation(status: string): boolean {
  const s = status?.toLowerCase();
  return s === "draft" || s === "in_progress";
}
