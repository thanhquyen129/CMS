import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type AccountsPayableItem = {
  id: string;
  payableExposureId: string;
  recognizedAmount: number;
  adjustmentAmount: number;
  finalizedSettledAmount: number;
  outstanding: number;
  currencyCode: string;
  dueDate: string | null;
  settlementStatus: string;
  billId: string | null;
  counterpartyId: string | null;
  recognizedAt: string;
  notes: string | null;
  recordStatus: string;
  daysPastDue: number | null;
  agingBucket: string;
};

export type AccountsReceivableItem = {
  id: string;
  receivableExposureId: string;
  recognizedAmount: number;
  adjustmentAmount: number;
  finalizedSettledAmount: number;
  outstanding: number;
  currencyCode: string;
  dueDate: string | null;
  settlementStatus: string;
  billId: string | null;
  counterpartyId: string | null;
  recognizedAt: string;
  notes: string | null;
  recordStatus: string;
  daysPastDue: number | null;
  agingBucket: string;
};

export type PayableExposureItem = {
  id: string;
  amount: number;
  recognizedAmount: number;
  openAmount: number;
  currencyCode: string;
  status: string;
  effectiveDate: string;
  dueDate: string | null;
  billId: string | null;
  counterpartyId: string | null;
  costId: string | null;
  financialDocumentId: string | null;
  notes: string | null;
  recordStatus: string;
};

export type ReceivableExposureItem = {
  id: string;
  amount: number;
  recognizedAmount: number;
  openAmount: number;
  currencyCode: string;
  status: string;
  effectiveDate: string;
  dueDate: string | null;
  billId: string | null;
  counterpartyId: string | null;
  revenueId: string | null;
  financialDocumentId: string | null;
  notes: string | null;
  recordStatus: string;
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

export function listAccountsPayable(opts?: {
  settlementStatus?: string;
}): Promise<ApiResult<AccountsPayableItem[]>> {
  const qs = opts?.settlementStatus
    ? `?settlementStatus=${encodeURIComponent(opts.settlementStatus)}`
    : "";
  return apiGet<AccountsPayableItem[]>(`/api/accounts-payable${qs}`);
}

export function listAccountsReceivable(opts?: {
  settlementStatus?: string;
}): Promise<ApiResult<AccountsReceivableItem[]>> {
  const qs = opts?.settlementStatus
    ? `?settlementStatus=${encodeURIComponent(opts.settlementStatus)}`
    : "";
  return apiGet<AccountsReceivableItem[]>(`/api/accounts-receivable${qs}`);
}

export function listPayableExposures(opts?: {
  status?: string;
}): Promise<ApiResult<PayableExposureItem[]>> {
  const qs = opts?.status
    ? `?status=${encodeURIComponent(opts.status)}`
    : "";
  return apiGet<PayableExposureItem[]>(`/api/payable-exposures${qs}`);
}

export function listReceivableExposures(opts?: {
  status?: string;
}): Promise<ApiResult<ReceivableExposureItem[]>> {
  const qs = opts?.status
    ? `?status=${encodeURIComponent(opts.status)}`
    : "";
  return apiGet<ReceivableExposureItem[]>(`/api/receivable-exposures${qs}`);
}

export function settlementStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "open":
      return term(terms, "SETTLEMENT_OPEN", "Chưa tất toán");
    case "partial":
      return term(terms, "SETTLEMENT_PARTIAL", "Tất toán một phần");
    case "settled":
      return term(terms, "SETTLEMENT_SETTLED", "Đã tất toán");
    default:
      return status || "—";
  }
}

export function agingBucketLabel(bucket: string): string {
  switch (bucket?.toLowerCase()) {
    case "no_due_date":
      return "Không hạn";
    case "current":
      return "Trong hạn";
    case "1_30":
      return "1–30 ngày";
    case "31_60":
      return "31–60 ngày";
    case "61_90":
      return "61–90 ngày";
    case "90_plus":
      return "Trên 90 ngày";
    default:
      return bucket || "—";
  }
}

export function exposureStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "open":
      return "Đang mở";
    case "partial":
      return "Ghi nhận một phần";
    case "recognized":
      return "Đã ghi nhận";
    case "closed":
      return "Đã đóng";
    default:
      return status || "—";
  }
}

/** Outstanding only — Cost ≠ Payment; AP/AR ≠ Cost/Revenue. */
export function isOutstanding(
  item: { outstanding: number; settlementStatus: string }
): boolean {
  if (item.outstanding > 0) return true;
  const s = item.settlementStatus?.toLowerCase();
  return s === "open" || s === "partial";
}
