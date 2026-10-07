import { forwardApiMutation } from "@/lib/bff-api";

export async function POST() {
  return forwardApiMutation("POST", "/api/surcharges/migrate-legacy", {});
}
