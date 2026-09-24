/** Client-safe AP/AR types and labels (no next/headers). */

import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type AccountsPayableItem = {
  id: string;
  payableExposureId: string;
  recognizedAmount: number;
  adjustmentAmount: number;
  finalizedSettledAmount: number;
  outstanding: number;
  currencyCode: string;
  dueDate: string | null;
  settlementStatus: string;
  billId: string | null;
  counterpartyId: string | null;
  recognizedAt: string;
  notes: string | null;
  recordStatus: string;
  daysPastDue: number | null;
  agingBucket: string;
  rowVersion?: string | null;
  billNo?: string | null;
};

export type AccountsReceivableItem = {
  id: string;
  receivableExposureId: string;
  recognizedAmount: number;
  adjustmentAmount: number;
  finalizedSettledAmount: number;
  outstanding: number;
  currencyCode: string;
  dueDate: string | null;
  settlementStatus: string;
  billId: string | null;
  counterpartyId: string | null;
  recognizedAt: string;
  notes: string | null;
  recordStatus: string;
  daysPastDue: number | null;
  agingBucket: string;
  rowVersion?: string | null;
  billNo?: string | null;
};

export type PayableExposureItem = {
  id: string;
  amount: number;
  recognizedAmount: number;
  openAmount: number;
  currencyCode: string;
  status: string;
  effectiveDate: string;
  dueDate: string | null;
  billId: string | null;
  counterpartyId: string | null;
  costId: string | null;
  financialDocumentId: string | null;
  notes: string | null;
  recordStatus: string;
  rowVersion?: string | null;
  billNo?: string | null;
};

export type ReceivableExposureItem = {
  id: string;
  amount: number;
  recognizedAmount: number;
  openAmount: number;
  currencyCode: string;
  status: string;
  effectiveDate: string;
  dueDate: string | null;
  billId: string | null;
  counterpartyId: string | null;
  revenueId: string | null;
  financialDocumentId: string | null;
  notes: string | null;
  recordStatus: string;
  rowVersion?: string | null;
  billNo?: string | null;
};

export type AgingBucketSummary = {
  bucket: string;
  count: number;
  outstanding: number;
};

export type AgingReport = {
  asOf: string;
  buckets: AgingBucketSummary[];
  payableItems: AccountsPayableItem[] | null;
  receivableItems: AccountsReceivableItem[] | null;
};

export type AgingSummary = {
  asOf: string;
  canViewPayable: boolean;
  canViewReceivable: boolean;
  payable: AgingReport | null;
  receivable: AgingReport | null;
  note: string;
};

export function settlementStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "open":
      return term(terms, "SETTLEMENT_OPEN", "Chưa tất toán");
    case "partial":
    case "partially_settled":
      return term(terms, "SETTLEMENT_PARTIAL", "Tất toán một phần");
    case "settled":
      return term(terms, "SETTLEMENT_SETTLED", "Đã tất toán");
    default:
      return status || "—";
  }
}

export function isSettled(item: {
  outstanding: number;
  settlementStatus: string;
}): boolean {
  return item.settlementStatus?.toLowerCase() === "settled";
}

export type ApArStatusFilter = "outstanding" | "settled" | "all";

export function parseApArStatusFilter(
  raw: string | undefined
): ApArStatusFilter {
  if (raw === "settled" || raw === "all") return raw;
  return "outstanding";
}

export function isOutstanding(item: {
  outstanding: number;
  settlementStatus: string;
}): boolean {
  if (item.outstanding > 0) return true;
  const s = item.settlementStatus?.toLowerCase();
  return s === "open" || s === "partial" || s === "partially_settled";
}

export function filterByApArStatus<
  T extends { outstanding: number; settlementStatus: string },
>(items: T[], filter: ApArStatusFilter): T[] {
  if (filter === "settled") return items.filter(isSettled);
  if (filter === "all") return items;
  return items.filter(isOutstanding);
}

export function agingBucketLabel(bucket: string): string {
  switch (bucket?.toLowerCase()) {
    case "no_due_date":
      return "Không hạn";
    case "current":
      return "Trong hạn";
    case "1_30":
      return "1–30 ngày";
    case "31_60":
      return "31–60 ngày";
    case "61_90":
      return "61–90 ngày";
    case "90_plus":
      return "Trên 90 ngày";
    default:
      return bucket || "—";
  }
}

export function exposureStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "open":
      return "Đang mở";
    case "partial":
      return "Ghi nhận một phần";
    case "recognized":
      return "Đã ghi nhận";
    case "closed":
      return "Đã đóng";
    default:
      return status || "—";
  }
}
