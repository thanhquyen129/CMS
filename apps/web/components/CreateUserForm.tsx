"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { AccessRole } from "@/lib/access";
import type { OrganizationItem } from "@/lib/master-data";

type Props = {
  organizations: OrganizationItem[];
  roles: AccessRole[];
};

export function CreateUserForm({ organizations, roles }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setBusy(true);
    const form = e.currentTarget;
    const fd = new FormData(form);
    const email = String(fd.get("email") ?? "").trim();
    const displayName = String(fd.get("displayName") ?? "").trim();
    const password = String(fd.get("password") ?? "");
    const confirm = String(fd.get("confirmPassword") ?? "");
    const orgRaw = String(fd.get("organizationId") ?? "");
    const roleId = String(fd.get("roleId") ?? "");
    const organizationId = orgRaw || null;

    if (!email || !displayName) {
      setError("Nhập email và tên hiển thị.");
      setBusy(false);
      return;
    }
    if (password.length < 10 || !/[A-Za-z]/.test(password) || !/\d/.test(password)) {
      setError("Mật khẩu phải có ít nhất 10 ký tự, gồm chữ và số.");
      setBusy(false);
      return;
    }
    if (password !== confirm) {
      setError("Xác nhận mật khẩu không khớp.");
      setBusy(false);
      return;
    }

    try {
      const res = await fetch("/bff/admin/access/users", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ email, displayName, organizationId, password }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không tạo được người dùng.");
        return;
      }
      const created = (await res.json()) as { id?: string };
      if (roleId && created.id) {
        const assign = await fetch(
          `/bff/admin/access/users/${created.id}/roles/${roleId}`,
          { method: "POST", headers: { Accept: "application/json" } }
        );
        if (!assign.ok && assign.status !== 401) {
          const payload = (await assign.json().catch(() => ({}))) as { message?: string };
          setError(
            payload.message ||
              "Đã tạo người dùng nhưng gán vai trò thất bại. Gán lại tại Vai trò & Phân quyền."
          );
          startTransition(() => router.refresh());
          return;
        }
      }
      form.reset();
      setInfo("Đã tạo người dùng. Tài khoản đăng nhập được ngay bằng mật khẩu vừa đặt.");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
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
      <div className="form-grid">
        <div className="field">
          <label htmlFor="userEmail">Email đăng nhập</label>
          <input
            id="userEmail"
            name="email"
            type="email"
            required
            maxLength={320}
            autoComplete="off"
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="userDisplayName">Tên hiển thị</label>
          <input
            id="userDisplayName"
            name="displayName"
            type="text"
            required
            maxLength={256}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="userPassword">Mật khẩu</label>
          <input
            id="userPassword"
            name="password"
            type="password"
            required
            minLength={10}
            maxLength={128}
            autoComplete="new-password"
            disabled={waiting}
          />
          <span className="muted small">Tối thiểu 10 ký tự, có chữ và số.</span>
        </div>
        <div className="field">
          <label htmlFor="userPasswordConfirm">Xác nhận mật khẩu</label>
          <input
            id="userPasswordConfirm"
            name="confirmPassword"
            type="password"
            required
            minLength={10}
            maxLength={128}
            autoComplete="new-password"
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="userOrg">Đơn vị</label>
          <select id="userOrg" name="organizationId" disabled={waiting} defaultValue="">
            <option value="">Không gắn đơn vị</option>
            {organizations.map((o) => (
              <option key={o.id} value={o.id}>
                {o.code} — {o.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="userRole">Vai trò ban đầu</label>
          <select id="userRole" name="roleId" disabled={waiting} defaultValue="">
            <option value="">Chưa gán — gán sau tại Phân quyền</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name} ({r.code})
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="cta-row">
        <button type="submit" className="btn" disabled={waiting}>
          {waiting ? "Đang tạo…" : "Tạo người dùng"}
        </button>
      </div>
    </form>
  );
}
