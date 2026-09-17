import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

/** PATCH /bff/bills/:id/context → /api/bills/:id/context */
export async function PATCH(
  req: NextRequest,
  ctx: { params: Params }
) {
  const { id } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "PATCH",
    `/api/bills/${encodeURIComponent(id)}/context`,
    body
  );
}
