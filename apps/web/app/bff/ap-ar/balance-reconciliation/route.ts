import { forwardApiGet } from "@/lib/bff-api";

/** GET /bff/ap-ar/balance-reconciliation → /api/ap-ar/balance-reconciliation */
export async function GET() {
  return forwardApiGet("/api/ap-ar/balance-reconciliation");
}
