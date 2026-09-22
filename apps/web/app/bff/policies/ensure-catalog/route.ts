import { forwardApiMutation } from "@/lib/bff-api";

export async function POST() {
  return forwardApiMutation("POST", "/api/policies/ensure-catalog", {});
}
