import { NextRequest, NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";
import { getSessionToken } from "@/lib/api";

/** GET /bff/audit-events?objectType=&objectId= → proxy /api/audit-events. */
export async function GET(req: NextRequest) {
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json(
      { message: "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại." },
      { status: 401 }
    );
  }

  const src = req.nextUrl.searchParams;
  const qs = new URLSearchParams();
  for (const key of [
    "objectType",
    "objectId",
    "action",
    "correlationId",
    "from",
    "to",
    "skip",
    "take",
  ]) {
    const v = src.get(key);
    if (v) qs.set(key, v);
  }

  const path =
    qs.size > 0 ? `/api/audit-events?${qs.toString()}` : "/api/audit-events";

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });
    const text = await res.text();
    return new NextResponse(text, {
      status: res.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch {
    return NextResponse.json(
      { message: "Không kết nối được máy chủ API. Thử lại sau." },
      { status: 502 }
    );
  }
}
