import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ orderId: string; billId: string }> };

export async function DELETE(req: NextRequest, ctx: Ctx) {
  const { orderId, billId } = await ctx.params;
  const reason = req.nextUrl.searchParams.get("reason");
  const qs = reason ? `?reason=${encodeURIComponent(reason)}` : "";
  return forwardApiMutation("DELETE", `/api/orders/${orderId}/bills/${billId}${qs}`);
}
