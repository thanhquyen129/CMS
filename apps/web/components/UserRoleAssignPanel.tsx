"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { AccessRole, AccessUser, UserRoleItem } from "@/lib/access";

type Props = {
  users: AccessUser[];
  roles: AccessRole[];
  userRolesByUserId: Record<string, UserRoleItem[]>;
};

export function UserRoleAssignPanel({
  users,
  roles,
  userRolesByUserId,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function assign(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const userId = String(fd.get("userId") ?? "");
    const roleId = String(fd.get("roleId") ?? "");
    if (!userId || !roleId) {
      setError("Chọn người dùng và vai trò.");
      setBusy(false);
      return;
    }

    try {
      const res = await fetch(
        `/bff/admin/access/users/${userId}/roles/${roleId}`,
        { method: "POST", headers: { Accept: "application/json" } }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Gán vai trò thất bại.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function unassign(userId: string, roleId: string) {
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(
        `/bff/admin/access/users/${userId}/roles/${roleId}`,
        { method: "DELETE", headers: { Accept: "application/json" } }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Gỡ vai trò thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const loading = busy || isPending;

  return (
    <div className="layout-cols-2" style={{ marginTop: "0.5rem" }}>
      <fieldset className="group-box">
        <legend>Thành viên</legend>
        <p className="muted" style={{ marginTop: 0 }}>
          Quyền thực tế = hợp các quyền đã bật trên vai trò được gán.
        </p>

        {error ? (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        ) : null}

        {users.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có người dùng trong thuê bao.
          </div>
        ) : (
          <div className="member-card-grid">
            {users.map((u) => {
              const assigned = userRolesByUserId[u.id] ?? [];
              return (
                <div key={u.id} className="member-card">
                  <p className="member-name">{u.displayName}</p>
                  <div className="muted small">{u.email}</div>
                  <div className="muted small">
                    {u.isActive ? "Đang dùng" : "Ngừng"}
                  </div>
                  {assigned.length === 0 ? (
                    <p className="muted" style={{ margin: "0.45rem 0 0" }}>
                      Chưa gán vai trò
                    </p>
                  ) : (
                    <ul>
                      {assigned.map((r) => (
                        <li key={r.roleId}>
                          {r.name}{" "}
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            disabled={loading}
                            onClick={() => void unassign(u.id, r.roleId)}
                          >
                            Gỡ
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </fieldset>

      <fieldset className="group-box">
        <legend>Gán vai trò</legend>
        <form className="receive-form" onSubmit={assign} style={{ marginTop: 0 }}>
          <div className="form-grid cols-1">
            <div className="field">
              <label htmlFor="assignUser">Người dùng</label>
              <select id="assignUser" name="userId" required disabled={loading}>
                <option value="">— Chọn —</option>
                {users.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.displayName} ({u.email})
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="assignRole">Vai trò</label>
              <select id="assignRole" name="roleId" required disabled={loading}>
                <option value="">— Chọn —</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name} ({r.code})
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="cta-row">
            <button type="submit" className="btn" disabled={loading}>
              {loading ? "Đang lưu…" : "Gán vai trò"}
            </button>
          </div>
        </form>
      </fieldset>
    </div>
  );
}
