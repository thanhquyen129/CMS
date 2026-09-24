import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function POST(_req: Request, { params }: { params: Params }) {
  const { id } = await params;
  return forwardApiMutation("POST", `/api/revenue-mappings/${id}/cancel`);
}
