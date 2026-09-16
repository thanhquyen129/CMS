import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type {
  AccountsPayableItem,
  AccountsReceivableItem,
  AgingSummary,
  PayableExposureItem,
  ReceivableExposureItem,
} from "./ap-ar-shared";

export type {
  AccountsPayableItem,
  AccountsReceivableItem,
  AgingBucketSummary,
  AgingReport,
  AgingSummary,
  ApArStatusFilter,
  PayableExposureItem,
  ReceivableExposureItem,
} from "./ap-ar-shared";
export {
  agingBucketLabel,
  exposureStatusLabel,
  filterByApArStatus,
  isOutstanding,
  isSettled,
  parseApArStatusFilter,
  settlementStatusLabel,
} from "./ap-ar-shared";

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

export function getAgingSummary(opts?: {
  asOf?: string;
  includeSettled?: boolean;
}): Promise<ApiResult<AgingSummary>> {
  const params = new URLSearchParams();
  if (opts?.asOf) params.set("asOf", opts.asOf);
  if (opts?.includeSettled) params.set("includeSettled", "true");
  const qs = params.toString() ? `?${params}` : "";
  return apiGet<AgingSummary>(`/api/aging/summary${qs}`);
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

export function getPayableExposure(
  id: string
): Promise<ApiResult<PayableExposureItem>> {
  return apiGet<PayableExposureItem>(
    `/api/payable-exposures/${encodeURIComponent(id)}`
  );
}

export function getReceivableExposure(
  id: string
): Promise<ApiResult<ReceivableExposureItem>> {
  return apiGet<ReceivableExposureItem>(
    `/api/receivable-exposures/${encodeURIComponent(id)}`
  );
}
