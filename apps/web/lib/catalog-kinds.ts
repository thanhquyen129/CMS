export const CATALOG_KINDS: { id: string; label: string }[] = [
  { id: "cost_type", label: "Loại chi phí" },
  { id: "revenue_type", label: "Loại doanh thu" },
  { id: "service_type", label: "Loại dịch vụ" },
  { id: "pricing_component", label: "Thành phần giá" },
  { id: "document_type", label: "Loại chứng từ" },
  { id: "payment_term", label: "Điều khoản thanh toán" },
];

export function catalogKindLabel(kind: string): string {
  return CATALOG_KINDS.find((k) => k.id === kind)?.label ?? kind;
}
