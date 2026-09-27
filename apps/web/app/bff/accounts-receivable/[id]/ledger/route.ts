import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** GET /bff/accounts-receivable/:id/ledger → /api/accounts-receivable/:id/ledger */
export async function GET(_req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/accounts-receivable/${encodeURIComponent(id)}/ledger`);
}
