import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

/** POST /bff/bank-feed/lines/import → /api/bank-feed/lines/import-csv */
export async function POST(req: NextRequest) {
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("POST", "/api/bank-feed/lines/import-csv", body);
}
