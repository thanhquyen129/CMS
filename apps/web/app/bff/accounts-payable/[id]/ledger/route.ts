import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** GET /bff/accounts-payable/:id/ledger → /api/accounts-payable/:id/ledger */
export async function GET(_req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/accounts-payable/${encodeURIComponent(id)}/ledger`);
}
