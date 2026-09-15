import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string; roleCode: string }> };

export async function DELETE(_req: NextRequest, ctx: Ctx) {
  const { id, roleCode } = await ctx.params;
  return forwardApiMutation(
    "DELETE",
    `/api/business-parties/${id}/roles/${encodeURIComponent(roleCode)}`
  );
}
