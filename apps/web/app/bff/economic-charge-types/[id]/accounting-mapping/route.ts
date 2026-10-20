import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

export async function GET(_req: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return forwardApiGet(`/api/economic-charge-types/${encodeURIComponent(id)}/accounting-mapping`);
}
