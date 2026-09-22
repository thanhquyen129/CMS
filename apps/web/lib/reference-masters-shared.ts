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
