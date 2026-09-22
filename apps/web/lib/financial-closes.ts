import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type FinancialCloseSnapshotDetail = {
  id: string;
  snapshotId: string;
  lineNo: number;
  metricKey: string;
  metricValue: number;
  currencyCode: string | null;
  sourceType: string | null;
  sourceId: string | null;
  notes: string | null;
};

export type FinancialCloseSnapshot = {
  id: string;
  financialCloseId: string;
  scopeType: string;
  scopeId: string | null;
  snapshotVersion: number;
  closedAt: string;
  closedBy: string | null;
  policyVersion: string;
  baseCurrency: string;
  immutableHash: string;
  details: FinancialCloseSnapshotDetail[];
};

export type FinancialCloseItem = {
  id: string;
  scopeType: string;
  scopeId: string | null;
  periodFrom: string | null;
  periodTo: string | null;
  versionNo: number;
  status: string;
  policyVersion: string;
  baseCurrency: string;
  notes: string | null;
  startedAt: string | null;
  startedBy: string | null;
  lockedAt: string | null;
  lockedBy: string | null;
  reopenedAt: string | null;
  reopenedBy: string | null;
  reopenReason: string | null;
  supersedesCloseId: string | null;
  snapshots: FinancialCloseSnapshot[];
};

export type FinancialClosePnl = {
  financialCloseId: string;
  snapshotId: string;
  snapshotVersion: number;
  baseCurrency: string;
  closedAt: string;
  immutableHash: string;
  revenueTotal: number;
  costTotal: number;
  profitTotal: number;
  apOutstandingTotal: number;
  arOutstandingTotal: number;
  metrics: {
    metricKey: string;
    metricValue: number;
    currencyCode: string | null;
    sourceType: string | null;
    notes: string | null;
  }[];
  note: string;
};

export type CloseEligibilityGate = {
  code: string;
  label: string;
  passed: boolean;
  failReason: string | null;
};

export type CloseEligibility = {
  financialCloseId: string;
  eligible: boolean;
  gates: CloseEligibilityGate[];
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

export function listFinancialCloses(opts?: {
  status?: string;
  scopeType?: string;
}): Promise<ApiResult<FinancialCloseItem[]>> {
  const qs = new URLSearchParams();
  if (opts?.status) qs.set("status", opts.status);
  if (opts?.scopeType) qs.set("scopeType", opts.scopeType);
  const q = qs.toString();
  return apiGet<FinancialCloseItem[]>(
    `/api/financial-closes${q ? `?${q}` : ""}`
  );
}

export function getFinancialClose(
  id: string
): Promise<ApiResult<FinancialCloseItem>> {
  return apiGet<FinancialCloseItem>(`/api/financial-closes/${id}`);
}

export function getFinancialClosePnl(
  id: string,
  snapshotId?: string
): Promise<ApiResult<FinancialClosePnl>> {
  const qs = snapshotId ? `?snapshotId=${encodeURIComponent(snapshotId)}` : "";
  return apiGet<FinancialClosePnl>(`/api/financial-closes/${id}/pnl${qs}`);
}

export function getFinancialCloseEligibility(
  id: string
): Promise<ApiResult<CloseEligibility>> {
  return apiGet<CloseEligibility>(`/api/financial-closes/${id}/eligibility`);
}

export function closeStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "open":
      return "Đang mở";
    case "locked":
      return term(terms, "CLOSE_LOCKED", "Đã khóa chốt");
    case "reopened":
      return term(terms, "REOPEN_FINANCIAL_CLOSE", "Đã mở lại");
    default:
      return status || "—";
  }
}

export function scopeTypeLabel(
  terms: TerminologyMap,
  scopeType: string
): string {
  switch (scopeType?.toLowerCase()) {
    case "period":
      return "Kỳ";
    case "bill":
      return term(terms, "BILL", "Bill");
    case "tenant":
      return "Thuê bao";
    default:
      return scopeType || "—";
  }
}

export function canSnapshot(status: string): boolean {
  const s = status?.toLowerCase();
  return s === "open" || s === "reopened";
}

export function canReopen(status: string): boolean {
  return status?.toLowerCase() === "locked";
}
