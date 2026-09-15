import { NextRequest, NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";
import { getSessionToken } from "@/lib/api";
import { forwardApiMutation } from "@/lib/bff-api";

type Ctx = { params: Promise<{ id: string }> };

export async function GET(_req: NextRequest, ctx: Ctx) {
  const { id } = await ctx.params;
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json(
      { message: "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại." },
      { status: 401 }
    );
  }

  try {
    const res = await fetch(
      `${getApiInternalUrl()}/api/business-parties/${id}/bank-accounts`,
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
      { message: "Không kết nối được máy chủ API. Thử lại sau." },
      { status: 502 }
    );
  }
}

export async function POST(req: NextRequest, ctx: Ctx) {
  const { id } = await ctx.params;
  let body: unknown = {};
  try {
    const text = await req.text();
    if (text.trim()) body = JSON.parse(text);
  } catch {
    body = {};
  }
  return forwardApiMutation(
    "POST",
    `/api/business-parties/${id}/bank-accounts`,
    body
  );
}
