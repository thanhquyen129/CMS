import { NextRequest } from "next/server";
import { forwardApiMutation } from "@/lib/bff-api";

type Params = Promise<{ id: string; billId: string }>;

export async function POST(
  _req: Request,
  { params }: { params: Params }
) {
  const { id, billId } = await params;
  return forwardApiMutation(
    "POST",
    `/api/orders/${encodeURIComponent(id)}/bills/${encodeURIComponent(billId)}`
  );
}

export async function DELETE(req: NextRequest, { params }: { params: Params }) {
  const { id, billId } = await params;
  const reason = req.nextUrl.searchParams.get("reason");
  const qs = reason ? `?reason=${encodeURIComponent(reason)}` : "";
  return forwardApiMutation(
    "DELETE",
    `/api/orders/${encodeURIComponent(id)}/bills/${encodeURIComponent(billId)}${qs}`
  );
}
