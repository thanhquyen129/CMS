import { NextRequest, NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";
import { getSessionToken } from "@/lib/api";
import { forwardApiMutation } from "@/lib/bff-api";

export async function GET() {
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  }
  try {
    const res = await fetch(`${getApiInternalUrl()}/api/bill-party-policy`, {
      headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
      cache: "no-store",
    });
    const text = await res.text();
    return new NextResponse(text, {
      status: res.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch {
    return NextResponse.json({ message: "Không kết nối được máy chủ API." }, { status: 502 });
  }
}

export async function PUT(req: NextRequest) {
  const body = await req.json().catch(() => ({}));
  return forwardApiMutation("PUT", "/api/bill-party-policy", body);
}
