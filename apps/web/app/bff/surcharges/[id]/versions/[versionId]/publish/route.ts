import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string; versionId: string }>;

export async function POST(_req: Request, { params }: { params: Params }) {
  const { id, versionId } = await params;
  return forwardApiMutation("POST", `/api/surcharges/${id}/versions/${versionId}/publish`);
}
