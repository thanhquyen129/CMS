import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type DocumentMatchDetail = {
  id: string;
  sourceLineId: string;
  targetLineId: string | null;
  targetCostId: string | null;
  targetRevenueId: string | null;
  matchedAmount: number;
  detailStatus: string;
  reversedAt: string | null;
  reverseReason: string | null;
};

export type DocumentMatch = {
  id: string;
  matchMethod: string;
  matchStatus: string;
  versionNo: number;
  primaryDocumentId: string | null;
  toleranceAmount: number;
  tolerancePercent: number;
  notes: string | null;
  confirmedAt: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  details: DocumentMatchDetail[];
  rowVersion?: string | null;
};

export const MATCH_METHODS = [
  "line_to_cost",
  "line_to_revenue",
  "line_to_line",
] as const;

export type MatchMethod = (typeof MATCH_METHODS)[number];

export function canStartMatch(doc: {
  receiptStatus: string;
  acceptanceStatus: string;
  recordStatus: string;
  lines: { openAmount: number }[];
}): boolean {
  if (doc.recordStatus?.toLowerCase() !== "active") return false;
  if (doc.receiptStatus?.toLowerCase() !== "received") return false;
  if (doc.acceptanceStatus?.toLowerCase() !== "accepted") return false;
  return doc.lines.some((l) => Number(l.openAmount) > 0);
}

export function matchMethodLabel(
  terms: TerminologyMap,
  method: string
): string {
  switch (method?.toLowerCase()) {
    case "line_to_line":
      return term(terms, "MATCH_LINE_TO_LINE", "Dòng ↔ dòng chứng từ");
    case "line_to_cost":
      return term(terms, "MATCH_LINE_TO_COST", "Dòng ↔ chi phí");
    case "line_to_revenue":
      return term(terms, "MATCH_LINE_TO_REVENUE", "Dòng ↔ doanh thu");
    default:
      return method || "—";
  }
}

export function matchStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return term(terms, "MATCH_DRAFT", "Nháp");
    case "confirmed":
      return term(terms, "MATCH_CONFIRMED", "Đã xác nhận");
    case "cancelled":
      return term(terms, "MATCH_CANCELLED", "Đã hủy");
    default:
      return status || "—";
  }
}

export function detailStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status?.toLowerCase()) {
    case "active":
      return "Hiệu lực";
    case "reversed":
      return term(terms, "MATCH_DETAIL_REVERSED", "Đã hủy khớp");
    default:
      return status || "—";
  }
}

export function isDraftMatch(status: string): boolean {
  return status?.toLowerCase() === "draft";
}

export function isActiveDetail(status: string): boolean {
  return status?.toLowerCase() === "active";
}

export function defaultMatchMethod(direction: string): MatchMethod {
  return direction?.toLowerCase() === "receivable"
    ? "line_to_revenue"
    : "line_to_cost";
}
