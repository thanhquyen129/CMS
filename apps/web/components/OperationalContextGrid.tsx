import { transportModeLabel } from "@/lib/bills-shared";
import type { OperationalContext } from "@/lib/create-workspace";
import { EXTRA_SERVICES, INCOTERMS, SPECIAL_FLAGS } from "@/lib/create-workspace";
import { formatDateTimeVi } from "@/lib/money";

type Row = {
  customerName?: string | null;
  assignedUserName?: string | null;
  transportMode?: string | null;
  originCode?: string | null;
  destinationCode?: string | null;
  routeCode?: string | null;
  etdAt?: string | null;
  etaAt?: string | null;
  customerReference?: string | null;
  description?: string | null;
  context?: OperationalContext | null;
};

function optionLabel(options: { value: string; label: string }[], value: string | null | undefined) {
  if (!value) return null;
  return options.find((o) => o.value === value)?.label ?? value;
}

function Kv({ label, value }: { label: string; value?: string | number | null }) {
  if (value == null || value === "") return null;
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

/** Operational rating context — identity fields plus cargo / extras. Not TMS execution. */
export function OperationalContextGrid({ row }: { row: Row }) {
  const ctx = row.context;
  const extras = (ctx?.extraServices ?? [])
    .map((v) => optionLabel(EXTRA_SERVICES, v) ?? v)
    .join(", ");
  const flags = (ctx?.specialFlags ?? [])
    .map((v) => optionLabel(SPECIAL_FLAGS, v) ?? v)
    .join(", ");
  const route =
    row.routeCode ||
    (row.originCode && row.destinationCode
      ? `${row.originCode} → ${row.destinationCode}`
      : row.originCode || row.destinationCode);

  return (
    <dl className="kv-grid">
      <Kv label="Khách hàng" value={row.customerName} />
      <Kv label="Người phụ trách" value={row.assignedUserName} />
      <Kv label="Phương thức" value={transportModeLabel(row.transportMode)} />
      <Kv label="Tuyến" value={route} />
      <Kv label="ETD" value={row.etdAt ? formatDateTimeVi(row.etdAt) : null} />
      <Kv label="ETA" value={row.etaAt ? formatDateTimeVi(row.etaAt) : null} />
      <Kv label="Reference khách" value={row.customerReference} />
      <Kv label="Mô tả" value={row.description} />
      <Kv label="Incoterm" value={optionLabel(INCOTERMS, ctx?.incoterm) ?? ctx?.incoterm} />
      <Kv label="Dịch vụ" value={ctx?.serviceType} />
      <Kv label="Người gửi" value={ctx?.shipperName} />
      <Kv label="Người nhận" value={ctx?.consigneeName} />
      <Kv label="Hàng hóa" value={ctx?.commodity || ctx?.cargoDescription} />
      <Kv label="Số kiện" value={ctx?.packageCount} />
      <Kv label="Trọng lượng (kg)" value={ctx?.grossWeightKg} />
      <Kv label="Thể tích (cbm)" value={ctx?.volumeCbm} />
      <Kv label="Dịch vụ thêm" value={extras || null} />
      <Kv label="Cảnh báo hàng" value={flags || null} />
      <Kv label="Tiền tệ báo giá" value={ctx?.preferredCurrency} />
      <Kv label="Ghi chú tính giá" value={ctx?.ratingNote} />
    </dl>
  );
}
