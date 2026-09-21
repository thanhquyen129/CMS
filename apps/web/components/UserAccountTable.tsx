"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import type { UserRoleItem } from "@/lib/access";
import type { OrganizationItem } from "@/lib/master-data";
import type { AccessUser } from "@/lib/tenant-admin-model";

type Props = {
  users: AccessUser[];
  organizations: OrganizationItem[];
  userRolesByUserId: Record<string, UserRoleItem[]>;
};

export function UserAccountTable({ users, organizations, userRolesByUserId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const orgById = useMemo(
    () => Object.fromEntries(organizations.map((o) => [o.id, o])),
    [organizations]
  );

  async function saveProfile(e: FormEvent<HTMLFormElement>, user: AccessUser) {
    e.preventDefault();
    setError(null);
    setBusyId(user.id);
    const fd = new FormData(e.currentTarget);
    const displayName = String(fd.get("displayName") ?? "").trim();
    const orgRaw = String(fd.get("organizationId") ?? "");
    const isActive = String(fd.get("isActive") ?? "") === "true";
    try {
      const res = await fetch(`/bff/admin/access/users/${user.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          displayName,
          isActive,
          organizationId: orgRaw || null,
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không cập nhật được người dùng.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusyId(null);
    }
  }

  async function setPassword(e: FormEvent<HTMLFormElement>, user: AccessUser) {
    e.preventDefault();
    setError(null);
    setBusyId(user.id);
    const form = e.currentTarget;
    const fd = new FormData(form);
    const password = String(fd.get("password") ?? "");
    const confirm = String(fd.get("confirmPassword") ?? "");
    if (password.length < 10 || !/[A-Za-z]/.test(password) || !/\d/.test(password)) {
      setError("Mật khẩu phải có ít nhất 10 ký tự, gồm chữ và số.");
      setBusyId(null);
      return;
    }
    if (password !== confirm) {
      setError("Xác nhận mật khẩu không khớp.");
      setBusyId(null);
      return;
    }
    try {
      const res = await fetch(`/bff/admin/access/users/${user.id}/password`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ password }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không đặt được mật khẩu.");
        return;
      }
      form.reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusyId(null);
    }
  }

  if (users.length === 0) {
    return (
      <div className="empty-state" role="status">
        Chưa có người dùng. Tạo tài khoản bên phải.
      </div>
    );
  }

  return (
    <div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Email</th>
              <th scope="col">Tên / đơn vị</th>
              <th scope="col">Vai trò</th>
              <th scope="col">Mật khẩu</th>
              <th scope="col">Trạng thái</th>
              <th scope="col">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => {
              const waiting = busyId === u.id || isPending;
              const roles = userRolesByUserId[u.id] ?? [];
              const org = u.organizationId ? orgById[u.organizationId] : null;
              return (
                <tr key={u.id}>
                  <td className="mono-id">{u.email}</td>
                  <td>
                    <form onSubmit={(e) => void saveProfile(e, u)}>
                      <div className="field">
                        <label className="sr-only" htmlFor={`dn-${u.id}`}>
                          Tên hiển thị
                        </label>
                        <input
                          id={`dn-${u.id}`}
                          name="displayName"
                          defaultValue={u.displayName}
                          required
                          maxLength={256}
                          disabled={waiting}
                        />
                      </div>
                      <div className="field">
                        <label className="sr-only" htmlFor={`org-${u.id}`}>
                          Đơn vị
                        </label>
                        <select
                          id={`org-${u.id}`}
                          name="organizationId"
                          defaultValue={u.organizationId ?? ""}
                          disabled={waiting}
                        >
                          <option value="">Không gắn đơn vị</option>
                          {organizations.map((o) => (
                            <option key={o.id} value={o.id}>
                              {o.code} — {o.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <input type="hidden" name="isActive" value={u.isActive ? "true" : "false"} />
                      <button type="submit" className="btn btn-ghost" disabled={waiting}>
                        Lưu hồ sơ
                      </button>
                    </form>
                    {org ? (
                      <p className="muted small">
                        Hiện tại: {org.code} — {org.name}
                      </p>
                    ) : null}
                  </td>
                  <td>
                    {roles.length === 0 ? (
                      <span className="muted">Chưa gán</span>
                    ) : (
                      roles.map((r) => r.name).join(", ")
                    )}
                  </td>
                  <td>
                    <p>{u.passwordSet ? "Đã đặt" : "Chưa đặt — không đăng nhập được"}</p>
                    <form onSubmit={(e) => void setPassword(e, u)}>
                      <div className="field">
                        <label className="sr-only" htmlFor={`pw-${u.id}`}>
                          Mật khẩu mới
                        </label>
                        <input
                          id={`pw-${u.id}`}
                          name="password"
                          type="password"
                          placeholder="Mật khẩu mới"
                          minLength={10}
                          maxLength={128}
                          autoComplete="new-password"
                          disabled={waiting}
                          required
                        />
                      </div>
                      <div className="field">
                        <label className="sr-only" htmlFor={`pwc-${u.id}`}>
                          Xác nhận
                        </label>
                        <input
                          id={`pwc-${u.id}`}
                          name="confirmPassword"
                          type="password"
                          placeholder="Xác nhận"
                          minLength={10}
                          maxLength={128}
                          autoComplete="new-password"
                          disabled={waiting}
                          required
                        />
                      </div>
                      <button type="submit" className="btn btn-ghost" disabled={waiting}>
                        Đặt mật khẩu
                      </button>
                    </form>
                  </td>
                  <td>{u.isActive ? "Đang dùng" : "Ngừng"}</td>
                  <td>
                    <form onSubmit={(e) => void saveProfile(e, u)}>
                      <input type="hidden" name="displayName" value={u.displayName} />
                      <input type="hidden" name="organizationId" value={u.organizationId ?? ""} />
                      <input
                        type="hidden"
                        name="isActive"
                        value={u.isActive ? "false" : "true"}
                      />
                      <button
                        type="submit"
                        className={u.isActive ? "btn btn-ghost" : "btn"}
                        disabled={waiting}
                      >
                        {u.isActive ? "Ngừng tài khoản" : "Mở lại"}
                      </button>
                    </form>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <p className="note">
        Không xóa người dùng. Ngừng tài khoản để nhả chỗ license. Không ngừng được Quản trị cuối
        cùng. Đặt mật khẩu sẽ thu hồi mọi phiên đăng nhập hiện tại của tài khoản đó.
      </p>
    </div>
  );
}
