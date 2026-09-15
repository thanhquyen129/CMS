"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { PARTY_ROLE_OPTIONS, partyRoleLabel } from "@/lib/party";

export function PartyRolesPanel({
  partyId,
  roleCodes,
}: {
  partyId: string;
  roleCodes: string[];
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busyCode, setBusyCode] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  async function toggle(code: string, enabled: boolean) {
    setError(null);
    setBusyCode(code);
    try {
      const res = await fetch(
        enabled
          ? `/bff/admin/parties/${partyId}/roles`
          : `/bff/admin/parties/${partyId}/roles/${encodeURIComponent(code)}`,
        {
          method: enabled ? "POST" : "DELETE",
          headers: {
            Accept: "application/json",
            ...(enabled ? { "Content-Type": "application/json" } : {}),
          },
          body: enabled ? JSON.stringify({ roleCode: code }) : undefined,
        }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Cập nhật vai trò thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusyCode(null);
    }
  }

  return (
    <fieldset className="group-box">
      <legend>Vai trò đối tác</legend>
      <p className="muted" style={{ marginTop: 0 }}>
        Một đối tác có thể vừa là khách hàng vừa là nhà cung cấp (canonical +
        roles).
      </p>
      <div className="form-grid">
        {PARTY_ROLE_OPTIONS.map((r) => {
          const on = roleCodes.includes(r.code);
          const busy = busyCode === r.code || isPending;
          return (
            <label key={r.code} className="field checkbox-field">
              <input
                type="checkbox"
                checked={on}
                disabled={busy}
                onChange={(e) => toggle(r.code, e.target.checked)}
              />{" "}
              {partyRoleLabel(r.code)}
            </label>
          );
        })}
      </div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
    </fieldset>
  );
}
