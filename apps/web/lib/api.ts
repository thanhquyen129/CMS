import { cookies } from "next/headers";
import { AUTH_COOKIE, getApiInternalUrl } from "./auth";

export type TerminologyMap = Record<string, string>;

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

export function term(map: TerminologyMap, key: string, fallback: string): string {
  return map[key] ?? fallback;
}

export async function getSessionToken(): Promise<string | undefined> {
  const jar = await cookies();
  return jar.get(AUTH_COOKIE)?.value;
}
