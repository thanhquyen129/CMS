import { NextRequest, NextResponse } from "next/server";
import { AUTH_COOKIE, getApiInternalUrl } from "@/lib/auth";

export async function GET(req: NextRequest) {
  const token = req.cookies.get(AUTH_COOKIE)?.value;
  if (!token) {
    return NextResponse.json(
      { code: "unauthorized", message: "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại." },
      { status: 401 }
    );
  }

  const q = req.nextUrl.searchParams.toString();
  try {
    const res = await fetch(
      `${getApiInternalUrl()}/api/business-parties/export${q ? `?${q}` : ""}`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: "text/csv, application/json",
        },
        cache: "no-store",
      }
    );
    const buf = await res.arrayBuffer();
    if (!res.ok) {
      return new NextResponse(buf, {
        status: res.status,
        headers: { "Content-Type": "application/json" },
      });
    }

    return new NextResponse(buf, {
      status: 200,
      headers: {
        "Content-Type": res.headers.get("Content-Type") ?? "text/csv; charset=utf-8",
        "Content-Disposition":
          res.headers.get("Content-Disposition") ??
          'attachment; filename="doi-tac.csv"',
      },
    });
  } catch {
    return NextResponse.json(
      {
        code: "upstream_unreachable",
        message: "Không kết nối được máy chủ API. Thử lại sau.",
      },
      { status: 502 }
    );
  }
}
