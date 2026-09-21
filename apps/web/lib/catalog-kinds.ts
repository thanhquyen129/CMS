export const CATALOG_KINDS: { id: string; label: string; group: "core" | "transport" | "other" }[] = [
  { id: "cost_type", label: "Loại chi phí", group: "core" },
  { id: "revenue_type", label: "Loại doanh thu", group: "core" },
  { id: "service_type", label: "Loại dịch vụ", group: "core" },
  { id: "transport_route", label: "Tuyến vận chuyển", group: "transport" },
  { id: "transport_mode", label: "Phương thức vận chuyển", group: "transport" },
  { id: "location", label: "Cảng / Sân bay / Cửa khẩu", group: "transport" },
  { id: "pricing_component", label: "Thành phần giá", group: "other" },
  { id: "document_type", label: "Loại chứng từ", group: "other" },
  { id: "payment_term", label: "Điều khoản thanh toán", group: "other" },
];

export const LOCATION_CLASSES = [
  { id: "port", label: "Cảng" },
  { id: "airport", label: "Sân bay" },
  { id: "border", label: "Cửa khẩu" },
] as const;

export function catalogKindLabel(kind: string): string {
  return CATALOG_KINDS.find((k) => k.id === kind)?.label ?? kind;
}

export function locationClassLabel(code: string | null | undefined): string {
  return LOCATION_CLASSES.find((k) => k.id === code)?.label ?? code ?? "—";
}
