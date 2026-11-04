import { forwardApiGet } from "@/lib/bff-api";

export async function GET(req: Request) {
  const { searchParams } = new URL(req.url);
  const activeOnly = searchParams.get("activeOnly");
  const qs = activeOnly ? `?activeOnly=${encodeURIComponent(activeOnly)}` : "";
  return forwardApiGet(`/api/surcharges/activation-services${qs}`);
}

