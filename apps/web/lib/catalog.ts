import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type MasterCatalogItem = {
  id: string;
  kind: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
};

export type FxRateItem = {
  id: string;
  fromCurrencyCode: string;
  toCurrencyCode: string;
  rateDate: string;
  rate: number;
  source: string;
  version: number;
  note: string | null;
  createdAt: string;
};

export const CATALOG_KINDS: { id: string; label: string }[] = [
  { id: "cost_type", label: "Loại chi phí" },
  { id: "revenue_type", label: "Loại doanh thu" },
  { id: "service_type", label: "Loại dịch vụ" },
  { id: "pricing_component", label: "Thành phần giá" },
  { id: "document_type", label: "Loại chứng từ" },
  { id: "payment_term", label: "Điều khoản thanh toán" },
];

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
        message: body.message || "Không tải được danh mục.",
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

export function listCatalog(kind?: string): Promise<ApiResult<MasterCatalogItem[]>> {
  const qs = kind ? `?kind=${encodeURIComponent(kind)}` : "";
  return apiGet<MasterCatalogItem[]>(`/api/master-catalog${qs}`);
}

export function listFxRates(): Promise<ApiResult<FxRateItem[]>> {
  return apiGet<FxRateItem[]>("/api/fx-rates");
}

export function catalogKindLabel(kind: string): string {
  return CATALOG_KINDS.find((k) => k.id === kind)?.label ?? kind;
}
