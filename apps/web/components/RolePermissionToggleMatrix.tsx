"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { PermissionMatrixItem } from "@/lib/access";

const SCOPES = [
  { value: "all", label: "Toàn thuê bao" },
  { value: "organization", label: "Theo tổ chức" },
  { value: "own", label: "Chỉ của mình" },
] as const;

type Props = {
  roleId: string;
  roleName: string;
  items: PermissionMatrixItem[];
};

export function RolePermissionToggleMatrix({ roleId, roleName, items }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busyCode, setBusyCode] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  async function setPermission(
    actionCode: string,
    enabled: boolean,
    dataScope: string | null
  ) {
    setError(null);
    setBusyCode(actionCode);
    try {
      const res = await fetch(`/bff/admin/access/roles/${roleId}/permissions`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          actionCode,
          enabled,
          dataScope: enabled ? dataScope || "all" : null,
        }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Không cập nhật được quyền.");
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
    <div>
      <h2 className="section-title">Quyền — {roleName}</h2>
      <p className="muted">
        Bật/tắt từng hành động. Phạm vi dữ liệu độc lập với quyền hành động
        (toàn thuê bao / tổ chức / của mình).
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="table-wrap" style={{ marginTop: "0.75rem" }}>
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Hành động</th>
              <th scope="col">Bật</th>
              <th scope="col">Phạm vi dữ liệu</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const busy = busyCode === item.actionCode || isPending;
              return (
                <tr key={item.actionCode}>
                  <td>
                    <div>{item.permissionName}</div>
                    <div className="mono-id muted">{item.actionCode}</div>
                  </td>
                  <td>
                    <label className="field" style={{ margin: 0 }}>
                      <input
                        type="checkbox"
                        checked={item.enabled}
                        disabled={busy || item.locked}
                        onChange={(e) =>
                          void setPermission(
                            item.actionCode,
                            e.target.checked,
                            item.dataScope
                          )
                        }
                        aria-label={`Bật ${item.permissionName}`}
                      />
                      {item.locked ? (
                        <span className="muted"> (khóa)</span>
                      ) : null}
                    </label>
                  </td>
                  <td>
                    <select
                      value={item.dataScope ?? "all"}
                      disabled={busy || !item.enabled || item.locked}
                      onChange={(e) =>
                        void setPermission(
                          item.actionCode,
                          true,
                          e.target.value
                        )
                      }
                      aria-label={`Phạm vi ${item.permissionName}`}
                    >
                      {SCOPES.map((s) => (
                        <option key={s.value} value={s.value}>
                          {s.label}
                        </option>
                      ))}
                    </select>
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
