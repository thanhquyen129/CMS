import { NextRequest } from "next/server";
import { forwardApiGet, forwardApiMutation } from "@/lib/bff-api";

export async function GET(req: NextRequest) {
  return forwardApiGet(`/api/bills${req.nextUrl.search}`);
}

export async function POST(req: NextRequest) {
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("POST", "/api/bills", body);
}
