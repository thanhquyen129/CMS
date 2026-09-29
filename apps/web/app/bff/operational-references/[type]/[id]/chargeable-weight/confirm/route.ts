import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ type: string; id: string }>;

/** POST /bff/operational-references/:type/:id/chargeable-weight/confirm */
export async function POST(_req: Request, ctx: { params: Params }) {
  const { type, id } = await ctx.params;
  return forwardApiMutation(
    "POST",
    `/api/operational-references/${encodeURIComponent(type)}/${encodeURIComponent(id)}/chargeable-weight/confirm`,
    {}
  );
}
