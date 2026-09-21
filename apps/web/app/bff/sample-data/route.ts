import { forwardApiGet, forwardApiMutation } from "@/lib/bff-api";

export async function GET() {
  return forwardApiGet("/api/sample-data");
}

export async function POST() {
  return forwardApiMutation("POST", "/api/sample-data/ensure");
}
