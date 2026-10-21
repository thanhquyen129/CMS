import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

export async function GET(req: NextRequest) {
  return forwardApiGet(`/api/costs/shared/allocation-context${req.nextUrl.search}`);
}

