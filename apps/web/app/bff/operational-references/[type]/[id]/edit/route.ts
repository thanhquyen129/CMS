import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ type: string; id: string }>;

/** GET /bff/operational-references/:type/:id/edit → /api/operational-references/:type/:id/edit */
export async function GET(_req: Request, ctx: { params: Params }) {
  const { type, id } = await ctx.params;
  return forwardApiGet(
    `/api/operational-references/${encodeURIComponent(type)}/${encodeURIComponent(id)}/edit`
  );
}
