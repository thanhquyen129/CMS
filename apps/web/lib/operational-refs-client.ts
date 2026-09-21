import type { OrderDetail } from "./operational-refs";

export type ClientResult<T> =
  | { ok: true; data: T }
  | { ok: false; message: string };

export async function fetchOrderClient(id: string): Promise<ClientResult<OrderDetail>> {
  try {
    const res = await fetch(`/bff/orders/${encodeURIComponent(id)}`, {
      headers: { Accept: "application/json" },
      cache: "no-store",
    });
    if (res.status === 401) {
      window.location.href = "/login";
      return { ok: false, message: "Phiên đăng nhập đã hết." };
    }
    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return { ok: false, message: body.message || "Không tải được đơn hàng." };
    }
    return { ok: true, data: (await res.json()) as OrderDetail };
  } catch {
    return { ok: false, message: "Không kết nối được máy chủ." };
  }
}
