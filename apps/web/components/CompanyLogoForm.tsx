"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

export function CompanyLogoForm({ hasLogo }: { hasLogo: boolean }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [stamp, setStamp] = useState(() => Date.now());

  async function onFile(file: File | undefined) {
    if (!file) return;
    setError(null);
    setInfo(null);
    if (!["image/png", "image/jpeg", "image/webp"].includes(file.type)) {
      setError("Logo chỉ nhận PNG, JPEG hoặc WebP.");
      return;
    }
    if (file.size > 512 * 1024) {
      setError("Logo phải nhỏ hơn 512 KB.");
      return;
    }
    setBusy(true);
    try {
      const buf = await file.arrayBuffer();
      const bytes = new Uint8Array(buf);
      let binary = "";
      bytes.forEach((b) => {
        binary += String.fromCharCode(b);
      });
      const base64 = btoa(binary);
      const res = await fetch("/bff/tenant-profile/logo", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ contentType: file.type, base64 }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được logo.");
        return;
      }
      setInfo("Đã cập nhật logo.");
      setStamp(Date.now());
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function clearLogo() {
    setError(null);
    setInfo(null);
    setBusy(true);
    try {
      const res = await fetch("/bff/tenant-profile/logo", {
        method: "DELETE",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không xóa được logo.");
        return;
      }
      setInfo("Đã gỡ logo.");
      setStamp(Date.now());
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? (
        <div className="alert alert-success" role="status">
          {info}
        </div>
      ) : null}
      {hasLogo ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={`/bff/tenant-profile/logo?t=${stamp}`}
          alt="Logo doanh nghiệp"
          style={{ maxHeight: 96, maxWidth: 240, objectFit: "contain", marginBottom: "0.75rem" }}
        />
      ) : (
        <p className="muted">Chưa có logo.</p>
      )}
      <div className="field">
        <label htmlFor="logoFile">Tải logo (PNG / JPEG / WebP, tối đa 512 KB)</label>
        <input
          id="logoFile"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          disabled={waiting}
          onChange={(e) => void onFile(e.target.files?.[0])}
        />
      </div>
      {hasLogo ? (
        <div className="cta-row">
          <button type="button" className="btn btn-ghost" disabled={waiting} onClick={() => void clearLogo()}>
            Gỡ logo
          </button>
        </div>
      ) : null}
    </div>
  );
}
