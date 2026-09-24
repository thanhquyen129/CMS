import { getApiInternalUrl, AUTH_COOKIE } from "./auth";
import { cookies } from "next/headers";

export type FieldOwnershipItem = {
  fieldName: string;
  ownerSystem: string;
  overrideReason?: string | null;
  overriddenAt?: string | null;
};

export async function getFieldOwnerships(
  objectType: string,
  objectId: string
): Promise<FieldOwnershipItem[]> {
  const token = (await cookies()).get(AUTH_COOKIE)?.value;
  if (!token) return [];

  const params = new URLSearchParams({ objectType, objectId });
  try {
    const res = await fetch(
      `${getApiInternalUrl()}/api/field-ownerships?${params}`,
      {
        headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
        cache: "no-store",
      }
    );
    if (!res.ok) return [];
    const data = (await res.json()) as FieldOwnershipItem[];
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
}
