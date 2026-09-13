import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import {
  AUTH_COOKIE,
  REFRESH_COOKIE,
  cookieSecure,
  getApiInternalUrl,
} from "@/lib/auth";

export async function POST() {
  const jar = await cookies();
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;

  if (refreshToken) {
    await fetch(`${getApiInternalUrl()}/api/auth/logout`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    }).catch(() => undefined);
  }

  const res = NextResponse.json({ ok: true });
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
