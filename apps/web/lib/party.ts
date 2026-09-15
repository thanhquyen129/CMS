export type BusinessParty = {
  id: string;
  code: string;
  name: string;
  legalName?: string | null;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  ward?: string | null;
  district?: string | null;
  city?: string | null;
  province?: string | null;
  countryCode?: string | null;
  postalCode?: string | null;
  defaultCurrencyCode?: string | null;
  paymentTermDays?: number | null;
  creditLimit?: number | null;
  creditLimitCurrencyCode?: string | null;
  notes?: string | null;
  isActive: boolean;
  roleCodes?: string[];
  createdAt?: string;
  updatedAt?: string | null;
};

export type PartyBankAccount = {
  id: string;
  partyId: string;
  bankName: string;
  bankBranch?: string | null;
  accountNumber: string;
  accountName?: string | null;
  currencyCode: string;
  isDefault: boolean;
  isActive: boolean;
  note?: string | null;
  createdAt: string;
};

export type PartyContact = {
  id: string;
  partyId: string;
  fullName: string;
  title?: string | null;
  phone?: string | null;
  email?: string | null;
  isPrimary: boolean;
  isActive: boolean;
  note?: string | null;
  createdAt: string;
};

export const PARTY_ROLE_OPTIONS = [
  { code: "customer", label: "Khách hàng" },
  { code: "vendor", label: "Nhà cung cấp" },
  { code: "payer", label: "Bên trả tiền" },
  { code: "payee", label: "Bên nhận tiền" },
] as const;

export function partyLabel(p: Pick<BusinessParty, "code" | "name">): string {
  return `${p.code} — ${p.name}`;
}

export function partyRoleLabel(code: string): string {
  return PARTY_ROLE_OPTIONS.find((r) => r.code === code)?.label ?? code;
}

export function formatCreditLimit(
  amount: number | null | undefined,
  currency: string | null | undefined
): string {
  if (amount == null) return "—";
  const cur = currency?.trim() || "VND";
  try {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: cur,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${amount.toLocaleString("vi-VN")} ${cur}`;
  }
}
