import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function POST(_req: NextRequest, { params }: { params: Params }) {
  const { id } = await params;
  return forwardApiMutation("POST", `/api/document-matches/${id}/confirm`);
}
