import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function GET(req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  const query = new URL(req.url).searchParams.toString();
  const path = `/api/bills/${encodeURIComponent(id)}/rate-suggestions${query ? `?${query}` : ""}`;
  return forwardApiGet(path);
}
