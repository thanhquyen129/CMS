import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import {
  AUTH_COOKIE,
  REFRESH_COOKIE,
  cookieSecure,
  getApiInternalUrl,
} from "@/lib/auth";

/** Rotate access + refresh cookies from refresh token (P22). */
export async function POST() {
  const jar = await cookies();
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) {
    return NextResponse.json(
      { code: "invalid_refresh", message: "Không có refresh token." },
      { status: 401 }
    );
  }

  const apiRes = await fetch(`${getApiInternalUrl()}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
  });
  const payload = await apiRes.json().catch(() => ({}));
  if (!apiRes.ok) {
    const res = NextResponse.json(
      {
        code: payload.code ?? "invalid_refresh",
        message: payload.message ?? "Refresh thất bại.",
      },
      { status: apiRes.status }
    );
    for (const name of [AUTH_COOKIE, REFRESH_COOKIE]) {
      res.cookies.set({
        name,
        value: "",
        httpOnly: true,
        secure: cookieSecure(),
        sameSite: "lax",
        path: "/",
        maxAge: 0,
      });
    }
    return res;
  }

  const accessToken = payload.accessToken as string;
  const newRefresh = payload.refreshToken as string;
  const expiresIn = Number(payload.expiresIn ?? 3600);
  const refreshExpiresIn = Number(payload.refreshExpiresIn ?? 14 * 24 * 3600);

  const res = NextResponse.json({ ok: true });
  res.cookies.set({
    name: AUTH_COOKIE,
    value: accessToken,
    httpOnly: true,
    secure: cookieSecure(),
    sameSite: "lax",
    path: "/",
    maxAge: Math.max(60, expiresIn),
  });
  res.cookies.set({
    name: REFRESH_COOKIE,
    value: newRefresh,
    httpOnly: true,
    secure: cookieSecure(),
    sameSite: "lax",
    path: "/",
    maxAge: Math.max(60, refreshExpiresIn),
  });
  return res;
}
