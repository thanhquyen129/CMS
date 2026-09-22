import { NextRequest } from "next/server";
import { forwardApiGet, forwardApiMutation } from "@/lib/bff-api";

export async function GET() {
  return forwardApiGet("/api/policies?latestOnly=true");
}

export async function PUT(req: NextRequest) {
  const body = await req.json().catch(() => ({}));
  return forwardApiMutation("PUT", "/api/policies", body);
}
