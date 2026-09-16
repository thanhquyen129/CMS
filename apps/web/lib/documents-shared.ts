/** Client-safe document types and labels (no next/headers). */

import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type FinancialDocumentListItem = {
  id: string;
  documentType: string;
  documentNo: string;
  direction: string;
  totalAmount: number;
  currencyCode: string;
  billId: string | null;
  receiptStatus: string;
  acceptanceStatus: string;
  matchingStatus: string;
  documentDate: string;
};

export type FinancialDocumentLine = {
  id: string;
  lineNo: number;
  description: string | null;
  amount: number;
  matchedAmount: number;
  openAmount: number;
  currencyCode: string;
  billId: string | null;
  costTypeCode: string | null;
  revenueTypeCode: string | null;
};

export type FinancialDocument = {
  id: string;
  documentType: string;
  documentNo: string;
  direction: string;
  totalAmount: number;
  currencyCode: string;
  documentDate: string;
  counterpartyId: string | null;
  billId: string | null;
  receiptStatus: string;
  acceptanceStatus: string;
  matchingStatus: string;
  receivedAt: string | null;
  acceptedAt: string | null;
  recordStatus: string;
  notes: string | null;
  lines: FinancialDocumentLine[];
};

export type ReceiveDocumentBody = {
  documentType: string;
  documentNo: string;
  direction: string;
  totalAmount: number;
  currencyCode: string;
  documentDate?: string | null;
  counterpartyId?: string | null;
  billId?: string | null;
  notes?: string | null;
  sourceSystem?: string | null;
  externalId?: string | null;
};

export function canAcceptDocument(doc: {
  receiptStatus: string;
  acceptanceStatus: string;
  recordStatus: string;
}): boolean {
  return (
    doc.recordStatus?.toLowerCase() === "active" &&
    doc.receiptStatus?.toLowerCase() === "received" &&
    doc.acceptanceStatus?.toLowerCase() === "not_accepted"
  );
}

export function canAddDocumentLine(doc: {
  receiptStatus: string;
  recordStatus: string;
  acceptanceStatus: string;
}): boolean {
  return (
    doc.recordStatus?.toLowerCase() === "active" &&
    doc.receiptStatus?.toLowerCase() === "received" &&
    doc.acceptanceStatus?.toLowerCase() !== "accepted"
  );
}

export function canMutateDocumentLine(
  doc: {
    receiptStatus: string;
    recordStatus: string;
    acceptanceStatus: string;
  },
  line: { matchedAmount: number }
): boolean {
  return canAddDocumentLine(doc) && Number(line.matchedAmount) <= 0.0000001;
}

export function documentLineCoverage(doc: {
  totalAmount: number;
  lines: { amount: number; openAmount: number }[];
}): {
  linesSum: number;
  openSum: number;
  remainingTowardTotal: number;
  sumsEqual: boolean;
} {
  const linesSum = doc.lines.reduce((s, l) => s + Number(l.amount || 0), 0);
  const openSum = doc.lines.reduce((s, l) => s + Number(l.openAmount || 0), 0);
  const remainingTowardTotal = Number(doc.totalAmount) - linesSum;
  const sumsEqual = Math.abs(remainingTowardTotal) <= 0.0001;
  return { linesSum, openSum, remainingTowardTotal, sumsEqual };
}

export function receiptStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "received":
      return term(terms, "RECEIVED", "Đã nhận");
    case "not_received":
      return term(terms, "NOT_RECEIVED", "Chưa nhận");
    default:
      return status || "—";
  }
}

export function acceptanceStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "accepted":
      return term(terms, "ACCEPTED", "Đã chấp nhận");
    case "not_accepted":
      return term(terms, "NOT_ACCEPTED", "Chưa chấp nhận");
    case "rejected":
      return term(terms, "REJECTED", "Từ chối");
    default:
      return status || "—";
  }
}

export function matchingStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "matched":
      return term(terms, "MATCHED", "Đã khớp");
    case "partially_matched":
      return term(terms, "PARTIALLY_MATCHED", "Khớp một phần");
    case "unmatched":
      return term(terms, "UNMATCHED", "Chưa khớp");
    default:
      return status || "—";
  }
}

export function documentTypeLabel(type: string): string {
  switch (type?.toLowerCase()) {
    case "dn":
      return "DN";
    case "invoice":
      return "Hóa đơn";
    case "credit_note":
      return "Credit note";
    case "debit_note":
      return "Debit note";
    case "other":
      return "Khác";
    default:
      return type || "—";
  }
}

export function directionLabel(
  terms: TerminologyMap,
  direction: string
): string {
  switch (direction?.toLowerCase()) {
    case "payable":
      return term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
    case "receivable":
      return term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
    default:
      return direction || "—";
  }
}

export function recordStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "active":
      return "Hiệu lực";
    case "cancelled":
      return term(terms, "DOCUMENT_CANCELLED", "Chứng từ đã hủy");
    case "voided":
      return term(terms, "DOCUMENT_VOIDED", "Chứng từ vô hiệu");
    default:
      return status || "—";
  }
}
