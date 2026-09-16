import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { CollectionItem, PaymentItem } from "./settlements-shared";

export type {
  CollectionAllocationItem,
  CollectionItem,
  PaymentAllocationItem,
  PaymentItem,
} from "./settlements-shared";
export {
  allocationStatusLabel,
  canReverseAllocation,
  isDraftAllocation,
  isFinalizedAllocation,
  settlementBillLinkLabel,
} from "./settlements-shared";

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
