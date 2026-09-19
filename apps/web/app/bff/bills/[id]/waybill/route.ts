import { NextRequest } from "next/server";
import { forwardApiGet, forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** GET /bff/bills/:id/waybill → /api/bills/:id/waybill */
export async function GET(_req: Request, ctx: { params: Params }) {
  const { id } = await ctx.params;
  return forwardApiGet(`/api/bills/${encodeURIComponent(id)}/waybill`);
}

/** PUT /bff/bills/:id/waybill → /api/bills/:id/waybill */
export async function PUT(req: NextRequest, ctx: { params: Params }) {
  const { id } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "PUT",
    `/api/bills/${encodeURIComponent(id)}/waybill`,
    body
  );
}
