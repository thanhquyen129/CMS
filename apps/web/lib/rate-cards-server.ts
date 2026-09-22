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
import { unwrapPaged, type PagedResult } from "./paging";

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

export function listRateCards(opts?: {
  q?: string;
  partyType?: string;
  transportMode?: string;
  active?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<ApiResult<PagedResult<RateCard>>> {
  const p = new URLSearchParams();
  if (opts?.q) p.set("q", opts.q);
  if (opts?.partyType) p.set("partyType", opts.partyType);
  if (opts?.transportMode) p.set("transportMode", opts.transportMode);
  if (opts?.active === true) p.set("isActive", "true");
  if (opts?.active === false) p.set("isActive", "false");
  if (opts?.page != null) p.set("page", String(opts.page));
  if (opts?.pageSize != null) p.set("pageSize", String(opts.pageSize));
  const qs = p.toString();
  return apiGet<RateCard[] | PagedResult<RateCard>>(
    qs ? `/api/rate-cards?${qs}` : "/api/rate-cards"
  ).then((r) => (r.ok ? { ok: true, data: unwrapPaged(r.data) } : r));
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

export type SurchargeRow = {
  id: string;
  code: string;
  name: string;
  calcMethod: string | null;
  amount: number;
  currencyCode: string;
  transportMode: string | null;
  cardCode: string;
  versionNo: number;
  versionStatus: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
};

export type AppendixRow = {
  id: string;
  rateCardId: string;
  cardCode: string;
  cardName: string;
  note: string | null;
  effectiveFrom: string | null;
  versionNo: number;
  status: string;
};

export type RatingHistoryRow = {
  id: string;
  ratedAt: string;
  billId: string;
  cardCode: string | null;
  versionNo: number | null;
  status: string;
  totalAmount: number;
  currencyCode: string;
  contextJson: string | null;
};

export function listSurcharges(): Promise<ApiResult<SurchargeRow[]>> {
  return apiGet<SurchargeRow[]>("/api/surcharges");
}

export function listAppendices(): Promise<ApiResult<AppendixRow[]>> {
  return apiGet<AppendixRow[]>("/api/rate-appendices");
}

export function listRatingHistory(): Promise<ApiResult<RatingHistoryRow[]>> {
  return apiGet<RatingHistoryRow[]>("/api/rating-history");
}
