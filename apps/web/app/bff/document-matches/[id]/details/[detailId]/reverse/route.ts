import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string; detailId: string }>;

export async function POST(req: NextRequest, { params }: { params: Params }) {
  const { id, detailId } = await params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "POST",
    `/api/document-matches/${id}/details/${detailId}/reverse`,
    body
  );
}
