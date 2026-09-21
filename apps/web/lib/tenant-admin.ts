import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type TenantProfile = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  legalName: string | null;
  taxId: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  ward: string | null;
  district: string | null;
  city: string | null;
  province: string | null;
  countryCode: string | null;
  postalCode: string | null;
  timeZoneId: string;
  dateFormat: string;
  defaultCurrencyCode: string;
  hasLogo: boolean;
};

export type LicenseModule = {
  code: string;
  name: string;
  includedInPlan: boolean;
  isEnabled: boolean;
};

export type TenantLicense = {
  id: string;
  planCode: string;
  planName: string;
  seatLimit: number;
  seatsUsed: number;
  validFrom: string;
  validUntil: string;
  status: string;
  notes: string | null;
  modules: LicenseModule[];
};

export type NotificationEventPref = {
  code: string;
  name: string;
  inApp: boolean;
  email: boolean;
};

export type NotificationSettings = {
  inAppEnabled: boolean;
  emailEnabled: boolean;
  smtpConfigured: boolean;
  events: NotificationEventPref[];
};

export type InAppNotification = {
  id: string;
  eventType: string;
  title: string;
  body: string;
  href: string | null;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
};

export type TenantBackupItem = {
  id: string;
  kind: string;
  status: string;
  byteSize: number;
  checksumSha256: string;
  note: string | null;
  createdAt: string;
  restoredAt: string | null;
};

export type AuditEventItem = {
  id: string;
  actorId: string | null;
  actorDisplayName: string | null;
  action: string;
  objectType: string;
  objectId: string;
  beforeJson: string | null;
  afterJson: string | null;
  reason: string | null;
  correlationId: string | null;
  occurredAt: string;
};

export type AccessUser = {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  organizationId: string | null;
  passwordSet: boolean;
  createdAt: string;
};

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

export function listAuditEvents(qs: string) {
  return apiGet<AuditEventItem[]>(`/api/audit-events${qs ? `?${qs}` : ""}`);
}

export function listAdminUsers() {
  return apiGet<AccessUser[]>("/api/users");
}

export type IntegrationRecordItem = {
  id: string;
  sourceSystem: string;
  externalObjectType: string;
  externalId: string;
  externalVersion: string | null;
  status: string;
  localObjectType: string | null;
  localObjectId: string | null;
  notes: string | null;
  receivedAt: string;
  processedAt: string | null;
  errorCount: number;
};

export function listIntegrationRecords() {
  return apiGet<IntegrationRecordItem[]>("/api/integration-records?take=50");
}

export const TIME_ZONES = [
  { id: "Asia/Ho_Chi_Minh", label: "Asia/Ho_Chi_Minh (UTC+7)" },
  { id: "Asia/Bangkok", label: "Asia/Bangkok (UTC+7)" },
  { id: "Asia/Singapore", label: "Asia/Singapore (UTC+8)" },
  { id: "Asia/Tokyo", label: "Asia/Tokyo (UTC+9)" },
  { id: "UTC", label: "UTC" },
  { id: "Europe/London", label: "Europe/London" },
  { id: "America/New_York", label: "America/New_York" },
] as const;

export const DATE_FORMATS = [
  { id: "dd/MM/yyyy", label: "dd/MM/yyyy (Việt Nam)" },
  { id: "yyyy-MM-dd", label: "yyyy-MM-dd (ISO)" },
  { id: "MM/dd/yyyy", label: "MM/dd/yyyy" },
] as const;

export const AUDIT_ACTION_LABELS: Record<string, string> = {
  "user.create": "Tạo người dùng",
  "user.update": "Cập nhật người dùng",
  "user.password_set": "Đặt mật khẩu",
  "tenant.profile.update": "Cập nhật doanh nghiệp",
  "license.module.update": "Bật/tắt module license",
  "notification.settings.update": "Cài đặt thông báo",
  "backup.create": "Tạo bản sao lưu",
  "backup.restore": "Khôi phục danh mục",
  "cost.create": "Tạo chi phí",
  "cost.confirm": "Xác nhận chi phí",
  "revenue.create": "Tạo doanh thu",
  "financial_document.accept": "Chấp nhận chứng từ",
  "document_match.confirm": "Xác nhận khớp chứng từ",
  "financial_close_snapshot.create": "Tạo bản chốt",
  "business_party.create": "Tạo đối tác",
  "business_party.update": "Cập nhật đối tác",
  "accounts_payable.write_off": "Xóa nợ phải trả",
  "accounts_receivable.write_off": "Xóa nợ phải thu",
  "accounts_payable.recognize": "Ghi nhận phải trả",
  "accounts_receivable.recognize": "Ghi nhận phải thu",
};

export const AUDIT_OBJECT_LABELS: Record<string, string> = {
  user: "Người dùng",
  tenant: "Thuê bao",
  license: "License",
  notification: "Thông báo",
  backup: "Sao lưu",
  cost: "Chi phí",
  revenue: "Doanh thu",
  business_party: "Đối tác",
  financial_document: "Chứng từ",
  financial_close_snapshot: "Bản chốt",
  accounts_payable: "Phải trả",
  accounts_receivable: "Phải thu",
};

export function auditActionLabel(action: string) {
  return AUDIT_ACTION_LABELS[action] ?? action;
}

export function auditObjectLabel(type: string) {
  return AUDIT_OBJECT_LABELS[type] ?? type;
}

export function licenseStatusLabel(status: string) {
  if (status === "active") return "Đang hiệu lực";
  if (status === "expired") return "Hết hạn";
  if (status === "suspended") return "Tạm ngưng";
  return status;
}

export function backupStatusLabel(status: string) {
  if (status === "completed") return "Hoàn tất";
  if (status === "failed") return "Lỗi";
  return status;
}
