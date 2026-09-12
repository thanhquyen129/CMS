import { NextRequest, NextResponse } from "next/server";
import { AUTH_COOKIE } from "@/lib/auth";

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;
  if (pathname.startsWith("/login") || pathname.startsWith("/bff/")) {
    return NextResponse.next();
  }

  const token = req.cookies.get(AUTH_COOKIE)?.value;
  const needsAuth =
    pathname === "/" ||
    pathname === "/dashboard" ||
    pathname.startsWith("/dashboard/") ||
    pathname === "/bills" ||
    pathname.startsWith("/bills/") ||
    pathname === "/documents" ||
    pathname.startsWith("/documents/") ||
    pathname === "/ap-ar" ||
    pathname.startsWith("/ap-ar/") ||
    pathname === "/settlements" ||
    pathname.startsWith("/settlements/") ||
    pathname === "/financial-closes" ||
    pathname.startsWith("/financial-closes/") ||
    pathname.startsWith("/queues/");

  if (!token && needsAuth) {
    const url = req.nextUrl.clone();
    url.pathname = "/login";
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/",
    "/login",
    "/dashboard",
    "/dashboard/:path*",
    "/bills",
    "/bills/:path*",
    "/documents",
    "/documents/:path*",
    "/ap-ar",
    "/ap-ar/:path*",
    "/settlements",
    "/settlements/:path*",
    "/financial-closes",
    "/financial-closes/:path*",
    "/queues/:path*",
  ],
};
