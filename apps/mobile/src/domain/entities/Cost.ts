import { BillListItemDto, CostItemDto } from "@lcms/shared";

export type CostFinancialMaturity = "expected" | "confirmed" | "actual";
export type CostAttributionType = "direct" | "shared";
export type CostCurrencyCode = "VND" | "USD";
export type CostAllocationBasis = "equal" | "weight" | "manual";

export interface CreateCostInput {
  billId?: string | null;
  attributionType: CostAttributionType;
  amount: number;
  currencyCode: CostCurrencyCode;
  costTypeCode: string;
  sourceType?: string;
}

export interface AdvanceCostMaturityInput {
  costId: string;
  action: "confirm" | "actualize";
  targetAmount: number;
  rowVersion: string;
}

export interface AdjustCostInput {
  costId: string;
  deltaAmount: number;
  reason: string;
  rowVersion: string;
}

export interface AllocateCostInput {
  costId: string;
  costTypeCode?: string | null;
  totalAmount: number;
  allocationBasis: CostAllocationBasis;
  targetBillIds: string[];
}

export interface CostFilterParams {
  attributionType?: CostAttributionType;
  financialMaturity?: CostFinancialMaturity;
  costTypeCode?: string;
  searchQuery?: string;
}

export interface CostToleranceValidationResult {
  isValid: boolean;
  errorVi?: string;
  warningVi?: string;
  variancePercentage?: number;
}

