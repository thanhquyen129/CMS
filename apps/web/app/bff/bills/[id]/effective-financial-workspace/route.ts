import { NextRequest } from "next/server";
import { forwardApiGet } from "@/lib/bff-api";

type Params = Promise<{ id: string }>;

export async function GET(req: NextRequest, { params }: { params: Params }) {
  const { id } = await params;
  const q = req.nextUrl.search || "";
  return forwardApiGet(`/api/bills/${encodeURIComponent(id)}/effective-financial-workspace${q}`);
}
