import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function DELETE(_req: Request, { params }: { params: Params }) {
  const { id } = await params;
  return forwardApiMutation("DELETE", `/api/rate-cards/${id}`);
}
