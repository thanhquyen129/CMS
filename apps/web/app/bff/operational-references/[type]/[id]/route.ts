import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ type: string; id: string }>;

/** PATCH /bff/operational-references/:type/:id → /api/operational-references/:type/:id (If-Match forwarded) */
export async function PATCH(req: NextRequest, ctx: { params: Params }) {
  const { type, id } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "PATCH",
    `/api/operational-references/${encodeURIComponent(type)}/${encodeURIComponent(id)}`,
    body
  );
}
