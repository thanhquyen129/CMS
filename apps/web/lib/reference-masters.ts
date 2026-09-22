import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type {
  LocationAlias,
  LocationItem,
  RouteStop,
  RouteItem,
  CommodityItem,
  PartySnapshotItem,
} from "./reference-masters-shared";
export { LOCATION_TYPE_OPTIONS, locationTypeLabel } from "./reference-masters-shared";
import type { CommodityItem, LocationItem, PartySnapshotItem, RouteItem } from "./reference-masters-shared";

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

export function listPartySnapshots(objectType: string, objectId: string) {
  const q = new URLSearchParams({ objectType, objectId, currentOnly: "true" });
  return apiGet<PartySnapshotItem[]>(`/api/party-snapshots?${q.toString()}`);
}
