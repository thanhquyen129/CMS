import { formatMoney } from "@/lib/money";

export type ApArLedgerKind = "receivable" | "payable";

export type ApArLedgerEntry = {
  id: string;
  entryType: string;
  amount: number;
  currencyCode: string;
  balanceBefore: number;
  balanceAfter: number;
  businessDate: string;
  occurredAt: string;
  actorId: string | null;
  actorName: string | null;
  reason: string | null;
  sourceType: string;
  sourceId: string;
  sourceLabel: string | null;
  cashId: string | null;
  cashAmount: number | null;
  cashCurrencyCode: string | null;
  reversesEntryId: string | null;
  reversedByEntryId: string | null;
  status: string;
};

export type ApArLedger = {
  accountId: string;
  kind: "ar" | "ap";
  billId: string | null;
  billNo: string | null;
  currencyCode: string;
  currentOutstanding: number;
  ledgerBalance: number;
  reconciled: boolean;
  entries: ApArLedgerEntry[];
};

export function ledgerEntryLabel(entry: ApArLedgerEntry, kind: ApArLedgerKind): string {
  switch (entry.entryType) {
    case "recognition":
      return "Ghi nhận công nợ";
    case "adjustment":
      return entry.amount >= 0 ? "Điều chỉnh tăng" : "Điều chỉnh giảm";
    case "write_off":
      return "Xóa nợ";
    case "write_off_reversal":
      return "Hoàn tác xóa nợ";
    case "reverse_recognize":
      return "Hủy ghi nhận";
    case "allocation":
      return kind === "receivable" ? "Phân bổ thu tiền" : "Phân bổ thanh toán";
    case "allocation_reversal":
      return "Hủy phân bổ";
    default:
      return entry.entryType;
  }
}

export function ledgerStatusLabel(entry: ApArLedgerEntry): string {
  if (entry.status !== "reversed") return "Đã ghi sổ";
  if (entry.entryType === "write_off") return "Đã hoàn tác";
  if (entry.entryType === "allocation") return "Đã hủy phân bổ";
  return "Đã hủy";
}

export function formatSignedMoney(amount: number, currencyCode: string): string {
  if (amount === 0) return formatMoney(0, currencyCode);
  const sign = amount > 0 ? "+" : "−";
  return `${sign}${formatMoney(Math.abs(amount), currencyCode)}`;
}

export function cashDetailHref(entry: ApArLedgerEntry, kind: ApArLedgerKind): string | null {
  if (!entry.cashId) return null;
  return kind === "receivable"
    ? `/settlements/collections/${entry.cashId}`
    : `/settlements/payments/${entry.cashId}`;
}

export type ApArLegacyAllocation = {
  allocationId: string;
  cashId: string;
  cashReference: string | null;
  cashAmount: number;
  cashCurrencyCode: string;
  settledAmount: number;
  reversedAt: string;
};

export type ApArBalanceMismatch = {
  kind: "ar" | "ap";
  accountId: string;
  billId: string | null;
  billNo: string | null;
  currencyCode: string;
  recordStatus: string;
  currentOutstanding: number;
  ledgerBalance: number;
  difference: number;
  storedAdjustmentAmount: number;
  derivedAdjustmentAmount: number;
  storedSettledAmount: number;
  derivedSettledAmount: number;
  cause: "legacy_cross_currency_reversal" | "unexplained";
  correctable: boolean;
  rowVersion: string | null;
  legacyAllocations: ApArLegacyAllocation[];
};

export type ApArBalanceCorrection = {
  auditEventId: string;
  kind: "ar" | "ap";
  accountId: string;
  occurredAt: string;
  actorId: string | null;
  actorName: string | null;
  reason: string | null;
  outstandingBefore: number | null;
  outstandingAfter: number | null;
  currencyCode: string | null;
};

export type ApArBalanceReconciliation = {
  generatedAt: string;
  includesReceivables: boolean;
  includesPayables: boolean;
  checkedReceivables: number;
  checkedPayables: number;
  items: ApArBalanceMismatch[];
  recentCorrections: ApArBalanceCorrection[];
};

export function mismatchCauseLabel(cause: ApArBalanceMismatch["cause"]): string {
  return cause === "legacy_cross_currency_reversal"
    ? "Hủy phân bổ khác tiền tệ trước bản sửa"
    : "Chưa xác định — cần kiểm tra thủ công";
}

export function apArLedgerHref(
  kind: ApArLedgerKind,
  accountId: string,
  billId?: string | null
): string {
  const p = new URLSearchParams();
  if (kind === "receivable") p.set("tab", "ar");
  p.set("status", "all");
  if (billId) p.set("billId", billId);
  p.set("id", accountId);
  p.set("view", "ledger");
  return `/ap-ar?${p.toString()}`;
}
