export type RateCard = {
  id: string;
  code: string;
  name: string;
  partyType: string;
  currencyCode: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
};

export type RateVersion = {
  id: string;
  rateCardId: string;
  versionNo: number;
  status: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  publishedAt: string | null;
  note: string | null;
  createdAt: string;
};

export type PricingRuleComponent = {
  id: string;
  pricingRuleId: string;
  code: string;
  name: string;
  financialNature: string;
  costTypeCode: string | null;
  revenueTypeCode: string | null;
  amount: number;
  currencyCode: string;
  sortOrder: number;
};

export type PricingRule = {
  id: string;
  rateVersionId: string;
  code: string;
  name: string;
  calcMethod: string;
  unitAmount: number;
  currencyCode: string;
  applicability: string | null;
  serviceTypeCode: string | null;
  partyTypeCode: string | null;
  routeCode: string | null;
  minAmount: number | null;
  maxAmount: number | null;
  sortOrder: number;
  components?: PricingRuleComponent[];
};

export type RatingHistoryItem = {
  id: string;
  billId: string;
  rateVersionId: string;
  ratedAt: string;
  currencyCode: string;
  totalAmount: number;
  quantity: number;
  weight: number | null;
  status: string;
  supersedesRatingId: string | null;
};

export type RatingDetail = {
  id: string;
  pricingRuleId: string | null;
  pricingRuleComponentId: string | null;
  ruleCode: string;
  componentCode: string;
  componentName: string;
  financialNature: string;
  financialMaturity: string;
  amount: number;
  currencyCode: string;
};

export type Rating = RatingHistoryItem & {
  serviceTypeCode: string | null;
  partyTypeCode: string | null;
  routeCode: string | null;
  baseAmount: number | null;
  details: RatingDetail[];
};

export function partyTypeLabel(partyType: string): string {
  switch (partyType?.toLowerCase()) {
    case "vendor":
      return "Nhà cung cấp";
    case "customer":
      return "Khách hàng";
    default:
      return partyType;
  }
}

export function versionStatusLabel(status: string): string {
  switch (status?.toLowerCase()) {
    case "draft":
      return "Nháp";
    case "published":
      return "Đã phát hành";
    default:
      return status;
  }
}

export function calcMethodLabel(method: string): string {
  switch (method?.toLowerCase()) {
    case "fixed":
      return "Cố định";
    case "unit_rate":
      return "Đơn giá × số lượng";
    case "percent_of_base":
      return "% trên cơ sở";
    case "min_max_clamp":
      return "Kẹp min/max";
    default:
      return method;
  }
}

export function isPublishedVersion(status: string): boolean {
  return status?.toLowerCase() === "published";
}

export function isDraftVersion(status: string): boolean {
  return status?.toLowerCase() === "draft";
}
