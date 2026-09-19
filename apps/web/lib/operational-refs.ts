import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type OperationalBillRef = {
  id: string;
  billNo: string;
  operationalStatus: string;
};

export type OrderListItem = {
  id: string;
  orderNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
  isActive: boolean;
  createdAt: string;
};

export type OrderDetail = OrderListItem & {
  tenantId: string;
  externalVersion: string | null;
  relatedBills: OperationalBillRef[];
};

export type ShipmentListItem = {
  id: string;
  shipmentNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
  isActive: boolean;
  createdAt: string;
};

export type ShipmentDetail = ShipmentListItem & {
  tenantId: string;
  externalVersion: string | null;
  relatedBills: OperationalBillRef[];
  legs: { id: string; legNo: string; operationalStatus: string }[];
};

export type LegListItem = {
  id: string;
  shipmentId: string;
  legNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
  isActive: boolean;
  createdAt: string;
};

export type LegDetail = LegListItem & {
  tenantId: string;
  shipmentNo: string | null;
  externalVersion: string | null;
  relatedBills: OperationalBillRef[];
  movements: { id: string; movementNo: string; operationalStatus: string }[];
};

export type MovementListItem = {
  id: string;
  movementNo: string;
  sourceSystem: string;
  externalId: string;
  operationalStatus: string;
  isActive: boolean;
  createdAt: string;
};

export type MovementDetail = MovementListItem & {
  tenantId: string;
  externalVersion: string | null;
  relatedBills: OperationalBillRef[];
  legs: {
    id: string;
    legNo: string;
    shipmentId: string;
    operationalStatus: string;
  }[];
};

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
            ? "Bạn không có quyền xem tham chiếu vận hành."
            : "Không tải được dữ liệu."),
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

export function listOrders(q?: string): Promise<ApiResult<OrderListItem[]>> {
  const qs = q ? `?q=${encodeURIComponent(q)}` : "";
  return apiGet<OrderListItem[]>(`/api/orders${qs}`);
}

export function getOrder(id: string): Promise<ApiResult<OrderDetail>> {
  return apiGet<OrderDetail>(`/api/orders/${encodeURIComponent(id)}`);
}

export function listShipments(q?: string): Promise<ApiResult<ShipmentListItem[]>> {
  const qs = q ? `?q=${encodeURIComponent(q)}` : "";
  return apiGet<ShipmentListItem[]>(`/api/shipments${qs}`);
}

export function getShipment(id: string): Promise<ApiResult<ShipmentDetail>> {
  return apiGet<ShipmentDetail>(`/api/shipments/${encodeURIComponent(id)}`);
}

export function listLegs(
  q?: string,
  shipmentId?: string
): Promise<ApiResult<LegListItem[]>> {
  const p = new URLSearchParams();
  if (q) p.set("q", q);
  if (shipmentId) p.set("shipmentId", shipmentId);
  const qs = p.toString();
  return apiGet<LegListItem[]>(`/api/transport-legs${qs ? `?${qs}` : ""}`);
}

export function getLeg(id: string): Promise<ApiResult<LegDetail>> {
  return apiGet<LegDetail>(`/api/transport-legs/${encodeURIComponent(id)}`);
}

export function listMovements(q?: string): Promise<ApiResult<MovementListItem[]>> {
  const qs = q ? `?q=${encodeURIComponent(q)}` : "";
  return apiGet<MovementListItem[]>(`/api/transport-movements${qs}`);
}

export function getMovement(id: string): Promise<ApiResult<MovementDetail>> {
  return apiGet<MovementDetail>(
    `/api/transport-movements/${encodeURIComponent(id)}`
  );
}

export function sourceSystemLabel(source: string): string {
  if (source?.toLowerCase() === "lcms_manual") return "Nhập tay LCMS";
  return source || "—";
}

export function operationalRefCode(row: {
  orderNo?: string | null;
  shipmentNo?: string | null;
  legNo?: string | null;
  movementNo?: string | null;
}): string {
  return row.orderNo || row.shipmentNo || row.legNo || row.movementNo || "—";
}
