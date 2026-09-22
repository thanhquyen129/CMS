import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function POST(req: NextRequest, { params }: { params: Params }) {
  const { id } = await params;
  const body = await req.json().catch(() => ({}));
  return forwardApiMutation("POST", `/api/revenues/${id}/mappings`, body);
}
