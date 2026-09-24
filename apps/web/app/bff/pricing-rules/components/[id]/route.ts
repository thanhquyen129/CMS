import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function PUT(req: NextRequest, { params }: { params: Params }) {
  const { id } = await params;
  const body = await req.json();
  return forwardApiMutation(
    "PUT",
    `/api/pricing-rules/components/${encodeURIComponent(id)}`,
    body
  );
}

export async function DELETE(_req: NextRequest, { params }: { params: Params }) {
  const { id } = await params;
  return forwardApiMutation(
    "DELETE",
    `/api/pricing-rules/components/${encodeURIComponent(id)}`
  );
}
