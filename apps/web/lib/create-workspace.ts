/** Shared types and helpers for UI-02 Order / Bill / Shipment create forms. */

export type CatalogOption = { value: string; label: string };

export type OperationalContext = {
  serviceType?: string | null;
  incoterm?: string | null;
  requestedAt?: string | null;
  pickupLocation?: string | null;
  deliveryLocation?: string | null;
  packageCount?: number | null;
  grossWeightKg?: number | null;
  volumeCbm?: number | null;
  chargeableWeightKg?: number | null;
  containerCount?: number | null;
  teu?: number | null;
  commodity?: string | null;
  specialFlags?: string[] | null;
  cargoDescription?: string | null;
  quoteReference?: string | null;
  shipperName?: string | null;
  consigneeName?: string | null;
  extraServices?: string[] | null;
  specialInstructions?: string | null;
  contactName?: string | null;
  contactChannel?: string | null;
  carrierName?: string | null;
  preferredCurrency?: string | null;
  rateDatePolicy?: string | null;
  vendorPartyId?: string | null;
  buyRateCardId?: string | null;
  rateDate?: string | null;
  ratingNote?: string | null;
  masterBillNo?: string | null;
  commodityTypeId?: string | null;
  chargeableConfirmed?: boolean | null;
  chargeableOverrideReason?: string | null;
};

export const TRANSPORT_MODES: CatalogOption[] = [
  { value: "air", label: "Air" },
  { value: "sea", label: "Sea" },
  { value: "road", label: "Road" },
  { value: "rail", label: "Rail" },
];

export const BILL_TYPES: CatalogOption[] = [
  { value: "house", label: "House Bill" },
  { value: "master", label: "Master Bill" },
];

export const INCOTERMS: CatalogOption[] = [
  { value: "EXW", label: "EXW" },
  { value: "FCA", label: "FCA" },
  { value: "FOB", label: "FOB" },
  { value: "CIF", label: "CIF" },
  { value: "CFR", label: "CFR" },
  { value: "DAP", label: "DAP" },
  { value: "DDP", label: "DDP" },
];

export const SPECIAL_FLAGS: CatalogOption[] = [
  { value: "dg", label: "DG" },
  { value: "reefer", label: "Hàng lạnh" },
  { value: "oog", label: "Quá khổ/quá tải" },
  { value: "other", label: "Khác" },
];

export const EXTRA_SERVICES: CatalogOption[] = [
  { value: "pickup", label: "Pickup" },
  { value: "delivery", label: "Delivery" },
  { value: "customs", label: "Hải quan" },
  { value: "warehouse", label: "Kho bãi" },
  { value: "insurance", label: "Bảo hiểm" },
  { value: "other", label: "Khác" },
];

const MANUAL_SOURCE = "lcms_manual";

/** Manual LCMS provenance for standalone create (ADR-0017 / ADR-0022). */
export function manualSource(): string {
  return MANUAL_SOURCE;
}

/** Business number ORD/BILL/SHP + yyMM + 4 digits. */
export function generateBusinessNo(prefix: string): string {
  const now = new Date();
  const yy = String(now.getFullYear()).slice(-2);
  const mm = String(now.getMonth() + 1).padStart(2, "0");
  const seq = Math.floor(1000 + Math.random() * 9000);
  return `${prefix}${yy}${mm}${seq}`;
}

export function formStr(fd: FormData, name: string): string | null {
  const raw = String(fd.get(name) ?? "").trim();
  return raw || null;
}

export function formNum(fd: FormData, name: string): number | null {
  const raw = String(fd.get(name) ?? "").trim().replaceAll(",", ".");
  if (!raw) return null;
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

export function formInt(fd: FormData, name: string): number | null {
  const n = formNum(fd, name);
  return n == null ? null : Math.round(n);
}

export function formDateIso(fd: FormData, name: string): string | null {
  const raw = String(fd.get(name) ?? "").trim();
  if (!raw) return null;
  return `${raw}T00:00:00+07:00`;
}

export function formChecks(fd: FormData, name: string): string[] {
  return fd
    .getAll(name)
    .map((v) => String(v).trim())
    .filter(Boolean);
}

export function composeRoute(origin: string | null, dest: string | null): string | null {
  if (!origin && !dest) return null;
  if (origin && dest) return `${origin} → ${dest}`;
  return origin || dest;
}

export function mergeCatalog(
  items: CatalogOption[],
  fallback: CatalogOption[]
): CatalogOption[] {
  if (items.length === 0) return fallback;
  const seen = new Set(items.map((i) => i.value.toLowerCase()));
  const extra = fallback.filter((f) => !seen.has(f.value.toLowerCase()));
  return [...items, ...extra];
}

export function catalogToOptions(
  items: { code: string; name: string; isActive: boolean }[]
): CatalogOption[] {
  return items
    .filter((i) => i.isActive)
    .map((i) => ({
      value: i.code,
      label: i.name && i.name !== i.code ? `${i.code} — ${i.name}` : i.code,
    }));
}
