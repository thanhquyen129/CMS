import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type {
  BusinessParty,
  PartyBankAccount,
  PartyContact,
  PartyDirectoryPage,
  PartyDirectorySummary,
  PartyDuplicateHit,
  PartyFinancialView,
  PartyLookupItem,
} from "./party";

export type {
  BusinessParty,
  PartyBankAccount,
  PartyContact,
  PartyLookupItem,
  PartyDuplicateHit,
} from "./party";
export {
  partyLabel,
  partyRoleLabel,
  partyKindLabel,
  partyStatusLabel,
  partyCreditModeLabel,
  formatCreditLimit,
  creditStatusLabel,
  PARTY_ROLE_OPTIONS,
  PARTY_KIND_OPTIONS,
  PARTY_LEGAL_TYPE_OPTIONS,
  PARTY_CREDIT_MODE_OPTIONS,
  PARTY_CONTACT_FUNCTION_OPTIONS,
} from "./party";

function qs(params: Record<string, string | undefined>): string {
  const sp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v != null && v !== "") sp.set(k, v);
  }
  const s = sp.toString();
  return s ? `?${s}` : "";
}

async function apiGet<T>(path: string): Promise<ApiResult<T>> {
  const token = await getSessionToken();
  if (!token) {
    redirect("/login");
  }

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (res.status === 401) {
      redirect("/login");
    }

    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message:
          body.message ||
          (res.status === 403
            ? "Bạn không có quyền xem đối tác."
            : "Không tải được dữ liệu đối tác."),
      };
    }

    return { ok: true, data: (await res.json()) as T };
  } catch {
    return {
      ok: false,
      status: 0,
      message: "Không kết nối được máy chủ API. Thử lại sau.",
    };
  }
}

export function listBusinessParties(opts?: {
  search?: string;
  roleCode?: string;
  isActive?: string;
}): Promise<ApiResult<BusinessParty[]>> {
  return apiGet<BusinessParty[]>(
    `/api/business-parties${qs({
      search: opts?.search,
      roleCode: opts?.roleCode,
      isActive: opts?.isActive,
    })}`
  );
}

export function getBusinessParty(
  id: string
): Promise<ApiResult<BusinessParty>> {
  return apiGet<BusinessParty>(`/api/business-parties/${id}`);
}

export function listPartyBankAccounts(
  partyId: string
): Promise<ApiResult<PartyBankAccount[]>> {
  return apiGet<PartyBankAccount[]>(
    `/api/business-parties/${partyId}/bank-accounts`
  );
}

export function listPartyContacts(
  partyId: string
): Promise<ApiResult<PartyContact[]>> {
  return apiGet<PartyContact[]>(`/api/business-parties/${partyId}/contacts`);
}

export function listPartyDirectory(opts?: {
  search?: string;
  roleCode?: string;
  status?: string;
  kind?: string;
  groupCode?: string;
  page?: string;
  pageSize?: string;
}): Promise<ApiResult<PartyDirectoryPage>> {
  return apiGet<PartyDirectoryPage>(
    `/api/business-parties/directory${qs({
      search: opts?.search,
      roleCode: opts?.roleCode,
      status: opts?.status,
      kind: opts?.kind,
      groupCode: opts?.groupCode,
      page: opts?.page,
      pageSize: opts?.pageSize,
    })}`
  );
}

export function getPartyDirectorySummary(opts?: {
  search?: string;
  roleCode?: string;
  status?: string;
  kind?: string;
  groupCode?: string;
}): Promise<ApiResult<PartyDirectorySummary>> {
  return apiGet<PartyDirectorySummary>(
    `/api/business-parties/summary${qs({
      search: opts?.search,
      roleCode: opts?.roleCode,
      status: opts?.status,
      kind: opts?.kind,
      groupCode: opts?.groupCode,
    })}`
  );
}

export function getPartyFinancial(
  id: string
): Promise<ApiResult<PartyFinancialView>> {
  return apiGet<PartyFinancialView>(`/api/business-parties/${id}/financial`);
}

export function lookupPartiesClient(opts: {
  q?: string;
  roleCode?: string;
  usableOnly?: boolean;
  take?: number;
}): Promise<PartyLookupItem[]> {
  const sp = new URLSearchParams();
  if (opts.q) sp.set("q", opts.q);
  if (opts.roleCode) sp.set("roleCode", opts.roleCode);
  if (opts.usableOnly === false) sp.set("usableOnly", "false");
  if (opts.take) sp.set("take", String(opts.take));
  return fetch(`/bff/admin/parties/lookup?${sp.toString()}`, {
    headers: { Accept: "application/json" },
    cache: "no-store",
  }).then(async (res) => {
    if (!res.ok) return [];
    return (await res.json()) as PartyLookupItem[];
  });
}

export async function findPartyDuplicates(opts: {
  taxId?: string;
  phone?: string;
  email?: string;
  excludeId?: string;
}): Promise<PartyDuplicateHit[]> {
  const sp = new URLSearchParams();
  if (opts.taxId) sp.set("taxId", opts.taxId);
  if (opts.phone) sp.set("phone", opts.phone);
  if (opts.email) sp.set("email", opts.email);
  if (opts.excludeId) sp.set("excludeId", opts.excludeId);
  const res = await fetch(`/bff/admin/parties/duplicates?${sp.toString()}`, {
    headers: { Accept: "application/json" },
    cache: "no-store",
  });
  if (!res.ok) return [];
  return (await res.json()) as PartyDuplicateHit[];
}
