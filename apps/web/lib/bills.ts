import { redirect } from "next/navigation";
import { formatApiErrorMessage, readApiErrorBody } from "./api-error";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import { unwrapPaged, type PagedResult } from "./paging";

export type {
  BillListItem,
  BillDto,
} from "./bills-shared";
export {
  billTypeLabel,
  operationalStatusLabel,
  transportModeLabel,
} from "./bills-shared";
import type { BillDto, BillListItem } from "./bills-shared";

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
  reporting?: BillReportingSummary | null;
};

export type ReportingLine = {
  kind: string;
  id: string;
  label: string | null;
  currencyCode: string;
  originalAmount: number;
  fxRate: number | null;
  fxSourceType: string | null;
  fxSourceName: string | null;
  fxRateDate: string | null;
  reportingAmount: number | null;
  fxStatus: string;
  fxOverrideReason?: string | null;
};

export type BillReportingSummary = {
  reportingCurrencyCode: string;
  canViewRevenue: boolean;
  canViewCost: boolean;
  revenueBestAvailable: number | null;
  costBestAvailable: number | null;
  directCostBestAvailable: number | null;
  allocatedCostAmount: number | null;
  profitBestAvailable: number | null;
  marginPercent: number | null;
  revenueMaturity: MaturityBreakdown | null;
  directCostMaturity: MaturityBreakdown | null;
  accountsPayableOutstanding: number | null;
  accountsReceivableOutstanding: number | null;
  missingFxCount: number;
  complete: boolean;
  lines: ReportingLine[];
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
  reportingCurrency?: string | null;
  reportingRevenue?: number | null;
  reportingCost?: number | null;
  reportingProfit?: number | null;
  unconvertedCurrencies?: string[] | null;
};

export type ApiResult<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; message: string; correlationId?: string };

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
      const body = await readApiErrorBody(res);
      return {
        ok: false,
        status: res.status,
        correlationId: body.correlationId,
        message: formatApiErrorMessage(
          body,
          res.status === 404
            ? "Không tìm thấy dữ liệu."
            : "Không tải được dữ liệu từ máy chủ."
        ),
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

export function listBills(
  q?: string,
  opts?: { page?: number; pageSize?: number }
): Promise<ApiResult<PagedResult<BillListItem>>> {
  const p = new URLSearchParams();
  if (q?.trim()) p.set("q", q.trim());
  if (opts?.page != null) p.set("page", String(opts.page));
  if (opts?.pageSize != null) p.set("pageSize", String(opts.pageSize));
  const qs = p.toString();
  return apiGet<BillListItem[] | PagedResult<BillListItem>>(
    qs ? `/api/bills?${qs}` : "/api/bills"
  ).then((r) =>
    r.ok ? { ok: true, data: unwrapPaged(r.data) } : r
  );
}

export function getBill(id: string): Promise<ApiResult<BillDto>> {
  return apiGet<BillDto>(`/api/bills/${id}`);
}

export function getFinancialProfile(
  id: string,
  asOf?: string | null
): Promise<ApiResult<BillFinancialProfile>> {
  const qs = asOf ? `?asOf=${encodeURIComponent(asOf)}` : "";
  return apiGet<BillFinancialProfile>(`/api/bills/${id}/financial-profile${qs}`);
}

export type ChargeProfitSource = {
  sourceId: string;
  sourceKind: string;
  side: string;
  billId: string | null;
  partnerId: string | null;
  peakMaturity: string;
};

export type ChargeProfitRow = {
  economicChargeTypeId: string | null;
  chargeCode: string;
  chargeName: string;
  costReporting: number | null;
  revenueReporting: number | null;
  profitReporting: number | null;
  marginPercent: number | null;
  comparisonKind: string;
  dataCompleteness: string;
  negativeFlag: boolean;
  note: string;
  sources: ChargeProfitSource[];
};

export type ChargeProfitability = {
  view: string;
  reportingCurrency: string | null;
  rows: ChargeProfitRow[];
};

export function getChargeProfitability(id: string, view = "best"): Promise<ApiResult<ChargeProfitability>> {
  const params = new URLSearchParams({ view });
  return apiGet<ChargeProfitability>(`/api/bills/${id}/charge-profitability?${params.toString()}`);
}

export function getOrderChargeProfitability(id: string, view = "best"): Promise<ApiResult<ChargeProfitability>> {
  const params = new URLSearchParams({ view });
  return apiGet<ChargeProfitability>(`/api/orders/${id}/charge-profitability?${params.toString()}`);
}

export function getProfitability(
  id: string,
  view = "best",
  reportingCurrency?: string | null
): Promise<ApiResult<BillProfitability>> {
  const params = new URLSearchParams({ view });
  if (reportingCurrency) params.set("reportingCurrency", reportingCurrency);
  return apiGet<BillProfitability>(
    `/api/bills/${id}/profitability?${params.toString()}`
  );
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
