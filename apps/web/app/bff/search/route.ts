import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

export async function GET(req: NextRequest) {
  const q = req.nextUrl.searchParams.get("q") ?? "";
  return forwardApiGet(`/api/search?q=${encodeURIComponent(q)}`);
}
