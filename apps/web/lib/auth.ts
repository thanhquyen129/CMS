export const AUTH_COOKIE = "lcms_at";

export function getApiInternalUrl(): string {
  return (
    process.env.API_INTERNAL_URL?.replace(/\/$/, "") ||
    process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ||
    "http://127.0.0.1:8080"
  );
}

export function cookieSecure(): boolean {
  // Production VPS is HTTP today (no TLS yet). Only set Secure when env says so.
  return process.env.AUTH_COOKIE_SECURE === "true";
}
