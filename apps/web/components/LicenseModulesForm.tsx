"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { licenseStatusLabel, type TenantLicense } from "@/lib/tenant-admin-model";
import { formatDateTimeVi } from "@/lib/money";

export function LicenseModulesForm({ license }: { license: TenantLicense }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busyCode, setBusyCode] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  async function setEnabled(code: string, isEnabled: boolean) {
    setError(null);
    setBusyCode(code);
    try {
      const res = await fetch(`/bff/tenant-license/modules/${encodeURIComponent(code)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ isEnabled }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không đổi được module.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusyCode(null);
    }
  }

  const seatPct =
    license.seatLimit > 0
      ? Math.min(100, Math.round((license.seatsUsed / license.seatLimit) * 100))
      : 0;

  return (
    <div>
      <dl className="kv-list">
        <div>
          <dt>Gói</dt>
          <dd>
            {license.planName} ({license.planCode})
          </dd>
        </div>
        <div>
          <dt>Trạng thái</dt>
          <dd>{licenseStatusLabel(license.status)}</dd>
        </div>
        <div>
          <dt>Hiệu lực</dt>
          <dd>
            {formatDateTimeVi(license.validFrom)} → {formatDateTimeVi(license.validUntil)}
          </dd>
        </div>
        <div>
          <dt>Chỗ người dùng</dt>
          <dd>
            {license.seatsUsed} / {license.seatLimit} ({seatPct}%)
          </dd>
        </div>
      </dl>
      {license.notes ? <p className="note">{license.notes}</p> : null}
      <p className="note">
        Tắt module chỉ ẩn menu. Không xóa bảng, không fork Core Model. Không bật được module
        ngoài gói. Số chỗ tính tài khoản đang dùng.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Module</th>
              <th scope="col">Trong gói</th>
              <th scope="col">Đang bật</th>
            </tr>
          </thead>
          <tbody>
            {license.modules.map((m) => {
              const waiting = busyCode === m.code || isPending;
              return (
                <tr key={m.code}>
                  <td>
                    <strong>{m.name}</strong>
                    <div className="muted small">{m.code}</div>
                  </td>
                  <td>{m.includedInPlan ? "Có" : "Không"}</td>
                  <td>
                    <label>
                      <input
                        type="checkbox"
                        checked={m.isEnabled}
                        disabled={waiting || !m.includedInPlan}
                        onChange={(e) => void setEnabled(m.code, e.target.checked)}
                      />{" "}
                      {m.isEnabled ? "Bật" : "Tắt"}
                    </label>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
