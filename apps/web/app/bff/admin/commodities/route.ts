import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

export async function PUT(req: NextRequest) {
  const body = await req.json().catch(() => ({}));
  return forwardApiMutation("PUT", "/api/commodities", body);
}
