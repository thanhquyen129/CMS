"use client";

import { Fragment, useCallback, useEffect, useState } from "react";
import { formatDateTimeVi } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

export type AuditEventRow = {
  id: string;
  actorId: string | null;
  action: string;
  objectType: string;
  objectId: string;
  beforeJson: string | null;
  afterJson: string | null;
  reason: string | null;
  correlationId: string | null;
  occurredAt: string;
};

type Props = {
  terms: TerminologyMap;
  objectType: string;
  objectId: string;
  /** Optional heading override. */
  title?: string;
};

function prettyJson(raw: string | null): string {
  if (!raw) return "—";
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

export function AuditTrailPanel({
  terms,
  objectType,
  objectId,
  title,
}: Props) {
  const auditLabel = term(terms, "AUDIT_TRAIL", "Nhật ký kiểm toán");
  const [rows, setRows] = useState<AuditEventRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const qs = new URLSearchParams({
        objectType,
        objectId,
        take: "50",
      });
      const res = await fetch(`/bff/audit-events?${qs.toString()}`, {
        headers: { Accept: "application/json" },
        cache: "no-store",
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(body.message || "Không tải được nhật ký kiểm toán.");
        setRows(null);
        return;
      }
      setRows((await res.json()) as AuditEventRow[]);
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
      setRows(null);
    } finally {
      setLoading(false);
    }
  }, [objectId, objectType]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <section className="panel-section" aria-labelledby={`audit-${objectId}`}>
      <h2 id={`audit-${objectId}`} className="section-title">
        {title ?? auditLabel}
      </h2>
      <p className="note">
        Sự kiện nghiệp vụ trên đối tượng tiền — trước/sau JSON (không sửa sổ
        tại đây).
      </p>

      {loading ? (
        <p className="muted">Đang tải {auditLabel.toLowerCase()}…</p>
      ) : error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : !rows || rows.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có sự kiện kiểm toán cho đối tượng này.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Thời điểm</th>
                <th scope="col">Hành động</th>
                <th scope="col">Lý do</th>
                <th scope="col">Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const open = expanded === row.id;
                return (
                  <Fragment key={row.id}>
                    <tr>
                      <td>{formatDateTimeVi(row.occurredAt)}</td>
                      <td>
                        <code className="mono-id">{row.action}</code>
                      </td>
                      <td>{row.reason?.trim() || "—"}</td>
                      <td>
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm"
                          aria-expanded={open}
                          onClick={() =>
                            setExpanded(open ? null : row.id)
                          }
                        >
                          {open ? "Thu gọn" : "Trước / sau"}
                        </button>
                      </td>
                    </tr>
                    {open ? (
                      <tr>
                        <td colSpan={4}>
                          <div className="dash-split">
                            <div>
                              <h3 className="section-title sm">Trước</h3>
                              <pre className="note mono-id" style={{ whiteSpace: "pre-wrap" }}>
                                {prettyJson(row.beforeJson)}
                              </pre>
                            </div>
                            <div>
                              <h3 className="section-title sm">Sau</h3>
                              <pre className="note mono-id" style={{ whiteSpace: "pre-wrap" }}>
                                {prettyJson(row.afterJson)}
                              </pre>
                            </div>
                          </div>
                        </td>
                      </tr>
                    ) : null}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
