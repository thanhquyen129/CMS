import raw from "./data/vietqr-banks.json";

export type VnBank = {
  id: number;
  code: string;
  bin: string;
  shortName: string;
  name: string;
  logo: string;
  transferSupported: number;
  lookupSupported: number;
};

type VietQrResponse = {
  code: string;
  desc: string;
  data: Array<{
    id: number;
    name: string;
    code: string;
    bin: string;
    shortName?: string;
    short_name?: string;
    logo: string;
    transferSupported?: number;
    lookupSupported?: number;
  }>;
};

const payload = raw as VietQrResponse;

/** Snapshot VietQR v2 — ngân hàng/tổ chức đang hỗ trợ chuyển khoản tại VN. */
export const VN_BANKS: VnBank[] = (payload.data ?? [])
  .map((b) => ({
    id: b.id,
    code: b.code,
    bin: b.bin,
    shortName: (b.shortName || b.short_name || b.code).trim(),
    name: b.name.trim(),
    logo: b.logo,
    transferSupported: b.transferSupported ?? 0,
    lookupSupported: b.lookupSupported ?? 0,
  }))
  .sort((a, b) => a.shortName.localeCompare(b.shortName, "vi"));

export function findVnBank(query: string | null | undefined): VnBank | undefined {
  if (!query?.trim()) return undefined;
  const q = query.trim().toLowerCase();
  return (
    VN_BANKS.find(
      (b) =>
        b.shortName.toLowerCase() === q ||
        b.code.toLowerCase() === q ||
        b.bin === q ||
        b.name.toLowerCase() === q
    ) ??
    VN_BANKS.find(
      (b) =>
        b.shortName.toLowerCase().includes(q) ||
        b.name.toLowerCase().includes(q) ||
        b.code.toLowerCase().includes(q)
    )
  );
}

export function searchVnBanks(query: string, limit = 12): VnBank[] {
  const q = query.trim().toLowerCase();
  if (!q) return VN_BANKS.slice(0, limit);
  return VN_BANKS.filter(
    (b) =>
      b.shortName.toLowerCase().includes(q) ||
      b.name.toLowerCase().includes(q) ||
      b.code.toLowerCase().includes(q) ||
      b.bin.includes(q)
  ).slice(0, limit);
}

export function bankDisplayLabel(bank: VnBank): string {
  return `${bank.shortName} — ${bank.name}`;
}
