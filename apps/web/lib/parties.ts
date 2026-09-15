import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { BusinessParty, PartyBankAccount, PartyContact } from "./party";

export type { BusinessParty, PartyBankAccount, PartyContact } from "./party";
export {
  partyLabel,
  partyRoleLabel,
  formatCreditLimit,
  PARTY_ROLE_OPTIONS,
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
