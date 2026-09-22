import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ billId: string; legId: string }> };

export async function DELETE(req: NextRequest, ctx: Ctx) {
  const { billId, legId } = await ctx.params;
  const reason = req.nextUrl.searchParams.get("reason");
  const qs = reason ? `?reason=${encodeURIComponent(reason)}` : "";
  return forwardApiMutation("DELETE", `/api/bills/${billId}/legs/${legId}${qs}`);
}
