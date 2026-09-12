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

export type CostAllocationDetail = {
  id: string;
  billId: string;
  basisValue: number;
  basisRatio: number;
  allocatedAmount: number;
  roundingAdjustment: number;
  manualOverrideAmount: number | null;
  overrideReason: string | null;
};

export type CostAllocation = {
  id: string;
  versionNo: number;
  allocationBasis: string;
  allocationStatus: string;
  allocatableAmount: number;
  allocatedAmount: number;
  finalizedAt: string | null;
  supersedesAllocationId: string | null;
  details: CostAllocationDetail[];
};

export type CostDto = CostListItem & {
  expectedAmount: number;
  confirmedAmount: number | null;
  actualAmount: number | null;
  baseAmount: number | null;
  fxRateId: string | null;
  vendorPartyId: string | null;
  sourceType: string | null;
  sourceId: string | null;
  approvalStatus: string;
  confirmedAt: string | null;
  actualizedAt: string | null;
  allocations: CostAllocation[];
};

export type AllocationBasis = "equal" | "quantity" | "manual_ratio";

export function allocationBasisLabel(basis: string): string {
  switch (basis?.toLowerCase()) {
    case "equal":
      return "Chia đều";
    case "quantity":
      return "Theo số lượng";
    case "manual_ratio":
      return "Tỷ lệ thủ công";
    default:
      return basis;
  }
}

export function allocationStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return "Nháp";
    case "finalized":
      return "Đã chốt";
    case "superseded":
      return "Đã thay thế";
    default:
      return status;
  }
}

export function isSharedCost(attributionType: string): boolean {
  return attributionType?.toLowerCase() === "shared";
}

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
