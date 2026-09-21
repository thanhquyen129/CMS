"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CATALOG_KINDS, LOCATION_CLASSES } from "@/lib/catalog-kinds";

export function UpsertCatalogItemForm({ defaultKind }: { defaultKind: string }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [kind, setKind] = useState(defaultKind);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const selectedKind = String(fd.get("kind") ?? "").trim();
    const attributes: Record<string, string> = {};
    if (selectedKind === "location") {
      const cls = String(fd.get("locationClass") ?? "").trim();
      if (cls) attributes.class = cls;
    }
    if (selectedKind === "transport_route") {
      const origin = String(fd.get("origin") ?? "").trim();
      const destination = String(fd.get("destination") ?? "").trim();
      if (origin) attributes.origin = origin.toUpperCase();
      if (destination) attributes.destination = destination.toUpperCase();
    }
    const body = {
      kind: selectedKind,
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      description: String(fd.get("description") ?? "").trim() || null,
      attributesJson: Object.keys(attributes).length ? JSON.stringify(attributes) : null,
      isActive: true,
    };
    if (!body.kind || !body.code || !body.name) {
      setError("Chọn loại, nhập mã và tên.");
      setSubmitting(false);
      return;
    }
    try {
      const res = await fetch("/bff/admin/catalog", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được danh mục.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      setKind(defaultKind);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor="cat-kind">Nhóm</label>
          <select
            id="cat-kind"
            name="kind"
            required
            disabled={busy}
            value={kind}
            onChange={(e) => setKind(e.target.value)}
          >
            {CATALOG_KINDS.map((k) => (
              <option key={k.id} value={k.id}>
                {k.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="cat-code">Mã</label>
          <input id="cat-code" name="code" required maxLength={64} disabled={busy} />
        </div>
        <div className="field">
          <label htmlFor="cat-name">Tên</label>
          <input id="cat-name" name="name" required maxLength={256} disabled={busy} />
        </div>
        {kind === "location" ? (
          <div className="field">
            <label htmlFor="cat-loc">Loại địa điểm</label>
            <select id="cat-loc" name="locationClass" required disabled={busy} defaultValue="port">
              {LOCATION_CLASSES.map((k) => (
                <option key={k.id} value={k.id}>
                  {k.label}
                </option>
              ))}
            </select>
          </div>
        ) : null}
        {kind === "transport_route" ? (
          <>
            <div className="field">
              <label htmlFor="cat-origin">Điểm đi (mã)</label>
              <input id="cat-origin" name="origin" maxLength={16} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="cat-dest">Điểm đến (mã)</label>
              <input id="cat-dest" name="destination" maxLength={16} disabled={busy} />
            </div>
          </>
        ) : null}
        <div className="field">
          <label htmlFor="cat-desc">Diễn giải</label>
          <input id="cat-desc" name="description" maxLength={512} disabled={busy} />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang lưu…" : "Thêm / cập nhật"}
        </button>
      </div>
    </form>
  );
}
