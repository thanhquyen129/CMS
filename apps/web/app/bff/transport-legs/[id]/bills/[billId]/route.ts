import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string; billId: string }>;

export async function POST(
  _req: Request,
  { params }: { params: Params }
) {
  const { id, billId } = await params;
  return forwardApiMutation(
    "POST",
    `/api/transport-legs/${encodeURIComponent(id)}/bills/${encodeURIComponent(billId)}`
  );
}
