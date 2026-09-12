import { cookies } from "next/headers";
import { AUTH_COOKIE, getApiInternalUrl } from "./auth";
import type { TerminologyMap } from "./terminology";

export type { TerminologyMap } from "./terminology";
export { term } from "./terminology";

export async function fetchTerminology(): Promise<TerminologyMap> {
  const base = getApiInternalUrl();
  try {
    const res = await fetch(`${base}/api/terminology`, {
      next: { revalidate: 300 },
    });
    if (!res.ok) return {};
    return (await res.json()) as TerminologyMap;
  } catch {
    return {};
  }
}

export async function getSessionToken(): Promise<string | undefined> {
  const jar = await cookies();
  return jar.get(AUTH_COOKIE)?.value;
}
