import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string }> };

export async function POST(_req: Request, ctx: Ctx) {
  const { id } = await ctx.params;
  return forwardApiMutation("POST", `/api/reconciliations/${id}/complete`);
}
