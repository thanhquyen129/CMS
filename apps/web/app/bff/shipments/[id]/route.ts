import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** GET /bff/shipments/:id → /api/shipments/:id */
export async function GET(_req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/shipments/${encodeURIComponent(id)}`);
}
