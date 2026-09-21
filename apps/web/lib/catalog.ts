import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export { CATALOG_KINDS, catalogKindLabel, locationClassLabel } from "./catalog-kinds";

export type MasterCatalogItem = {
  id: string;
  kind: string;
  code: string;
  name: string;
  description: string | null;
  attributesJson: string | null;
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
