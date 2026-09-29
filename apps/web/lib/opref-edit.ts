/** Operational reference edit + rating context (ADR-0039). */

export type OprefObjectType = "bill" | "order" | "shipment" | "leg" | "movement";

export type ChargeableState = "missing" | "system" | "source" | "manual" | "override";

export type ChargeableWeightState = {
  state: ChargeableState;
  value: number | null;
  uom: string;
  sourceChannel: string | null;
  sourceLabel: string | null;
  sourceValue: number | null;
  ruleCode: string | null;
  isConfirmed: boolean;
  confirmedAt: string | null;
  overrideReason: string | null;
  overriddenAt: string | null;
  canConfirm: boolean;
  missingReason: string | null;
  persisted: boolean;
};

export type OprefFieldKind =
  | "text"
  | "decimal"
  | "integer"
  | "datetime"
  | "transport_mode"
  | "commodity"
  | "flags";

export type OprefFieldState = {
  code: string;
  label: string;
  kind: OprefFieldKind;
  group: string;
  value: string | null;
  source: string;
  sourceLabel: string;
  editable: boolean;
  requiresReason: boolean;
  lockReason: string | null;
  ratingRelevant: boolean;
  sourceValue: string | null;
  overrideReason: string | null;
};

export type OprefRatingStatus = {
  ratingId: string;
  billId: string;
  billNo: string;
  rateVersionId: string;
  ratedAt: string;
  chargeableWeightKg: number | null;
  chargeableBasis: string | null;
  stale: boolean;
  staleAt: string | null;
  staleReason: string | null;
};

export type OprefOption = { value: string; label: string };

export type OprefEditView = {
  objectType: OprefObjectType;
  objectId: string;
  code: string;
  objectLabel: string;
  sourceSystem: string | null;
  sourceLabel: string;
  canEdit: boolean;
  canOverrideSource: boolean;
  canOverrideChargeable: boolean;
  readOnlyReason: string | null;
  rowVersion: string | null;
  fields: OprefFieldState[];
  chargeable: ChargeableWeightState | null;
  currentRatings: OprefRatingStatus[];
  linkedBillCount: number;
  commodities: OprefOption[];
};

export type OprefEditResult = {
  changedFields: string[];
  ratingsMarkedStale: number;
  rowVersion: string | null;
};

export type RatingMissingField = {
  field: string;
  label: string;
  ruleCodes: string[];
  message: string;
  action: "edit_bill" | "rating_form" | "rate_card" | string;
};

export type RatingReadiness = {
  ready: boolean;
  billId: string;
  rateVersionId: string;
  rateCardCode: string | null;
  versionNo: number;
  ruleCodes: string[];
  quantity: number | null;
  quantityBasis: string;
  quantityUom: string | null;
  quantityRuleCode: string | null;
  requiresOverride: boolean;
  missing: RatingMissingField[];
  chargeable: ChargeableWeightState | null;
};

export type ApiError = {
  code?: string;
  message?: string;
  missing?: RatingMissingField[];
};

export const TRANSPORT_MODE_OPTIONS: OprefOption[] = [
  { value: "air", label: "Hàng không (Air)" },
  { value: "sea", label: "Đường biển (Sea)" },
  { value: "road", label: "Đường bộ (Road)" },
  { value: "rail", label: "Đường sắt (Rail)" },
  { value: "express", label: "Chuyển phát nhanh" },
];

export const FIELD_GROUP_LABELS: Record<string, string> = {
  cargo: "Hàng hóa & đo lường",
  route: "Tuyến & lịch",
  service: "Dịch vụ",
  other: "Khác",
};

const BASIS_LABELS: Record<string, string> = {
  confirmed: "Đã xác nhận trên Bill",
  measured: "Theo số trên Bill",
  air_volumetric: "Hệ thống tính (Air, thể tích)",
  sea_wm: "Hệ thống tính (Sea, W/M)",
  override: "Ghi đè khi tính giá",
  manual: "Nhập khi tính giá",
  weight: "Theo trọng lượng thực",
  not_required: "Không cần (phí cố định)",
};

export function basisLabel(basis: string | null | undefined): string {
  if (!basis) return "—";
  return BASIS_LABELS[basis] ?? basis;
}

/** Undetermined CW is shown as text, never as 0 (ADR-0039 D07). */
export function formatChargeable(value: number | null | undefined, uom?: string | null): string {
  if (value === null || value === undefined) return "Chưa xác định";
  const n = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 4 }).format(value);
  return `${n} ${uom === "W/M" ? "W/M" : "kg"}`;
}

export function chargeableStateLabel(state: ChargeableWeightState | null | undefined): string {
  if (!state) return "Không áp dụng";
  switch (state.state) {
    case "missing":
      return "Chưa tính được";
    case "override":
      return "Ghi đè";
    case "system":
      return state.isConfirmed ? "Hệ thống tính · Đã xác nhận" : "Hệ thống tính · Chưa xác nhận";
    default:
      return state.isConfirmed
        ? `${state.sourceLabel ?? "Nhập thủ công"} · Đã xác nhận`
        : state.sourceLabel ?? "Nhập thủ công";
  }
}

export function sourceBadgeClass(source: string): string {
  switch (source) {
    case "override":
      return "status-pill src-override";
    case "api":
    case "import":
      return "status-pill src-external";
    case "system":
      return "status-pill src-system";
    default:
      return "status-pill";
  }
}

export function ratingStatusView(status: string, staleAt: string | null | undefined): { label: string; className: string } {
  if (status?.toLowerCase() === "superseded") {
    return { label: "Đã thay thế", className: "status-pill status-inactive" };
  }
  if (staleAt) {
    return { label: "Cần tính giá lại", className: "status-pill rating-stale" };
  }
  return { label: "Hiện hành", className: "status-pill status-confirmed" };
}

export function objectTypeFromKind(kind: string): OprefObjectType | null {
  switch (kind) {
    case "bill":
    case "bills":
      return "bill";
    case "order":
    case "orders":
      return "order";
    case "shipment":
    case "shipments":
      return "shipment";
    case "leg":
    case "legs":
    case "transport-legs":
      return "leg";
    case "movement":
    case "movements":
    case "transport-movements":
      return "movement";
    default:
      return null;
  }
}
