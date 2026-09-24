/** Client-safe settlement types and labels (no next/headers). */

import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type PaymentAllocationItem = {
  id: string;
  paymentId: string;
  accountsPayableId: string;
  amount: number;
  currencyCode: string;
  baseAmount: number | null;
  fxRateId: string | null;
  allocationStatus: string;
  createdAt: string;
  finalizedAt: string | null;
  reversedAt: string | null;
  reverseReason: string | null;
  notes: string | null;
  rowVersion?: string | null;
};

export type PaymentItem = {
  id: string;
  amount: number;
  baseAmount: number | null;
  fxRateId: string | null;
  appliedAmount: number;
  allocatedAmount: number;
  unappliedAmount: number;
  availableToAllocate: number;
  currencyCode: string;
  valueDate: string;
  counterpartyId: string | null;
  billId: string | null;
  billNo: string | null;
  referenceNo: string | null;
  notes: string | null;
  status: string;
  recordStatus: string;
  allocations: PaymentAllocationItem[];
  rowVersion?: string | null;
};

export type CollectionAllocationItem = {
  id: string;
  collectionId: string;
  accountsReceivableId: string;
  amount: number;
  currencyCode: string;
  baseAmount: number | null;
  fxRateId: string | null;
  allocationStatus: string;
  createdAt: string;
  finalizedAt: string | null;
  reversedAt: string | null;
  reverseReason: string | null;
  notes: string | null;
  rowVersion?: string | null;
};

export type CollectionItem = {
  id: string;
  amount: number;
  baseAmount: number | null;
  fxRateId: string | null;
  appliedAmount: number;
  allocatedAmount: number;
  unappliedAmount: number;
  availableToAllocate: number;
  currencyCode: string;
  valueDate: string;
  counterpartyId: string | null;
  billId: string | null;
  billNo: string | null;
  referenceNo: string | null;
  notes: string | null;
  status: string;
  recordStatus: string;
  allocations: CollectionAllocationItem[];
  rowVersion?: string | null;
};

export function allocationStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return term(terms, "ALLOCATION_DRAFT", "Phân bổ nháp");
    case "finalized":
      return term(terms, "ALLOCATION_FINALIZED", "Đã chốt phân bổ");
    case "reversed":
      return term(terms, "ALLOCATION_REVERSED", "Đã đảo phân bổ");
    default:
      return status || "—";
  }
}

export function isDraftAllocation(status: string): boolean {
  return status?.toLowerCase() === "draft";
}

export function isFinalizedAllocation(status: string): boolean {
  return status?.toLowerCase() === "finalized";
}

export function canReverseAllocation(status: string): boolean {
  const s = status?.toLowerCase();
  return s === "draft" || s === "finalized";
}

export function settlementBillLinkLabel(
  billId: string | null | undefined,
  billNo: string | null | undefined,
  billLabel = "Bill"
): string | null {
  if (!billId) return null;
  if (billNo?.trim()) return billNo.trim();
  return `${billLabel} ${billId.slice(0, 8)}…`;
}
