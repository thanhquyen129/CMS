import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

export async function GET(req: NextRequest) {
  const qs = req.nextUrl.searchParams.toString();
  return forwardApiGet(`/api/service-selections/audits${qs ? `?${qs}` : ""}`);
}
