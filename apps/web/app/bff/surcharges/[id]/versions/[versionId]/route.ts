import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string; versionId: string }>;

export async function PUT(req: NextRequest, { params }: { params: Params }) {
  const { id, versionId } = await params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation("PUT", `/api/surcharges/${id}/versions/${versionId}`, body);
}
