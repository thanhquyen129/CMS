export type RateCard = {
  id: string;
  code: string;
  name: string;
  partyType: string;
  currencyCode: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  transportMode?: string | null;
  routeCode?: string | null;
  carrierName?: string | null;
  supplierPartyId?: string | null;
  customerPartyId?: string | null;
  customerGroupCode?: string | null;
  hasPublishedVersion?: boolean;
  routeId?: string | null;
  declarationMode?: string | null;
  routeIsActive?: boolean | null;
  routeOriginCode?: string | null;
  routeDestinationCode?: string | null;
  rowVersion?: string | null;
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
  vatRate?: number | null;
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
  economicChargeTypeId?: string | null;
  economicChargeTypeCode?: string | null;
};

export type RateBreak = {
  id: string;
  pricingRuleId: string;
  sequenceNo: number;
  minQuantity: number;
  maxQuantity: number | null;
  unitAmount: number;
};

export type ContainerRate = {
  id: string;
  pricingRuleId: string;
  containerType: string;
  unitAmount: number;
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
  routeId?: string | null;
  appliesAnyRoute?: boolean | null;
  carrierPartyId?: string | null;
  carrierName?: string | null;
  weightBasis?: string | null;
  weightMin?: number | null;
  weightMax?: number | null;
  matrixGroupCode?: string | null;
  minAmount: number | null;
  maxAmount: number | null;
  sortOrder: number;
  chargeCode?: string | null;
  transportMode?: string | null;
  commodityCode?: string | null;
  destinationCode?: string | null;
  components?: PricingRuleComponent[];
  breaks?: RateBreak[];
  containerRates?: ContainerRate[];
};

export const tariffCommodityOptions = [
  { value: "GENERAL", label: "Hàng thường", modes: ["air", "sea"] },
  { value: "DRY_FOOD", label: "Thực phẩm khô", modes: ["air"] },
  { value: "COSMETICS", label: "Mỹ phẩm", modes: ["air"] },
  { value: "COSMETICS_FOOD", label: "Mỹ phẩm, thực phẩm", modes: ["sea"] },
  { value: "EXPRESS", label: "Chuyển nhanh", modes: ["air"] },
] as const;

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
  chargeableWeightKg?: number | null;
  chargeableBasis?: string | null;
  staleAt?: string | null;
  staleReason?: string | null;
  partyTypeCode?: string | null;
  contextType?: string | null;
  transportLegId?: string | null;
  vendorPartyId?: string | null;
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
  formulaText?: string | null;
  sourceVersionId?: string | null;
  amountOriginal?: number | null;
  originalCurrency?: string | null;
  lineFxRate?: number | null;
  vatRate?: number | null;
  vatAmount?: number | null;
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
      return "Giá mua";
    case "customer":
      return "Giá bán";
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
    case "weight_break_pivot":
      return "Bậc trọng lượng";
    case "weight_step":
      return "Bậc bước trọng lượng";
    case "container_rate":
      return "Đơn giá container";
    case "composite":
      return "Giá thành phần";
    default:
      return "Cách tính khác";
  }
}

export function weightBandLabel(min: number | null | undefined, max: number | null | undefined): string {
  if (min == null && max == null) return "—";
  const text = (value: number) => {
    const raw = String(value);
    return raw.includes("e") || raw.includes("E") ? value.toFixed(4).replace(/0+$/, "").replace(/\.$/, "") : raw;
  };
  const lower = min ?? 0;
  if (max == null) return `≥ ${text(lower)}`;
  if (lower === 0) return `< ${text(max)}`;
  return `${text(lower)} – < ${text(max)}`;
}

export const weightBasisOptions = [
  { value: "", label: "Không lọc nấc" },
  { value: "gross", label: "Trọng lượng thực" },
  { value: "chargeable", label: "Trọng lượng tính cước" },
] as const;

export const basicCalcMethods = [
  { value: "fixed", label: "Cố định" },
  { value: "unit_rate", label: "Đơn giá × số lượng" },
  { value: "percent_of_base", label: "% trên cơ sở" },
  { value: "min_max_clamp", label: "Kẹp min/max" },
  { value: "weight_step", label: "Bậc bước trọng lượng" },
  { value: "container_rate", label: "Đơn giá container" },
  { value: "composite", label: "Giá thành phần" },
] as const;

export function rateTransportLabel(mode: string | null | undefined): string {
  switch ((mode ?? "").toLowerCase()) {
    case "air":
      return "Hàng không";
    case "sea":
    case "ocean":
      return "Đường biển";
    case "road":
    case "truck":
      return "Đường bộ";
    case "rail":
      return "Đường sắt";
    case "water":
      return "Đường thủy";
    case "multimodal":
      return "Đa phương thức";
    default:
      return mode ? "Phương thức khác" : "";
  }
}

export function declarationModeLabel(mode: string | null | undefined): string {
  switch (mode?.toLowerCase()) {
    case "basic":
      return "Bảng giá cơ bản";
    case "advanced":
      return "Bảng giá nâng cao";
    default:
      return "Chưa xác định hình thức";
  }
}

export function amountFieldLabel(calcMethod: string, applicability?: string | null): string {
  switch (calcMethod?.toLowerCase()) {
    case "fixed":
      return "Số tiền cố định";
    case "percent_of_base":
      return "Tỷ lệ (%)";
    case "unit_rate":
      return applicability === "per_gross_kg" ? "Đơn giá (theo kg thực)" : "Đơn giá (theo số lượng cước)";
    case "container_rate":
      return "Đơn giá (theo container)";
    case "weight_step":
      return "Đơn giá (theo kg)";
    case "min_max_clamp":
      return "Đơn giá (kẹp min/max)";
    default:
      return "Đơn giá";
  }
}

export function routeOptionLabel(route: {
  code: string;
  originCode?: string | null;
  destinationCode?: string | null;
}): string {
  const from = route.originCode || "—";
  const to = route.destinationCode || "—";
  return `${from} → ${to} (${route.code})`;
}

export function isWeightBreakMethod(method: string): boolean {
  return method?.toLowerCase() === "weight_break_pivot";
}

export function isContainerRateMethod(method: string): boolean {
  return method?.toLowerCase() === "container_rate";
}

export function isPublishedVersion(status: string): boolean {
  return status?.toLowerCase() === "published";
}

export function isDraftVersion(status: string): boolean {
  return status?.toLowerCase() === "draft";
}

export function cardStatusLabel(isActive: boolean): string {
  return isActive ? "Đang sử dụng" : "Ngừng sử dụng";
}

export function publicationLabel(hasPublished: boolean): string {
  return hasPublished ? "Có phiên bản đã phát hành" : "Chưa có phiên bản đã phát hành";
}

export const publicationListHint =
  "Chưa có ngữ cảnh Bill. Nhãn này không kết luận sẵn sàng tính giá.";
