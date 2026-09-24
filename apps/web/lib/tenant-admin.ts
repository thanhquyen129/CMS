import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type {
  AccessUser,
  AuditEventItem,
  InAppNotification,
  IntegrationRecordItem,
  NotificationSettings,
  SampleDataStatus,
  TenantBackupItem,
  TenantLicense,
  TenantProfile,
  TenantReadiness,
} from "./tenant-admin-model";

export * from "./tenant-admin-model";

async function apiGet<T>(path: string): Promise<ApiResult<T>> {
  const token = await getSessionToken();
  if (!token) {
    redirect("/login");
  }

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
      cache: "no-store",
    });
    if (res.status === 401) redirect("/login");
    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message: body.message || "Không tải được dữ liệu hệ thống.",
      };
    }
    return { ok: true, data: (await res.json()) as T };
  } catch {
    return { ok: false, status: 0, message: "Không kết nối được máy chủ API. Thử lại sau." };
  }
}

export function getTenantProfile() {
  return apiGet<TenantProfile>("/api/tenant-profile");
}

export function getTenantReadiness() {
  return apiGet<TenantReadiness>("/api/tenant-profile/readiness");
}

export function getTenantLicense() {
  return apiGet<TenantLicense>("/api/tenant-license");
}

export function getNotificationSettings() {
  return apiGet<NotificationSettings>("/api/notifications/settings");
}

export function listInbox(unreadOnly = false) {
  return apiGet<InAppNotification[]>(
    `/api/notifications/inbox?unreadOnly=${unreadOnly ? "true" : "false"}&take=50`
  );
}

export function listTenantBackups() {
  return apiGet<TenantBackupItem[]>("/api/tenant-backups");
}

export function getSampleDataStatus() {
  return apiGet<SampleDataStatus>("/api/sample-data");
}

export function listAuditEvents(qs: string) {
  return apiGet<AuditEventItem[]>(`/api/audit-events${qs ? `?${qs}` : ""}`);
}

export function listAdminUsers() {
  return apiGet<AccessUser[]>("/api/users");
}

export function listIntegrationRecords() {
  return apiGet<IntegrationRecordItem[]>("/api/integration-records?take=50");
}
