import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";

export type BillListItem = {
  id: string;
  billNo: string;
  billType: string;
  operationalStatus: string;
  isActive: boolean;
  organizationId: string | null;
  createdBy: string | null;
  createdAt: string;
};

export type BillDto = BillListItem & {
  tenantId: string;
  sourceSystem: string | null;
  externalId: string | null;
};

export type MaturityBreakdown = {
  expectedTotal: number;
  confirmedTotal: number;
  actualTotal: number;
};

export type CurrencyFinancialBucket = {
  currencyCode: string;
  revenueBestAvailable: number;
  costBestAvailable: number;
  profitBestAvailable: number;
  directCostBestAvailable: number;
  allocatedCostAmount: number;
  revenueMaturity: MaturityBreakdown;
  directCostMaturity: MaturityBreakdown;
  revenueVarianceExpectedVsActual: number;
  directCostVarianceExpectedVsActual: number;
  profitVarianceExpectedVsActual: number;
  revenueLineCount: number;
  directCostLineCount: number;
  allocatedCostLineCount: number;
};

export type SettlementOutstandingBucket = {
  currencyCode: string;
  accountsPayableOutstanding: number;
  accountsReceivableOutstanding: number;
};

export type BillFinancialProfile = {
  billId: string;
  billNo: string;
  viewKind: string;
  asOfTimestamp: string;
  asOfFilter: string | null;
  byCurrency: CurrencyFinancialBucket[];
  settlementOutstanding: SettlementOutstandingBucket[];
  hasMixedCurrencies: boolean;
  note: string;
  asOfLimitationNote: string | null;
};

export type ProfitabilityCurrencyBucket = {
  currencyCode: string;
  revenueAmount: number;
  directCostAmount: number;
  allocatedCostAmount: number;
  costAmount: number;
  profitAmount: number;
  revenueVarianceExpectedVsActual: number;
  costVarianceExpectedVsActual: number;
  profitVarianceExpectedVsActual: number;
  revenueLineCount: number;
  directCostLineCount: number;
  allocatedCostLineCount: number;
};

export type BillProfitability = {
  billId: string;
  billNo: string;
  view: string;
  asOfTimestamp: string;
  byCurrency: ProfitabilityCurrencyBucket[];
  hasMixedCurrencies: boolean;
  note: string;
};

export type ApiResult<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; message: string };

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
          (res.status === 404
            ? "Không tìm thấy dữ liệu."
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

export function listBills(q?: string): Promise<ApiResult<BillListItem[]>> {
  const qs = q?.trim() ? `?q=${encodeURIComponent(q.trim())}` : "";
  return apiGet<BillListItem[]>(`/api/bills${qs}`);
}

export function getBill(id: string): Promise<ApiResult<BillDto>> {
  return apiGet<BillDto>(`/api/bills/${id}`);
}

export function getFinancialProfile(
  id: string
): Promise<ApiResult<BillFinancialProfile>> {
  return apiGet<BillFinancialProfile>(`/api/bills/${id}/financial-profile`);
}

export function getProfitability(
  id: string,
  view = "best"
): Promise<ApiResult<BillProfitability>> {
  return apiGet<BillProfitability>(
    `/api/bills/${id}/profitability?view=${encodeURIComponent(view)}`
  );
}

/** Vietnamese labels for operational status — never show raw enum to end users. */
export function operationalStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "active":
      return "Đang xử lý";
    case "confirmed":
      return "Đã xác nhận";
    case "completed":
    case "delivered":
      return "Đã giao";
    case "pending_document":
    case "awaiting_document":
      return "Chờ chứng từ";
    case "pending_approval":
      return "Chờ phê duyệt";
    case "recognized":
      return "Đã ghi nhận";
    case "closed":
      return "Đã đóng";
    case "cancelled":
    case "canceled":
      return "Đã hủy";
    default:
      return status || "—";
  }
}

export function billTypeLabel(billType: string): string {
  switch (billType?.toLowerCase()) {
    case "air":
      return "Hàng không";
    case "sea":
    case "ocean":
      return "Đường biển";
    case "road":
    case "truck":
      return "Đường bộ";
    case "rail":
      return "Đường sắt";
    case "multimodal":
      return "Đa phương thức";
    default:
      return billType || "—";
  }
}

/** Best-available rollup from a financial profile (first currency or null). */
export function profileBestRollup(profile: BillFinancialProfile | null): {
  currencyCode: string;
  revenue: number;
  cost: number;
  profit: number;
  revenueExpected: number;
  revenueConfirmed: number;
  revenueActual: number;
} | null {
  const b = profile?.byCurrency?.[0];
  if (!b) return null;
  return {
    currencyCode: b.currencyCode,
    revenue: b.revenueBestAvailable,
    cost: b.costBestAvailable,
    profit: b.profitBestAvailable,
    revenueExpected: b.revenueMaturity.expectedTotal,
    revenueConfirmed: b.revenueMaturity.confirmedTotal,
    revenueActual: b.revenueMaturity.actualTotal,
  };
}
