import { NextRequest, NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";
import { getSessionToken } from "@/lib/api";

export async function GET(req: NextRequest) {
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  }

  const qs = req.nextUrl.searchParams.toString();
  const path = qs ? `/api/aging/export?${qs}` : "/api/aging/export";

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "text/csv",
      },
      cache: "no-store",
    });

    if (res.status === 401) {
      return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
    }

    if (!res.ok) {
      const body = await res.text();
      return new NextResponse(body || "Export failed", { status: res.status });
    }

    const blob = await res.arrayBuffer();
    const disposition =
      res.headers.get("content-disposition") ||
      'attachment; filename="aging.csv"';
    return new NextResponse(blob, {
      status: 200,
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": disposition,
      },
    });
  } catch {
    return NextResponse.json(
      { message: "Không kết nối được máy chủ API." },
      { status: 502 }
    );
  }
}
