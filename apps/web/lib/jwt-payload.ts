/** Decode JWT payload without verifying (UI only; server still enforces authz). */
export function readJwtSub(token: string | undefined | null): string | null {
  if (!token) return null;
  const parts = token.split(".");
  if (parts.length < 2) return null;
  try {
    const json = Buffer.from(parts[1].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString(
      "utf8"
    );
    const payload = JSON.parse(json) as { sub?: string };
    return payload.sub && payload.sub.length > 0 ? payload.sub : null;
  } catch {
    return null;
  }
}
