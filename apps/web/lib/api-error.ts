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
