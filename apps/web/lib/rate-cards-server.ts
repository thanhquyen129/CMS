import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type {
  PricingRule,
  RateCard,
  RateVersion,
  Rating,
  RatingHistoryItem,
} from "./rate-cards";

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

export function listRateCards(): Promise<ApiResult<RateCard[]>> {
  return apiGet<RateCard[]>("/api/rate-cards");
}

export function getRateCard(id: string): Promise<ApiResult<RateCard>> {
  return apiGet<RateCard>(`/api/rate-cards/${encodeURIComponent(id)}`);
}

export function listRateVersions(
  rateCardId: string
): Promise<ApiResult<RateVersion[]>> {
  return apiGet<RateVersion[]>(
    `/api/rate-cards/${encodeURIComponent(rateCardId)}/versions`
  );
}

export function listPricingRules(
  versionId: string
): Promise<ApiResult<PricingRule[]>> {
  return apiGet<PricingRule[]>(
    `/api/rate-versions/${encodeURIComponent(versionId)}/rules`
  );
}

export function listRatingsByBill(
  billId: string
): Promise<ApiResult<RatingHistoryItem[]>> {
  return apiGet<RatingHistoryItem[]>(
    `/api/bills/${encodeURIComponent(billId)}/ratings`
  );
}

export function getRating(id: string): Promise<ApiResult<Rating>> {
  return apiGet<Rating>(`/api/ratings/${encodeURIComponent(id)}`);
}
