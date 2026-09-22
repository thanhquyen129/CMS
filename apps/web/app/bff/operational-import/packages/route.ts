import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

/** POST /bff/operational-import/packages → /api/operational-import/packages */
export async function POST(req: NextRequest) {
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("POST", "/api/operational-import/packages", body);
}
