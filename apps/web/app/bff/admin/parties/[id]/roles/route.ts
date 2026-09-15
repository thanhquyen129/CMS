import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string }> };

export async function POST(req: NextRequest, ctx: Ctx) {
  const { id } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("POST", `/api/business-parties/${id}/roles`, body);
}
