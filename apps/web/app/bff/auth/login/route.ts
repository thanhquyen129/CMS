import { NextRequest, NextResponse } from "next/server";
import {
  AUTH_COOKIE,
  REFRESH_COOKIE,
  DISPLAY_NAME_COOKIE,
  cookieSecure,
  getApiInternalUrl,
} from "@/lib/auth";

export async function POST(req: NextRequest) {
  let body: { email?: string; password?: string };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json(
      { code: "validation_error", message: "Email và mật khẩu là bắt buộc." },
      { status: 400 }
    );
  }

  if (!body.email?.trim() || !body.password) {
    return NextResponse.json(
      { code: "validation_error", message: "Email và mật khẩu là bắt buộc." },
      { status: 400 }
    );
  }

  const apiRes = await fetch(`${getApiInternalUrl()}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      email: body.email.trim(),
      password: body.password,
    }),
    cache: "no-store",
  });

  const payload = await apiRes.json().catch(() => ({}));
  if (!apiRes.ok) {
    return NextResponse.json(
      {
        code: payload.code ?? "login_failed",
        message: payload.message ?? "Đăng nhập thất bại.",
      },
      { status: apiRes.status }
    );
  }

  const accessToken = payload.accessToken as string | undefined;
  const refreshToken = payload.refreshToken as string | undefined;
  const expiresIn = Number(payload.expiresIn ?? 3600);
  const refreshExpiresIn = Number(payload.refreshExpiresIn ?? 14 * 24 * 3600);
  if (!accessToken) {
    return NextResponse.json(
      { code: "login_failed", message: "Máy chủ không trả về token." },
      { status: 502 }
    );
  }

  const res = NextResponse.json({ ok: true, user: payload.user });
  res.cookies.set({
    name: AUTH_COOKIE,
    value: accessToken,
    httpOnly: true,
    secure: cookieSecure(),
    sameSite: "lax",
    path: "/",
    maxAge: Math.max(60, expiresIn),
  });
  if (refreshToken) {
    res.cookies.set({
      name: REFRESH_COOKIE,
      value: refreshToken,
      httpOnly: true,
      secure: cookieSecure(),
      sameSite: "lax",
      path: "/",
      maxAge: Math.max(60, refreshExpiresIn),
    });
  }
  const displayName =
    payload.user &&
    typeof payload.user === "object" &&
    "displayName" in payload.user &&
    typeof (payload.user as { displayName?: unknown }).displayName === "string"
      ? String((payload.user as { displayName: string }).displayName).trim()
      : "";
  if (displayName) {
    res.cookies.set({
      name: DISPLAY_NAME_COOKIE,
      value: displayName.slice(0, 80),
      httpOnly: false,
      secure: cookieSecure(),
      sameSite: "lax",
      path: "/",
      maxAge: Math.max(60, refreshExpiresIn || expiresIn),
    });
  }
  return res;
}
