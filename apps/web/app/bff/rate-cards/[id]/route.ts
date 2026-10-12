import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function PUT(req: Request, { params }: { params: Params }) {
  const { id } = await params;
  const body = await req.json().catch(() => ({}));
  return forwardApiMutation("PUT", `/api/rate-cards/${id}`, body);
}

export async function DELETE(_req: Request, { params }: { params: Params }) {
  const { id } = await params;
  return forwardApiMutation("DELETE", `/api/rate-cards/${id}`);
}
