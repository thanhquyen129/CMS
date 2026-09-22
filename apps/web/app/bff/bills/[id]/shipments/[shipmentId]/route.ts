import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string; shipmentId: string }> };

export async function DELETE(req: NextRequest, ctx: Ctx) {
  const { id, shipmentId } = await ctx.params;
  const reason = req.nextUrl.searchParams.get("reason");
  const qs = reason ? `?reason=${encodeURIComponent(reason)}` : "";
  return forwardApiMutation(
    "DELETE",
    `/api/bills/${id}/shipments/${shipmentId}${qs}`
  );
}
