export type PartyStatusCode = "active" | "inactive" | "blocked";

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
  isBlocked?: boolean;
  statusCode?: PartyStatusCode;
  partyKind?: string;
  shortName?: string | null;
  legalType?: string | null;
  groupCode?: string | null;
  externalCode?: string | null;
  industryCode?: string | null;
  invoiceEmail?: string | null;
  vatRegistered?: boolean | null;
  assignedUserId?: string | null;
  parentPartyId?: string | null;
  parentPartyCode?: string | null;
  parentPartyName?: string | null;
  creditControlMode?: string;
  blockedReason?: string | null;
  blockedAt?: string | null;
  roleCodes?: string[];
  createdAt?: string;
  updatedAt?: string | null;
};

export type PartyBankAccount = {
  id: string;
  partyId: string;
  bankName: string;
  bankBranch?: string | null;
  bankCode?: string | null;
  swiftBic?: string | null;
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
  functionCode?: string | null;
  phone?: string | null;
  email?: string | null;
  isPrimary: boolean;
  isActive: boolean;
  note?: string | null;
  createdAt: string;
};

export type PartyLookupItem = {
  id: string;
  code: string;
  name: string;
  shortName?: string | null;
  legalName?: string | null;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  isActive: boolean;
  isBlocked: boolean;
  statusCode: PartyStatusCode;
  roleCodes: string[];
  defaultCurrencyCode?: string | null;
  paymentTermDays?: number | null;
  creditStatus?: string | null;
  creditMessage?: string | null;
};

export type PartyDirectoryItem = {
  id: string;
  code: string;
  name: string;
  shortName?: string | null;
  legalName?: string | null;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  partyKind: string;
  groupCode?: string | null;
  isActive: boolean;
  isBlocked: boolean;
  statusCode: PartyStatusCode;
  defaultCurrencyCode?: string | null;
  paymentTermDays?: number | null;
  creditLimit?: number | null;
  creditLimitCurrencyCode?: string | null;
  creditControlMode: string;
  roleCodes: string[];
  apOutstanding?: number | null;
  arOutstanding?: number | null;
  creditStatus?: string | null;
  createdAt: string;
};

export type PartyDirectoryPage = {
  items: PartyDirectoryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type PartyDirectorySummary = {
  total: number;
  active: number;
  inactive: number;
  blocked: number;
};

export type PartyDuplicateHit = {
  id: string;
  code: string;
  name: string;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  matchOn: string;
};

export type PartyCreditEvaluation = {
  mode: string;
  creditLimit?: number | null;
  creditLimitCurrencyCode?: string | null;
  arOutstandingSameCurrency: number;
  utilizationPercent?: number | null;
  status: string;
  wouldBlock: boolean;
  message?: string | null;
};

export type PartyMoneyBucket = {
  currencyCode: string;
  outstanding: number;
  openCount: number;
};

export type PartyFinancialView = {
  partyId: string;
  code: string;
  name: string;
  canViewAp: boolean;
  canViewAr: boolean;
  canViewBills: boolean;
  apByCurrency?: PartyMoneyBucket[] | null;
  arByCurrency?: PartyMoneyBucket[] | null;
  apOutstandingTotal?: number | null;
  arOutstandingTotal?: number | null;
  apOverdueCount?: number | null;
  arOverdueCount?: number | null;
  credit: PartyCreditEvaluation;
  billCount: number;
  costCount: number;
  revenueCount: number;
  documentCount: number;
  recentBills: Array<{
    id: string;
    billNo: string;
    createdAt: string;
    operationalStatus?: string | null;
  }>;
  recentDocuments: Array<{
    id: string;
    documentNo: string;
    documentType: string;
    direction: string;
    totalAmount: number;
    currencyCode: string;
    receivedAt?: string | null;
  }>;
};

export const PARTY_ROLE_OPTIONS = [
  { code: "customer", label: "Khách hàng" },
  { code: "vendor", label: "Nhà cung cấp" },
  { code: "payer", label: "Bên trả tiền" },
  { code: "payee", label: "Bên nhận tiền" },
  { code: "bill_to", label: "Bên nhận hóa đơn" },
  { code: "shipper", label: "Người gửi hàng" },
  { code: "consignee", label: "Người nhận hàng" },
  { code: "carrier", label: "Hãng vận chuyển" },
  { code: "agent", label: "Đại lý" },
] as const;

export const PARTY_KIND_OPTIONS = [
  { code: "organization", label: "Tổ chức" },
  { code: "individual", label: "Cá nhân" },
] as const;

export const PARTY_LEGAL_TYPE_OPTIONS = [
  { code: "company", label: "Công ty" },
  { code: "llc", label: "TNHH" },
  { code: "jsc", label: "Cổ phần" },
  { code: "individual", label: "Cá nhân" },
  { code: "household", label: "Hộ kinh doanh" },
  { code: "foreign", label: "Nước ngoài" },
  { code: "other", label: "Khác" },
] as const;

export const PARTY_CREDIT_MODE_OPTIONS = [
  { code: "advisory", label: "Tham chiếu — chỉ hiển thị" },
  { code: "warn", label: "Cảnh báo khi gần/vượt hạn mức" },
  { code: "block", label: "Chặn ghi nhận phải thu khi vượt hạn mức" },
] as const;

export const PARTY_CONTACT_FUNCTION_OPTIONS = [
  { code: "general", label: "Chung" },
  { code: "billing", label: "Công nợ / hóa đơn" },
  { code: "ops", label: "Điều vận" },
  { code: "legal", label: "Pháp chế" },
] as const;

export function partyLabel(p: Pick<BusinessParty, "code" | "name">): string {
  return `${p.code} — ${p.name}`;
}

export function partyRoleLabel(code: string): string {
  return PARTY_ROLE_OPTIONS.find((r) => r.code === code)?.label ?? code;
}

export function partyKindLabel(code: string | null | undefined): string {
  return PARTY_KIND_OPTIONS.find((k) => k.code === code)?.label ?? code ?? "—";
}

export function partyLegalTypeLabel(code: string | null | undefined): string {
  if (!code) return "—";
  return PARTY_LEGAL_TYPE_OPTIONS.find((k) => k.code === code)?.label ?? code;
}

export function partyStatusLabel(code: string | null | undefined, fallbackActive?: boolean): string {
  const c = code ?? (fallbackActive === false ? "inactive" : "active");
  if (c === "blocked") return "Bị chặn";
  if (c === "inactive") return "Ngừng";
  return "Đang dùng";
}

export function partyCreditModeLabel(code: string | null | undefined): string {
  return PARTY_CREDIT_MODE_OPTIONS.find((k) => k.code === code)?.label ?? code ?? "—";
}

export function partyContactFunctionLabel(code: string | null | undefined): string {
  return (
    PARTY_CONTACT_FUNCTION_OPTIONS.find((k) => k.code === code)?.label ??
    code ??
    "Chung"
  );
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

export function creditStatusLabel(code: string | null | undefined): string {
  switch (code) {
    case "over":
      return "Vượt hạn mức";
    case "watch":
      return "Sắp hết hạn mức";
    case "ok":
      return "Trong hạn mức";
    case "blocked":
      return "Bị chặn";
    case "inactive":
      return "Ngừng";
    default:
      return "Chưa đặt hạn mức";
  }
}
