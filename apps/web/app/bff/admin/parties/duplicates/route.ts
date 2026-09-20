import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

export async function GET(req: NextRequest) {
  const q = req.nextUrl.searchParams.toString();
  return forwardApiGet(`/api/business-parties/duplicates${q ? `?${q}` : ""}`);
}
