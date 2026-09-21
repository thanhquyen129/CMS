import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** GET /bff/orders/:id → /api/orders/:id */
export async function GET(_req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/orders/${encodeURIComponent(id)}`);
}
