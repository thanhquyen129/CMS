import type { PartyDuplicateHit, PartyLookupItem } from "./party";

export type { PartyDuplicateHit, PartyLookupItem };

/** Browser typeahead — BFF only; do not import server `lib/parties` (next/headers). */
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
