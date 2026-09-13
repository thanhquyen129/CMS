import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { BusinessParty } from "./party";

export type OrganizationItem = {
  id: string;
  code: string;
  name: string;
  parentId: string | null;
  isActive: boolean;
  createdAt: string;
};

export type CurrencyItem = {
  id: string;
  code: string;
  name: string;
  decimalPlaces: number;
  isActive: boolean;
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
            ? "Bạn không có quyền xem danh mục."
            : "Không tải được danh mục."),
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

export function listOrganizations(): Promise<ApiResult<OrganizationItem[]>> {
  return apiGet<OrganizationItem[]>("/api/organizations");
}

export function listCurrencies(): Promise<ApiResult<CurrencyItem[]>> {
  return apiGet<CurrencyItem[]>("/api/currencies");
}

export function listAdminParties(): Promise<ApiResult<BusinessParty[]>> {
  return apiGet<BusinessParty[]>("/api/business-parties");
}
