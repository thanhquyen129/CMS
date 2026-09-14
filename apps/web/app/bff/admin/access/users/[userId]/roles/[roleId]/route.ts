import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ userId: string; roleId: string }> };

export async function POST(_req: NextRequest, ctx: Ctx) {
  const { userId, roleId } = await ctx.params;
  return forwardApiMutation("POST", `/api/users/${userId}/roles/${roleId}`);
}

export async function DELETE(_req: NextRequest, ctx: Ctx) {
  const { userId, roleId } = await ctx.params;
  return forwardApiMutation("DELETE", `/api/users/${userId}/roles/${roleId}`);
}
