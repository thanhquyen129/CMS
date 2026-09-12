import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function POST(_req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  return forwardApiMutation("POST", `/api/payment-allocations/${id}/finalize`);
}
