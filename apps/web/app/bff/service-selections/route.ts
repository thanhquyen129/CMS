import { NextRequest } from "next/server";
import { forwardApiGet, forwardApiMutation } from "@/lib/bff-api";

export async function GET(req: NextRequest) {
  const qs = req.nextUrl.searchParams.toString();
  return forwardApiGet(`/api/service-selections${qs ? `?${qs}` : ""}`);
}

export async function PUT(req: NextRequest) {
  const text = await req.text();
  const body = text.trim() ? JSON.parse(text) : {};
  return forwardApiMutation("PUT", "/api/service-selections", body);
}
