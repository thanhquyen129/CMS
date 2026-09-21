"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { TenantBackupItem } from "@/lib/tenant-admin-model";
import { backupStatusLabel } from "@/lib/tenant-admin-model";
import { formatDateTimeVi } from "@/lib/money";

type Props = {
  tenantCode: string;
  items: TenantBackupItem[];
};

function formatBytes(n: number) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / (1024 * 1024)).toFixed(1)} MB`;
}

export function BackupWorkspace({ tenantCode, items }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const expected = `RESTORE ${tenantCode}`;

  async function createBackup(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const note = String(fd.get("note") ?? "").trim() || null;
    try {
      const res = await fetch("/bff/tenant-backups", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ note }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không tạo được bản sao lưu.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      setInfo("Đã lưu bản sao danh mục / cấu hình. Không gồm sổ tiền, mật khẩu, phiên đăng nhập.");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function restore(e: FormEvent<HTMLFormElement>, id: string) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const confirmPhrase = String(fd.get("confirmPhrase") ?? "").trim();
    try {
      const res = await fetch(`/bff/tenant-backups/${id}/restore`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ confirmPhrase }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không khôi phục được.");
        return;
      }
      setInfo("Đã khôi phục danh mục / cấu hình. Sổ tiền, chứng từ, AP/AR không đổi.");
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

      <div className="alert alert-warning" role="note">
        Sao lưu logic chỉ gồm hồ sơ doanh nghiệp, danh mục, tiền tệ/tỷ giá, tổ chức, license
        module và cài đặt thông báo. Không khôi phục sổ tiền, Bill, chứng từ, AP/AR, thanh toán.
        Phục hồi disaster (PITR Postgres) do vận hành máy chủ — không có nút “khôi phục cả
        database” trên màn này.
      </div>

      <fieldset className="group-box">
        <legend>Tạo bản sao lưu</legend>
        <form className="receive-form" onSubmit={(e) => void createBackup(e)}>
          <div className="field">
            <label htmlFor="backupNote">Ghi chú</label>
            <input
              id="backupNote"
              name="note"
              maxLength={512}
              placeholder="Ví dụ: trước khi chỉnh danh mục tuyến"
              disabled={waiting}
            />
          </div>
          <div className="cta-row">
            <button type="submit" className="btn" disabled={waiting}>
              {waiting ? "Đang tạo…" : "Tạo bản sao lưu danh mục"}
            </button>
          </div>
        </form>
      </fieldset>

      <fieldset className="group-box" style={{ marginTop: "1.25rem" }}>
        <legend>Bản đã lưu</legend>
        {items.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có bản sao lưu.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Thời điểm</th>
                  <th scope="col">Kích thước</th>
                  <th scope="col">Checksum</th>
                  <th scope="col">Ghi chú</th>
                  <th scope="col">Khôi phục</th>
                </tr>
              </thead>
              <tbody>
                {items.map((b) => (
                  <tr key={b.id}>
                    <td>
                      {formatDateTimeVi(b.createdAt)}
                      <div className="muted small">
                        {backupStatusLabel(b.status)}
                        {b.restoredAt ? ` · đã khôi phục ${formatDateTimeVi(b.restoredAt)}` : ""}
                      </div>
                    </td>
                    <td>{formatBytes(b.byteSize)}</td>
                    <td className="mono-id">{b.checksumSha256.slice(0, 12)}…</td>
                    <td>{b.note || "—"}</td>
                    <td>
                      <form onSubmit={(e) => void restore(e, b.id)}>
                        <div className="field">
                          <label htmlFor={`confirm-${b.id}`}>
                            Gõ đúng <code>{expected}</code>
                          </label>
                          <input
                            id={`confirm-${b.id}`}
                            name="confirmPhrase"
                            required
                            disabled={waiting}
                            autoComplete="off"
                            placeholder={expected}
                          />
                        </div>
                        <button type="submit" className="btn btn-ghost" disabled={waiting}>
                          Khôi phục danh mục
                        </button>
                      </form>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </fieldset>
    </div>
  );
}
