import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

/** POST /bff/ratings/readiness → /api/ratings/readiness (read-only pre-check) */
export async function POST(req: NextRequest) {
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("POST", "/api/ratings/readiness", body);
}
