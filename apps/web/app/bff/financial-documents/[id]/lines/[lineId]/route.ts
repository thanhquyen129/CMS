import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string; lineId: string }>;

export async function PUT(req: NextRequest, { params }: { params: Params }) {
  const { id, lineId } = await params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "PUT",
    `/api/financial-documents/${id}/lines/${lineId}`,
    body
  );
}

export async function DELETE(_req: NextRequest, { params }: { params: Params }) {
  const { id, lineId } = await params;
  return forwardApiMutation(
    "DELETE",
    `/api/financial-documents/${id}/lines/${lineId}`
  );
}
