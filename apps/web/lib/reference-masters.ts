import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type LocationAlias = { aliasCode: string; sourceSystem: string | null };

export type LocationItem = {
  id: string;
  code: string;
  name: string;
  locationType: string;
  countryCode: string | null;
  subdivision: string | null;
  city: string | null;
  iataCode: string | null;
  unlocode: string | null;
  terminalCode: string | null;
  isActive: boolean;
  aliases: LocationAlias[];
};

export type RouteStop = { sequenceNo: number; locationId: string; locationCode: string };

export type RouteItem = {
  id: string;
  code: string;
  name: string;
  originLocationId: string;
  originCode: string;
  destinationLocationId: string;
  destinationCode: string;
  transportModeCode: string | null;
  serviceTypeCode: string | null;
  isActive: boolean;
  stops: RouteStop[];
};

export type CommodityItem = {
  id: string;
  code: string;
  name: string;
  category: string | null;
  parentId: string | null;
  isDangerousGoods: boolean;
  isTemperatureControlled: boolean;
  isOversize: boolean;
  isOverweight: boolean;
  isHighValue: boolean;
  specialHandling: string | null;
  isActive: boolean;
};

export const LOCATION_TYPE_OPTIONS = [
  { id: "airport", label: "Sân bay" },
  { id: "port", label: "Cảng" },
  { id: "city", label: "Thành phố" },
  { id: "depot", label: "Kho / depot" },
  { id: "border", label: "Cửa khẩu" },
  { id: "other", label: "Khác" },
] as const;

export function locationTypeLabel(code: string): string {
  return LOCATION_TYPE_OPTIONS.find((t) => t.id === code)?.label ?? code;
}

async function apiGet<T>(path: string): Promise<ApiResult<T>> {
  const token = await getSessionToken();
  if (!token) redirect("/login");
  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
      cache: "no-store",
    });
    if (res.status === 401) redirect("/login");
    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return { ok: false, status: res.status, message: body.message || "Không tải được danh mục." };
    }
    return { ok: true, data: (await res.json()) as T };
  } catch {
    return { ok: false, status: 502, message: "Không kết nối được máy chủ." };
  }
}

export function listLocations(activeOnly = false) {
  return apiGet<LocationItem[]>(`/api/locations?activeOnly=${activeOnly}`);
}

export function listRoutes(activeOnly = false) {
  return apiGet<RouteItem[]>(`/api/routes?activeOnly=${activeOnly}`);
}

export function listCommodities(activeOnly = false) {
  return apiGet<CommodityItem[]>(`/api/commodities?activeOnly=${activeOnly}`);
}

export type PartySnapshotItem = {
  id: string;
  roleCode: string;
  partyId: string | null;
  isWalkIn: boolean;
  displayName: string;
  taxId: string | null;
  phone: string | null;
  email: string | null;
  addressLine1: string | null;
  capturedAt: string;
  supersededAt: string | null;
};

export function listPartySnapshots(objectType: string, objectId: string) {
  const q = new URLSearchParams({ objectType, objectId, currentOnly: "true" });
  return apiGet<PartySnapshotItem[]>(`/api/party-snapshots?${q.toString()}`);
}
