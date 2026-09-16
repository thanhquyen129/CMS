import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type {
  CostDto,
  CostListItem,
  RevenueDto,
  RevenueListItem,
} from "./costs-revenues";

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

export function listCosts(opts?: {
  financialMaturity?: string;
  attributionType?: string;
}): Promise<ApiResult<CostListItem[]>> {
  const p = new URLSearchParams();
  if (opts?.financialMaturity) p.set("financialMaturity", opts.financialMaturity);
  if (opts?.attributionType) p.set("attributionType", opts.attributionType);
  const qs = p.toString();
  return apiGet<CostListItem[]>(qs ? `/api/costs?${qs}` : "/api/costs");
}

export function listSharedCosts(): Promise<ApiResult<CostListItem[]>> {
  return apiGet<CostListItem[]>(
    `/api/costs?attributionType=${encodeURIComponent("shared")}`
  );
}

export function getCost(id: string): Promise<ApiResult<CostDto>> {
  return apiGet<CostDto>(`/api/costs/${encodeURIComponent(id)}`);
}

export function getRevenue(id: string): Promise<ApiResult<RevenueDto>> {
  return apiGet<RevenueDto>(`/api/revenues/${encodeURIComponent(id)}`);
}

export function listRevenuesByBill(
  billId: string
): Promise<ApiResult<RevenueListItem[]>> {
  return apiGet<RevenueListItem[]>(
    `/api/revenues?billId=${encodeURIComponent(billId)}`
  );
}

export function listRevenues(opts?: {
  financialMaturity?: string;
}): Promise<ApiResult<RevenueListItem[]>> {
  const p = new URLSearchParams();
  if (opts?.financialMaturity) p.set("financialMaturity", opts.financialMaturity);
  const qs = p.toString();
  return apiGet<RevenueListItem[]>(qs ? `/api/revenues?${qs}` : "/api/revenues");
}
