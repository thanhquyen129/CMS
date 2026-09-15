/**
 * Địa chỉ hành chính VN sau sáp nhập (2025): Phường/Xã → Tỉnh/Thành phố.
 * Địa chỉ cũ: Phường/Xã → Quận/Huyện → Tỉnh/Thành phố.
 */

export type VnAddressScheme = "new" | "legacy";

/** 34 đơn vị hành chính cấp tỉnh sau sắp xếp (gợi ý chọn). */
export const VN_PROVINCES_NEW: string[] = [
  "An Giang",
  "Bắc Ninh",
  "Cà Mau",
  "Cao Bằng",
  "Cần Thơ",
  "Đà Nẵng",
  "Đắk Lắk",
  "Điện Biên",
  "Đồng Nai",
  "Đồng Tháp",
  "Gia Lai",
  "Hà Nội",
  "Hà Tĩnh",
  "Hải Phòng",
  "Huế",
  "Hưng Yên",
  "Khánh Hòa",
  "Lai Châu",
  "Lâm Đồng",
  "Lạng Sơn",
  "Lào Cai",
  "Nghệ An",
  "Ninh Bình",
  "Phú Thọ",
  "Quảng Ngãi",
  "Quảng Ninh",
  "Quảng Trị",
  "Sơn La",
  "Tây Ninh",
  "Thái Nguyên",
  "Thanh Hóa",
  "TP. Hồ Chí Minh",
  "Tuyên Quang",
  "Vĩnh Long",
].sort((a, b) => a.localeCompare(b, "vi"));

/** Gợi ý chi nhánh phổ biến theo tỉnh/TP + hội sở. */
export function suggestBankBranches(bankShortName?: string | null): string[] {
  const bank = bankShortName?.trim() || "Ngân hàng";
  const base = [
    "Hội sở chính",
    "Hội sở giao dịch",
    ...VN_PROVINCES_NEW.map((p) => `Chi nhánh ${p}`),
  ];
  // Prefixed variants help search when many banks share province names.
  return [
    `${bank} — Hội sở chính`,
    ...VN_PROVINCES_NEW.slice(0, 12).map((p) => `${bank} — Chi nhánh ${p}`),
    ...base,
  ];
}

export function inferAddressScheme(district?: string | null): VnAddressScheme {
  return district?.trim() ? "legacy" : "new";
}
