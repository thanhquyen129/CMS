/** Client Idempotency-Key for consequential money commands (UX-12 / W-M5). */
export function newIdempotencyKey(prefix = "web"): string {
  const rand =
    typeof crypto !== "undefined" && "randomUUID" in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  return `${prefix}-${rand}`;
}

export function withIdempotency(
  headers: HeadersInit | undefined,
  key: string
): HeadersInit {
  return { ...(headers ?? {}), "Idempotency-Key": key, Accept: "application/json" };
}
