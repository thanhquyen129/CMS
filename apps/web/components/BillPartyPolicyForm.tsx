"use client";

import type { FormEvent } from "react";
import { useEffect, useState } from "react";

const ROLES = [
  { code: "customer", label: "Khách hàng" },
  { code: "payer", label: "Bên trả tiền" },
  { code: "shipper", label: "Người gửi hàng" },
  { code: "consignee", label: "Người nhận hàng" },
  { code: "bill_to", label: "Bên nhận hóa đơn" },
] as const;

/** Tenant policy: which Bill party roles are required, and whether walk-in snapshots are allowed. */
export function BillPartyPolicyForm() {
  const [required, setRequired] = useState<string[]>([]);
  const [allowWalkIn, setAllowWalkIn] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const res = await fetch("/bff/admin/bill-party-policy", { headers: { Accept: "application/json" } });
      if (!res.ok) return;
      const body = (await res.json()) as { requiredRoles?: string[]; allowWalkIn?: boolean };
      if (cancelled) return;
      setRequired(body.requiredRoles ?? []);
      setAllowWalkIn(Boolean(body.allowWalkIn));
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaved(false);
    setBusy(true);
    try {
      const res = await fetch("/bff/admin/bill-party-policy", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ requiredRoles: required, allowWalkIn }),
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được chính sách đối tác trên Bill.");
        return;
      }
      setSaved(true);
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="stack-form">
      <fieldset className="group-box">
        <legend>Vai trò đối tác bắt buộc trên Bill</legend>
        <p className="note">Để trống nếu thuê bao chưa bắt buộc vai trò. Không cố định ba đối tác cho mọi Bill.</p>
        {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
        {saved ? <div className="alert alert-success" role="status">Đã lưu chính sách đối tác trên Bill.</div> : null}
        <div className="create-checks">
          {ROLES.map((role) => (
            <label key={role.code}>
              <input
                type="checkbox"
                checked={required.includes(role.code)}
                disabled={busy}
                onChange={(e) => {
                  setRequired((prev) =>
                    e.target.checked ? [...prev, role.code] : prev.filter((c) => c !== role.code));
                }}
              />{" "}
              {role.label}
            </label>
          ))}
        </div>
        <label className="checkbox-field">
          <input type="checkbox" checked={allowWalkIn} disabled={busy} onChange={(e) => setAllowWalkIn(e.target.checked)} />
          Cho phép đối tác vãng lai khi chưa có trong danh mục
        </label>
        <button className="btn" type="submit" disabled={busy}>{busy ? "Đang lưu…" : "Lưu chính sách đối tác"}</button>
      </fieldset>
    </form>
  );
}
