import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { CostDto, CostListItem, RevenueListItem } from "./costs-revenues";

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

export function listCostsByBill(
  billId: string
): Promise<ApiResult<CostListItem[]>> {
  return apiGet<CostListItem[]>(
    `/api/costs?billId=${encodeURIComponent(billId)}`
  );
}

export function listSharedCosts(): Promise<ApiResult<CostListItem[]>> {
  return apiGet<CostListItem[]>(
    `/api/costs?attributionType=${encodeURIComponent("shared")}`
  );
}

export function getCost(id: string): Promise<ApiResult<CostDto>> {
  return apiGet<CostDto>(`/api/costs/${encodeURIComponent(id)}`);
}

export function listRevenuesByBill(
  billId: string
): Promise<ApiResult<RevenueListItem[]>> {
  return apiGet<RevenueListItem[]>(
    `/api/revenues?billId=${encodeURIComponent(billId)}`
  );
}
