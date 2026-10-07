import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

export async function POST(req: NextRequest) {
  return forwardApiMutation("POST", "/api/surcharges", await readBody(req));
}

async function readBody(req: NextRequest): Promise<unknown> {
  try {
    const text = await req.text();
    return text.trim() ? JSON.parse(text) : {};
  } catch {
    return {};
  }
}
