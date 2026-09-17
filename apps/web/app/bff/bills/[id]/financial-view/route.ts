import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** GET /bff/bills/:id/financial-view → /api/bills/:id/financial-view */
export async function GET(
  _req: Request,
  ctx: { params: Params }
) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/bills/${encodeURIComponent(id)}/financial-view`);
}
