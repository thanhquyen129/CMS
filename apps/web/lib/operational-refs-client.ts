import type { OrderDetail, ShipmentDetail } from "./operational-refs";

export type ClientResult<T> =
  | { ok: true; data: T }
  | { ok: false; message: string };

export function sourceSystemLabel(source: string): string {
  if (source?.toLowerCase() === "lcms_manual") return "Nhập tay LCMS";
  return source || "—";
}

async function fetchJsonClient<T>(path: string, failMsg: string): Promise<ClientResult<T>> {
  try {
    const res = await fetch(path, {
      headers: { Accept: "application/json" },
      cache: "no-store",
    });
    if (res.status === 401) {
      window.location.href = "/login";
      return { ok: false, message: "Phiên đăng nhập đã hết." };
    }
    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return { ok: false, message: body.message || failMsg };
    }
    return { ok: true, data: (await res.json()) as T };
  } catch {
    return { ok: false, message: "Không kết nối được máy chủ." };
  }
}

export function fetchOrderClient(id: string): Promise<ClientResult<OrderDetail>> {
  return fetchJsonClient<OrderDetail>(
    `/bff/orders/${encodeURIComponent(id)}`,
    "Không tải được đơn hàng."
  );
}

export function fetchShipmentClient(id: string): Promise<ClientResult<ShipmentDetail>> {
  return fetchJsonClient<ShipmentDetail>(
    `/bff/shipments/${encodeURIComponent(id)}`,
    "Không tải được Shipment."
  );
}
