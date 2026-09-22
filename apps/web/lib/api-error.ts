/** Problem-details / LCMS error body shape from API & BFF. */
export type ApiErrorBody = {
  message?: string;
  correlationId?: string;
  code?: string;
};

/**
 * UX-13: surface support id so ops can find the request in logs/audit.
 * Keeps Vietnamese message first; appends mã hỗ trợ when present.
 */
export function formatApiErrorMessage(
  body: ApiErrorBody | null | undefined,
  fallback: string
): string {
  const msg =
    typeof body?.message === "string" && body.message.trim().length > 0
      ? body.message.trim()
      : fallback;
  const cid =
    typeof body?.correlationId === "string" ? body.correlationId.trim() : "";
  if (!cid) return msg;
  return `${msg} · Mã hỗ trợ: ${cid}`;
}

export async function readApiErrorBody(
  res: Response
): Promise<ApiErrorBody> {
  return (await res.json().catch(() => ({}))) as ApiErrorBody;
}

function looksLikePeriodLock(body: ApiErrorBody | null | undefined): boolean {
  const code = body?.code?.toLowerCase() ?? "";
  if (code === "period_locked") return true;
  const msg = body?.message ?? "";
  return /khóa chốt|period[_\s-]?lock|mở lại chốt/i.test(msg);
}

/**
 * UX-07: distinguish period lock vs concurrency vs generic conflict.
 * Always prefers server Vietnamese message when present.
 */
export function formatHttpError(
  status: number,
  body: ApiErrorBody | null | undefined,
  fallbacks?: {
    forbidden?: string;
    conflict?: string;
    default?: string;
  }
): string {
  if (status === 403) {
    return formatApiErrorMessage(
      body,
      fallbacks?.forbidden ?? "Bạn không có quyền thực hiện thao tác này."
    );
  }

  if (status === 409) {
    if (looksLikePeriodLock(body)) {
      return formatApiErrorMessage(
        body,
        "Kỳ/phạm vi đã khóa chốt tài chính. Mở lại chốt tại Chốt tài chính nếu cần điều chỉnh."
      );
    }
    const code = body?.code?.toLowerCase() ?? "";
    if (code === "concurrency_conflict") {
      return formatApiErrorMessage(
        body,
        "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại."
      );
    }
    return formatApiErrorMessage(
      body,
      fallbacks?.conflict ??
        "Không thực hiện được vì xung đột trạng thái. Tải lại và thử lại."
    );
  }

  return formatApiErrorMessage(
    body,
    fallbacks?.default ?? "Thao tác thất bại."
  );
}

/** True when operator should open Financial Close to unlock. */
export function isPeriodLockedError(
  body: ApiErrorBody | null | undefined
): boolean {
  return looksLikePeriodLock(body);
}
