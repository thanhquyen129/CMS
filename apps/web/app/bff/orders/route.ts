import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

export async function PUT(req: NextRequest) {
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("PUT", "/api/orders", body);
}
