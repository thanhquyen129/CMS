export type CostListItem = {
  id: string;
  billId: string | null;
  attributionType: string;
  financialMaturity: string;
  amount: number;
  currencyCode: string;
  costTypeCode: string | null;
  recordStatus: string;
  organizationId: string | null;
  createdBy: string | null;
  effectiveDate: string;
};

export type RevenueListItem = {
  id: string;
  billId: string;
  financialMaturity: string;
  amount: number;
  currencyCode: string;
  revenueTypeCode: string | null;
  recordStatus: string;
  effectiveDate: string;
};

export function maturityLabelKey(maturity: string): string {
  switch (maturity?.toLowerCase()) {
    case "expected":
      return "EXPECTED";
    case "confirmed":
      return "CONFIRMED";
    case "actual":
      return "ACTUAL";
    default:
      return maturity?.toUpperCase() ?? "";
  }
}

export function canConfirm(maturity: string, recordStatus: string): boolean {
  return (
    recordStatus === "active" && maturity?.toLowerCase() === "expected"
  );
}

export function canActualize(maturity: string, recordStatus: string): boolean {
  return (
    recordStatus === "active" && maturity?.toLowerCase() === "confirmed"
  );
}
