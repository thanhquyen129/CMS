import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type PaymentAllocationItem = {
  id: string;
  paymentId: string;
  accountsPayableId: string;
  amount: number;
  currencyCode: string;
  baseAmount: number | null;
  fxRateId: string | null;
  allocationStatus: string;
  finalizedAt: string | null;
  reversedAt: string | null;
  reverseReason: string | null;
  notes: string | null;
};

export type PaymentItem = {
  id: string;
  amount: number;
  baseAmount: number | null;
  fxRateId: string | null;
  appliedAmount: number;
  allocatedAmount: number;
  unappliedAmount: number;
  availableToAllocate: number;
  currencyCode: string;
  valueDate: string;
  counterpartyId: string | null;
  billId: string | null;
  billNo: string | null;
  referenceNo: string | null;
  notes: string | null;
  status: string;
  recordStatus: string;
  allocations: PaymentAllocationItem[];
};

export type CollectionAllocationItem = {
  id: string;
  collectionId: string;
  accountsReceivableId: string;
  amount: number;
  currencyCode: string;
  baseAmount: number | null;
  fxRateId: string | null;
  allocationStatus: string;
  finalizedAt: string | null;
  reversedAt: string | null;
  reverseReason: string | null;
  notes: string | null;
};

export type CollectionItem = {
  id: string;
  amount: number;
  baseAmount: number | null;
  fxRateId: string | null;
  appliedAmount: number;
  allocatedAmount: number;
  unappliedAmount: number;
  availableToAllocate: number;
  currencyCode: string;
  valueDate: string;
  counterpartyId: string | null;
  billId: string | null;
  billNo: string | null;
  referenceNo: string | null;
  notes: string | null;
  status: string;
  recordStatus: string;
  allocations: CollectionAllocationItem[];
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

export function listPayments(): Promise<ApiResult<PaymentItem[]>> {
  return apiGet<PaymentItem[]>("/api/payments");
}

export function getPayment(id: string): Promise<ApiResult<PaymentItem>> {
  return apiGet<PaymentItem>(`/api/payments/${id}`);
}

export function listCollections(): Promise<ApiResult<CollectionItem[]>> {
  return apiGet<CollectionItem[]>("/api/collections");
}

export function getCollection(id: string): Promise<ApiResult<CollectionItem>> {
  return apiGet<CollectionItem>(`/api/collections/${id}`);
}

export function allocationStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return term(terms, "ALLOCATION_DRAFT", "Phân bổ nháp");
    case "finalized":
      return term(terms, "ALLOCATION_FINALIZED", "Đã chốt phân bổ");
    case "reversed":
      return term(terms, "ALLOCATION_REVERSED", "Đã đảo phân bổ");
    default:
      return status || "—";
  }
}

export function isDraftAllocation(status: string): boolean {
  return status?.toLowerCase() === "draft";
}

export function isFinalizedAllocation(status: string): boolean {
  return status?.toLowerCase() === "finalized";
}

/** Draft or finalized — API accepts reverse with reason (not already reversed). */
export function canReverseAllocation(status: string): boolean {
  const s = status?.toLowerCase();
  return s === "draft" || s === "finalized";
}

/** Link label for settlement ↔ Bill: prefer billNo (G6). */
export function settlementBillLinkLabel(
  billId: string | null | undefined,
  billNo: string | null | undefined,
  billLabel = "Bill"
): string | null {
  if (!billId) return null;
  if (billNo?.trim()) return billNo.trim();
  return `${billLabel} ${billId.slice(0, 8)}…`;
}
