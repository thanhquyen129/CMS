import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ roleId: string }> };

export async function PUT(req: NextRequest, ctx: Ctx) {
  const { roleId } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("PUT", `/api/roles/${roleId}/permissions`, body);
}
