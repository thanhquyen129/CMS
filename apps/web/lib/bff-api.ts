import { cookies, headers } from "next/headers";
import { NextResponse } from "next/server";
import { AUTH_COOKIE, getApiInternalUrl } from "./auth";

/** Forward authenticated BFF GET to LCMS API; preserve Vietnamese error body. */
export async function forwardApiGet(apiPath: string): Promise<NextResponse> {
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;
  if (!token) {
    return NextResponse.json(
      { code: "unauthorized", message: "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại." },
      { status: 401 }
    );
  }

  try {
    const res = await fetch(`${getApiInternalUrl()}${apiPath}`, {
      method: "GET",
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
      {
        code: "upstream_unreachable",
        message: "Không kết nối được máy chủ API. Thử lại sau.",
      },
      { status: 502 }
    );
  }
}

/** Forward authenticated BFF mutate to LCMS API; preserve Vietnamese error body. */
export async function forwardApiMutation(
  method: string,
  apiPath: string,
  body?: unknown
): Promise<NextResponse> {
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;
  if (!token) {
    return NextResponse.json(
      { code: "unauthorized", message: "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại." },
      { status: 401 }
    );
  }

  const incoming = await headers();
  const idempotencyKey = incoming.get("Idempotency-Key")?.trim();
  const ifMatch = incoming.get("If-Match")?.trim();

  try {
    const res = await fetch(`${getApiInternalUrl()}${apiPath}`, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...(idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {}),
        ...(ifMatch ? { "If-Match": ifMatch } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
      cache: "no-store",
    });

    if (res.status === 204) {
      return new NextResponse(null, { status: 204 });
    }

    const payload = await res.json().catch(() => ({}));
    if (!res.ok) {
      return NextResponse.json(
        {
          code: (payload as { code?: string }).code ?? "api_error",
          message:
            (payload as { message?: string }).message ??
            (res.status === 403
              ? "Bạn không có quyền thực hiện thao tác này."
              : res.status === 409
                ? "Không thể thực hiện vì xung đột trạng thái. Tải lại trang và thử lại."
                : "Thao tác thất bại."),
        },
        { status: res.status }
      );
    }

    return NextResponse.json(payload, { status: res.status });
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
