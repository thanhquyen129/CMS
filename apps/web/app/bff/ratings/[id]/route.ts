import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function GET(_req: Request, { params }: { params: Params }) {
  const { id } = await params;
  return forwardApiGet(`/api/ratings/${id}`);
}
