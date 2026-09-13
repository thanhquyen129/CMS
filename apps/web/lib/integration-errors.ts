import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type IntegrationErrorItem = {
  id: string;
  integrationRecordId: string;
  errorCode: string;
  message: string;
  detail: string | null;
  attemptNo: number;
  occurredAt: string;
  nextRetryAt: string | null;
  recoveryStatus: string;
  recoveredAt: string | null;
  recoveryNote: string | null;
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
            ? "Bạn không có quyền xem lỗi tích hợp."
            : "Không tải được danh sách lỗi tích hợp."),
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

export function listIntegrationErrors(opts?: {
  recoveryStatus?: string;
  take?: number;
}): Promise<ApiResult<IntegrationErrorItem[]>> {
  const params = new URLSearchParams();
  if (opts?.recoveryStatus) {
    params.set("recoveryStatus", opts.recoveryStatus);
  }
  if (opts?.take) {
    params.set("take", String(opts.take));
  }
  const qs = params.size > 0 ? `?${params.toString()}` : "";
  return apiGet<IntegrationErrorItem[]>(`/api/integration-errors${qs}`);
}

export function integrationRecoveryStatusLabel(status: string): string {
  switch (status.toLowerCase()) {
    case "pending":
      return "Chờ xử lý";
    case "retried":
      return "Đã thử lại";
    case "dead_letter":
      return "Dead letter";
    default:
      return status;
  }
}
