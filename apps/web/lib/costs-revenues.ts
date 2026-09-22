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
  vendorPartyId?: string | null;
  expectedAmount?: number;
  confirmedAmount?: number | null;
  actualAmount?: number | null;
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

export type CostAdjustmentItem = {
  id: string;
  adjustmentType: string;
  deltaAmount: number;
  currencyCode: string;
  reason: string;
  effectiveDate: string;
  appliedToMaturity: string;
  amountBefore: number;
  amountAfter: number;
  createdAt: string;
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
  adjustments: CostAdjustmentItem[];
  allocations: CostAllocation[];
};

export type RevenueDto = RevenueListItem & {
  expectedAmount: number;
  confirmedAmount: number | null;
  actualAmount: number | null;
  baseAmount: number | null;
  fxRateId: string | null;
  customerPartyId: string | null;
  sourceType: string | null;
  sourceId: string | null;
  recognitionPolicyVersion: string | null;
  approvalStatus: string;
  confirmedAt: string | null;
  actualizedAt: string | null;
  adjustments: CostAdjustmentItem[];
};

export type AllocationBasis =
  | "equal"
  | "quantity"
  | "gross_kg"
  | "chargeable"
  | "cbm"
  | "package_count"
  | "teu"
  | "manual_ratio"
  | "manual_percent"
  | "manual_amount";

export function adjustmentTypeLabel(type: string): string {
  switch (type?.toLowerCase()) {
    case "adjustment":
      return "Điều chỉnh";
    case "reversal":
      return "Đảo / hoàn";
    default:
      return type;
  }
}

export function allocationBasisLabel(basis: string): string {
  switch (basis?.toLowerCase()) {
    case "equal":
      return "Chia đều";
    case "quantity":
      return "Theo số lượng";
    case "gross_kg":
      return "Theo kg";
    case "chargeable":
      return "Theo trọng lượng tính cước";
    case "cbm":
      return "Theo CBM";
    case "package_count":
      return "Theo số kiện";
    case "teu":
      return "Theo TEU";
    case "manual_ratio":
      return "Tỷ lệ thủ công";
    case "manual_percent":
      return "Theo phần trăm";
    case "manual_amount":
      return "Theo số tiền";
    default:
      return basis;
  }
}

export function allocationStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return "Nháp";
    case "calculated":
      return "Đã tính";
    case "pending_approval":
      return "Chờ duyệt";
    case "finalized":
      return "Đã chốt";
    case "cancelled":
      return "Đã hủy";
    case "superseded":
      return "Đã thay thế";
    default:
      return status;
  }
}

export function isSharedCost(attributionType: string): boolean {
  return attributionType?.toLowerCase() === "shared";
}

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
