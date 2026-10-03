import { NextRequest, NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";
import { getSessionToken } from "@/lib/api";

export async function GET(req: NextRequest) {
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  }

  const { searchParams } = new URL(req.url);
  const targetUrl = new URL(`${getApiInternalUrl()}/api/financial-documents/eligible-source-lines`);
  searchParams.forEach((value, key) => {
    targetUrl.searchParams.set(key, value);
  });

  try {
    const res = await fetch(targetUrl.toString(), {
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
      { message: "Không kết nối được máy chủ API." },
      { status: 502 }
    );
  }
}

