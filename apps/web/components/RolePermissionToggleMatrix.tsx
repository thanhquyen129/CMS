"use client";

import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import type { PermissionMatrixItem } from "@/lib/access";

const SCOPES = [
  { value: "all", label: "Toàn thuê bao" },
  { value: "organization", label: "Theo tổ chức" },
  { value: "own", label: "Chỉ của mình" },
] as const;

const GROUPS: { id: string; title: string; match: (code: string) => boolean }[] =
  [
    {
      id: "bill",
      title: "Bill",
      match: (c) => c.startsWith("bill."),
    },
    {
      id: "cost",
      title: "Chi phí",
      match: (c) => c.startsWith("cost."),
    },
    {
      id: "revenue",
      title: "Doanh thu",
      match: (c) => c.startsWith("revenue."),
    },
    {
      id: "settlement",
      title: "AP / AR",
      match: (c) => c.startsWith("ap.") || c.startsWith("ar."),
    },
    {
      id: "master",
      title: "Danh mục",
      match: (c) => c.startsWith("master."),
    },
    {
      id: "system",
      title: "Hệ thống",
      match: (c) =>
        c.startsWith("user.") ||
        c.startsWith("role.") ||
        c.startsWith("settings."),
    },
  ];

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

  const groups = useMemo(() => {
    const used = new Set<string>();
    const result: { id: string; title: string; items: PermissionMatrixItem[] }[] =
      [];
    for (const g of GROUPS) {
      const slice = items.filter((i) => g.match(i.actionCode));
      slice.forEach((i) => used.add(i.actionCode));
      if (slice.length > 0) {
        result.push({ id: g.id, title: g.title, items: slice });
      }
    }
    const rest = items.filter((i) => !used.has(i.actionCode));
    if (rest.length > 0) {
      result.push({ id: "other", title: "Khác", items: rest });
    }
    return result;
  }, [items]);

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
        Bật/tắt theo nhóm nghiệp vụ. Phạm vi dữ liệu độc lập với quyền hành
        động.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="perm-group-grid">
        {groups.map((group) => (
          <fieldset key={group.id} className="group-box">
            <legend>{group.title}</legend>
            {group.items.map((item) => {
              const busy = busyCode === item.actionCode || isPending;
              return (
                <div key={item.actionCode} className="perm-item">
                  <div className="perm-item-meta">
                    <div>{item.permissionName}</div>
                    <div className="mono-id muted">{item.actionCode}</div>
                  </div>
                  <div className="perm-item-controls">
                    <label>
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
                      Bật
                      {item.locked ? (
                        <span className="muted"> (khóa)</span>
                      ) : null}
                    </label>
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
                  </div>
                </div>
              );
            })}
          </fieldset>
        ))}
      </div>
    </div>
  );
}
