import { NextRequest, NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";
import { getSessionToken } from "@/lib/api";

type Params = Promise<{ id: string }>;

export async function GET(_req: NextRequest, { params }: { params: Params }) {
  const { id } = await params;
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  }
  try {
    const res = await fetch(
      `${getApiInternalUrl()}/api/document-matches/${id}/exposure-proposals`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: "application/json",
        },
        cache: "no-store",
      }
    );
    const text = await res.text();
    return new NextResponse(text, {
      status: res.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch {
    return NextResponse.json(
      { message: "Không kết nối được máy chủ API." },
      { status: 502 }
    );
  }
}
