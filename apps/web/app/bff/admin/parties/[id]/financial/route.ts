import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string }> };

export async function GET(_req: NextRequest, ctx: Ctx) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/business-parties/${id}/financial`);
}
