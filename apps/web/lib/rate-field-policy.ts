export type CatalogChoice = { code: string; name: string; isActive?: boolean };

export const serviceAirBlocked =
  "Loại dịch vụ AIR trùng phương thức vận chuyển. Không chọn cho quy tắc mới. Dữ liệu cũ vẫn được giữ để xem.";
export const waterBlocked =
  "Đường thủy (WATER) là dữ liệu cũ, chưa có trong danh mục phương thức. Không tạo mới và không đổi thành đường biển.";
export const expressBlocked =
  "Chuyển phát nhanh (EXPRESS) chưa được bật vì Bill và Chặng chưa có quy tắc trọng lượng riêng. Không đổi thành hàng không.";
export const modeRouteConflict =
  "Phương thức không khớp tuyến đã chọn. Chọn đúng phương thức của tuyến, hoặc chọn Tất cả tuyến.";
export const chargeRequired = "Quy tắc mới phải có nhóm cạnh tranh. Không để trống nhóm tính giá.";
export const surchargeRedirect = "Phụ phí không khai trên quy tắc giá. Hãy tạo ở Quản lý phụ phí.";
export const draftFinancialWarning =
  "Bản nháp còn dòng chưa có loại chi phí hoặc loại doanh thu. Có thể lưu, chưa phát hành được.";
export const catalogEmpty = "Danh mục trống. Không tự sinh mã.";

const canonicalModes = [
  { code: "AIR", label: "Hàng không" },
  { code: "SEA", label: "Đường biển" },
  { code: "ROAD", label: "Đường bộ" },
  { code: "RAIL", label: "Đường sắt" },
];

const knownLabels: Record<string, string> = {
  AIR: "Hàng không",
  SEA: "Đường biển",
  ROAD: "Đường bộ",
  RAIL: "Đường sắt",
  WATER: "Đường thủy (dữ liệu cũ)",
  EXPRESS: "Chuyển phát nhanh",
  FREIGHT: "Vận tải",
};

export function storedMode(value: string | null | undefined): string | null {
  if (!value?.trim()) return null;
  const key = value.trim().toUpperCase();
  if (key === "AIR" || key === "SEA" || key === "ROAD" || key === "RAIL" || key === "WATER" || key === "EXPRESS") {
    return key.toLowerCase();
  }
  return value.trim().toLowerCase();
}

export function sameMode(left: string | null | undefined, right: string | null | undefined): boolean {
  return storedMode(left) === storedMode(right);
}

export function catalogLabel(code: string, name?: string | null): string {
  const text = name?.trim();
  if (text && text.toUpperCase() !== code.trim().toUpperCase()) return text;
  return knownLabels[code.trim().toUpperCase()] ?? text ?? code;
}

export function selectableServices(items: CatalogChoice[] | undefined, keepCode?: string | null): CatalogChoice[] {
  const active = (items ?? []).filter((item) => item.isActive !== false && item.code.trim());
  const source = active.length > 0 ? active : [];
  return source.filter((item) => item.code.trim().toUpperCase() !== "AIR" || sameCode(item.code, keepCode));
}

export function selectableModes(items: CatalogChoice[] | undefined): { value: string; label: string }[] {
  const active = (items ?? []).filter((item) => item.isActive !== false && item.code.trim());
  const source = active.length > 0
    ? active
    : canonicalModes.map((item) => ({ code: item.code, name: item.label, isActive: true }));
  return source
    .filter((item) => {
      const code = item.code.trim().toUpperCase();
      return code !== "WATER" && code !== "EXPRESS";
    })
    .map((item) => ({
      value: storedMode(item.code) ?? item.code.toLowerCase(),
      label: catalogLabel(item.code, item.name),
    }));
}

export function serviceBlock(code: string | null | undefined, alreadyStored: boolean): string | null {
  if (alreadyStored || code?.trim().toUpperCase() !== "AIR") return null;
  return serviceAirBlocked;
}

export function modeBlock(mode: string | null | undefined): string | null {
  const stored = storedMode(mode);
  if (stored === "water") return waterBlocked;
  if (stored === "express") return expressBlocked;
  return null;
}

export function routeModeConflictMessage(
  routeMode: string | null | undefined,
  ruleMode: string | null | undefined,
  appliesAnyRoute: boolean,
): string | null {
  if (appliesAnyRoute) return null;
  const route = storedMode(routeMode);
  const rule = storedMode(ruleMode);
  if (!route || !rule || route === "multimodal") return null;
  return route === rule ? null : modeRouteConflict;
}

export function suggestRuleName(charge: string, route: string, min: string, max: string): string {
  const parts = [charge.trim(), route.trim()].filter(Boolean);
  if (min.trim() || max.trim()) {
    parts.push(max.trim() ? `${min.trim() || "0"} – < ${max.trim()}` : `≥ ${min.trim() || "0"}`);
  }
  return parts.join(" · ");
}

function sameCode(left: string, right: string | null | undefined): boolean {
  return left.trim().toUpperCase() === (right ?? "").trim().toUpperCase();
}
