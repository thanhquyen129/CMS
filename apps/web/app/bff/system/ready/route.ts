import { NextResponse } from "next/server";
import { getApiInternalUrl } from "@/lib/auth";

export async function GET() {
  try {
    const res = await fetch(`${getApiInternalUrl()}/ready`, {
      cache: "no-store",
      signal: AbortSignal.timeout(8000),
    });
    const body = await res.text();
    return new NextResponse(body, {
      status: res.status,
      headers: { "Content-Type": res.headers.get("Content-Type") ?? "application/json" },
    });
  } catch (e) {
    return NextResponse.json(
      { status: "error", message: e instanceof Error ? e.message : "unreachable" },
      { status: 503 },
    );
  }
}
