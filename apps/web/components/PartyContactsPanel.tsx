"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { PartyContact } from "@/lib/party";

export function PartyContactsPanel({
  partyId,
  contacts,
}: {
  partyId: string;
  contacts: PartyContact[];
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const body = {
      fullName: String(fd.get("fullName") ?? "").trim(),
      title: String(fd.get("title") ?? "").trim() || null,
      phone: String(fd.get("phone") ?? "").trim() || null,
      email: String(fd.get("email") ?? "").trim() || null,
      isPrimary: fd.get("isPrimary") === "on",
      isActive: true,
      note: null as string | null,
    };
    if (!body.fullName) {
      setError("Nhập tên người liên hệ.");
      setSubmitting(false);
      return;
    }
    try {
      const res = await fetch(`/bff/admin/parties/${partyId}/contacts`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Thêm người liên hệ thất bại.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  async function onDelete(id: string) {
    if (!window.confirm("Xóa mềm người liên hệ này?")) return;
    setError(null);
    try {
      const res = await fetch(
        `/bff/admin/parties/${partyId}/contacts/${id}`,
        { method: "DELETE", headers: { Accept: "application/json" } }
      );
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Xóa người liên hệ thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    }
  }

  const busy = submitting || isPending;

  return (
    <fieldset className="group-box">
      <legend>Người liên hệ</legend>
      {contacts.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có người liên hệ.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Họ tên</th>
                <th>Chức vụ</th>
                <th>Liên hệ</th>
                <th>Chính</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {contacts.map((c) => (
                <tr key={c.id}>
                  <td>{c.fullName}</td>
                  <td>{c.title || "—"}</td>
                  <td>
                    {[c.phone, c.email].filter(Boolean).join(" · ") || "—"}
                  </td>
                  <td>{c.isPrimary ? "Có" : "—"}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-ghost"
                      disabled={busy}
                      onClick={() => onDelete(c.id)}
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <form className="receive-form" onSubmit={onSubmit} style={{ marginTop: "0.75rem" }}>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="contactName">Họ tên</label>
            <input id="contactName" name="fullName" required disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="contactTitle">Chức vụ</label>
            <input id="contactTitle" name="title" disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="contactPhone">Điện thoại</label>
            <input id="contactPhone" name="phone" disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="contactEmail">Email</label>
            <input id="contactEmail" name="email" type="email" disabled={busy} />
          </div>
          <label className="field checkbox-field">
            <input type="checkbox" name="isPrimary" defaultChecked disabled={busy} />{" "}
            Liên hệ chính
          </label>
        </div>
        {error ? (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        ) : null}
        <div className="cta-row">
          <button type="submit" className="btn" disabled={busy}>
            {busy ? "Đang lưu…" : "Thêm người liên hệ"}
          </button>
        </div>
      </form>
    </fieldset>
  );
}
