/** Client types for Bill Financial View (UI-02 drawer). */

import type { BillDto } from "./bills-shared";
import type { CostListItem, RevenueListItem } from "./costs-revenues";
import type { FinancialDocumentListItem } from "./documents-shared";
import type { BillWaybill } from "./waybill";

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

export type BillFinancialProfile = {
  billId: string;
  billNo: string;
  viewKind: string;
  asOfTimestamp: string;
  asOfFilter: string | null;
  byCurrency: CurrencyFinancialBucket[];
  settlementOutstanding: {
    currencyCode: string;
    accountsPayableOutstanding: number;
    accountsReceivableOutstanding: number;
  }[];
  hasMixedCurrencies: boolean;
  note: string;
  asOfLimitationNote: string | null;
};

export type BillProgressStep = {
  id: string;
  labelVi: string;
  state: "done" | "current" | "pending" | string;
};

export type BillGraphOrderRef = {
  id: string;
  orderNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
};

export type BillGraphShipmentRef = {
  id: string;
  shipmentNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
};

export type BillGraphLegRef = {
  id: string;
  legNo: string;
  shipmentId: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
};

export type BillGraphMovementRef = {
  id: string;
  movementNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
};

export type BillGraphDto = {
  billId: string;
  billNo: string;
  billType: string;
  operationalStatus: string;
  orders: BillGraphOrderRef[];
  shipments: BillGraphShipmentRef[];
  legs: BillGraphLegRef[];
  movements: BillGraphMovementRef[];
};

export type BillFinancialView = {
  bill: BillDto;
  profile: BillFinancialProfile;
  graph: BillGraphDto;
  progress: BillProgressStep[];
  costs: CostListItem[];
  revenues: RevenueListItem[];
  documents: FinancialDocumentListItem[];
  costCount: number;
  revenueCount: number;
  documentCount: number;
  waybill?: BillWaybill | null;
};

export type UpdateBillContextBody = {
  customerPartyId?: string | null;
  routeCode?: string | null;
  etdAt?: string | null;
  etaAt?: string | null;
  assignedUserId?: string | null;
  description?: string | null;
  internalNote?: string | null;
};

export async function fetchBillFinancialView(
  billId: string
): Promise<
  | { ok: true; data: BillFinancialView }
  | { ok: false; status: number; message: string }
> {
  try {
    const res = await fetch(
      `/bff/bills/${encodeURIComponent(billId)}/financial-view`,
      { headers: { Accept: "application/json" }, cache: "no-store" }
    );
    if (res.status === 401) {
      window.location.href = "/login";
      return { ok: false, status: 401, message: "Phiên đăng nhập đã hết." };
    }
    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message:
          body.message ||
          (res.status === 404
            ? "Không tìm thấy Bill."
            : "Không tải được hồ sơ tài chính Bill."),
      };
    }
    return { ok: true, data: (await res.json()) as BillFinancialView };
  } catch {
    return {
      ok: false,
      status: 0,
      message: "Không kết nối được máy chủ. Thử lại sau.",
    };
  }
}

export async function patchBillContext(
  billId: string,
  body: UpdateBillContextBody
): Promise<{ ok: true } | { ok: false; message: string }> {
  try {
    const res = await fetch(
      `/bff/bills/${encodeURIComponent(billId)}/context`,
      {
        method: "PATCH",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body),
      }
    );
    if (res.status === 401) {
      window.location.href = "/login";
      return { ok: false, message: "Phiên đăng nhập đã hết." };
    }
    if (res.status === 204 || res.ok) {
      return { ok: true };
    }
    const payload = (await res.json().catch(() => ({}))) as {
      message?: string;
    };
    return {
      ok: false,
      message: payload.message || "Không lưu được ghi chú / ngữ cảnh Bill.",
    };
  } catch {
    return { ok: false, message: "Không kết nối được máy chủ. Thử lại sau." };
  }
}
