import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string; contactId: string }> };

export async function PUT(req: NextRequest, ctx: Ctx) {
  const { id, contactId } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "PUT",
    `/api/business-parties/${id}/contacts/${contactId}`,
    body
  );
}

export async function DELETE(_req: NextRequest, ctx: Ctx) {
  const { id, contactId } = await ctx.params;
  return forwardApiMutation(
    "DELETE",
    `/api/business-parties/${id}/contacts/${contactId}`
  );
}
