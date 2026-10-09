import { NextRequest } from "next/server";
import { forwardApiGet, forwardApiMutation } from "@/lib/bff-api";

export async function GET() {
  return forwardApiGet("/api/economic-charge-types");
}

export async function POST(req: NextRequest) {
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("POST", "/api/economic-charge-types", body);
}
