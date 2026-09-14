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
    <div>
      <h2 className="section-title">Thành viên &amp; vai trò</h2>
      <p className="muted">
        Gán một hoặc nhiều vai trò cho từng thành viên. Quyền thực tế = hợp
        các quyền đã bật trên các vai trò đó.
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
        <div className="table-wrap" style={{ marginTop: "0.75rem" }}>
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Thành viên</th>
                <th scope="col">Vai trò</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => {
                const assigned = userRolesByUserId[u.id] ?? [];
                return (
                  <tr key={u.id}>
                    <td>
                      <div>{u.displayName}</div>
                      <div className="muted">{u.email}</div>
                      <div className="muted">
                        {u.isActive ? "Đang dùng" : "Ngừng"}
                      </div>
                    </td>
                    <td>
                      {assigned.length === 0 ? (
                        <span className="muted">Chưa gán</span>
                      ) : (
                        <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>
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
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <h3 className="section-title">Gán vai trò</h3>
      <form className="receive-form" onSubmit={assign}>
        <div className="form-grid">
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
    </div>
  );
}
