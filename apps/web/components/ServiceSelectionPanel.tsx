"use client";

import { useEffect, useMemo, useState } from "react";

export type ServiceOption = {
  serviceCode: string;
  name: string;
  selectionScope: string;
  hasBuyPrice: boolean;
  hasSellPrice: boolean;
};

type SelectionRow = {
  id?: string;
  serviceCode: string;
  scope: string;
  quantity: number;
  isSelected: boolean;
  revision: number;
  confirmedAt?: string | null;
  confirmedByUserId?: string | null;
};

type AuditRow = {
  serviceCode: string;
  oldIsSelected: boolean;
  newIsSelected: boolean;
  oldQuantity: number;
  newQuantity: number;
  revision: number;
  actorUserId?: string | null;
  actedAt: string;
  notes?: string | null;
};

type Draft = {
  selected: boolean;
  quantity: number;
  dirty: boolean;
};

export function ServiceSelectionPanel({
  contextType,
  contextId,
  options,
}: {
  contextType: "BILL" | "SHIPMENT" | "LEG";
  contextId: string;
  options: ServiceOption[];
}) {
  const [rows, setRows] = useState<SelectionRow[]>([]);
  const [audits, setAudits] = useState<AuditRow[]>([]);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [showAudit, setShowAudit] = useState(false);

  async function reload() {
    const qs = new URLSearchParams({ contextType, contextId });
    const [selRes, auditRes] = await Promise.all([
      fetch(`/bff/service-selections?${qs}`, { headers: { Accept: "application/json" } }),
      fetch(`/bff/service-selections/audits?${qs}`, { headers: { Accept: "application/json" } }),
    ]);
    const sel = (await selRes.json().catch(() => [])) as SelectionRow[];
    const aud = (await auditRes.json().catch(() => [])) as AuditRow[];
    if (!selRes.ok) {
      setError((sel as unknown as { message?: string }).message || "Không tải được lựa chọn dịch vụ.");
      return;
    }
    setRows(Array.isArray(sel) ? sel : []);
    setAudits(Array.isArray(aud) ? aud : []);
    const next: Record<string, Draft> = {};
    for (const opt of options) {
      const saved = (Array.isArray(sel) ? sel : []).find(
        (r) => r.serviceCode.toUpperCase() === opt.serviceCode.toUpperCase()
      );
      next[opt.serviceCode] = {
        selected: saved?.isSelected === true,
        quantity: saved?.quantity && saved.quantity > 0 ? saved.quantity : 1,
        dirty: false,
      };
    }
    setDrafts(next);
  }

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contextType, contextId, options.map((o) => o.serviceCode).join("|")]);

  const dirtyCount = useMemo(
    () => Object.values(drafts).filter((d) => d.dirty).length,
    [drafts]
  );

  async function saveAll() {
    setBusy(true);
    setError(null);
    try {
      for (const opt of options) {
        const draft = drafts[opt.serviceCode];
        if (!draft?.dirty) continue;
        const saved = rows.find((r) => r.serviceCode.toUpperCase() === opt.serviceCode.toUpperCase());
        const res = await fetch("/bff/service-selections", {
          method: "PUT",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify({
            contextType,
            contextId,
            serviceCode: opt.serviceCode,
            isSelected: draft.selected,
            quantity: draft.quantity,
            scope: opt.selectionScope || contextType,
            expectedRevision: saved?.revision,
          }),
        });
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        if (!res.ok) {
          setError(payload.message || `Không lưu được dịch vụ ${opt.serviceCode}.`);
          return;
        }
      }
      await reload();
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  if (options.length === 0) {
    return (
      <div className="panel" id="optional-services">
        <h3 className="section-title sm">Dịch vụ / Phụ phí tùy chọn</h3>
        <p className="muted">Chưa có phụ phí OPTIONAL_OPT_IN đang phát hành cho thuê bao này.</p>
      </div>
    );
  }

  return (
    <div className="panel stack" id="optional-services" style={{ marginTop: "1rem" }}>
      <h3 className="section-title sm">Dịch vụ / Phụ phí tùy chọn</h3>
      <p className="note">
        Mặc định không tích chọn. Chỉ sau khi bấm <strong>Lưu lựa chọn dịch vụ</strong> hệ thống mới kích hoạt phụ phí khi tính giá.
        {dirtyCount > 0 ? ` · ${dirtyCount} thay đổi chưa lưu` : ""}
      </p>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Chọn</th>
              <th>Dịch vụ</th>
              <th>Phạm vi</th>
              <th className="num">Số lượng</th>
              <th>Giá BUY/SELL</th>
              <th>Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {options.map((opt) => {
              const draft = drafts[opt.serviceCode] ?? { selected: false, quantity: 1, dirty: false };
              const saved = rows.find((r) => r.serviceCode.toUpperCase() === opt.serviceCode.toUpperCase());
              const status = draft.dirty
                ? "Chưa lưu / Chưa chọn"
                : saved?.isSelected
                  ? "Đã lưu xác nhận"
                  : "Chưa lưu / Chưa chọn";
              return (
                <tr key={opt.serviceCode}>
                  <td>
                    <input
                      type="checkbox"
                      checked={draft.selected}
                      aria-label={`Chọn ${opt.serviceCode}`}
                      onChange={(e) =>
                        setDrafts((prev) => ({
                          ...prev,
                          [opt.serviceCode]: {
                            ...draft,
                            selected: e.target.checked,
                            dirty: true,
                          },
                        }))
                      }
                    />
                  </td>
                  <td>
                    <code>{opt.serviceCode}</code>
                    <span className="muted small block">{opt.name}</span>
                  </td>
                  <td>{opt.selectionScope || contextType}</td>
                  <td className="num">
                    <input
                      type="number"
                      min={0.0001}
                      step="any"
                      value={draft.quantity}
                      style={{ width: "5rem" }}
                      onChange={(e) =>
                        setDrafts((prev) => ({
                          ...prev,
                          [opt.serviceCode]: {
                            ...draft,
                            quantity: Number(e.target.value) || 1,
                            dirty: true,
                          },
                        }))
                      }
                    />
                  </td>
                  <td>
                    {opt.hasBuyPrice ? "Có giá BUY" : "UNPRICED BUY"}
                    {" · "}
                    {opt.hasSellPrice ? "Có giá SELL" : "UNPRICED SELL"}
                  </td>
                  <td>
                    <span className={draft.dirty || !saved?.isSelected ? "badge badge-subtle" : "badge"}>
                      {status}
                    </span>
                    {saved?.revision ? (
                      <span className="muted small block">revision {saved.revision}</span>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      <div className="cta-row">
        <button className="btn btn-primary" type="button" disabled={busy || dirtyCount === 0} onClick={() => void saveAll()}>
          {busy ? "Đang lưu…" : "Lưu lựa chọn dịch vụ"}
        </button>
        <button className="btn" type="button" onClick={() => setShowAudit((v) => !v)}>
          {showAudit ? "Ẩn lịch sử Audit" : "Xem lịch sử Audit"}
        </button>
      </div>
      {showAudit ? (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Thời điểm</th>
                <th>Dịch vụ</th>
                <th>Trước → Sau</th>
                <th>Số lượng</th>
                <th>Revision</th>
                <th>Actor</th>
              </tr>
            </thead>
            <tbody>
              {audits.length === 0 ? (
                <tr>
                  <td colSpan={6} className="muted">Chưa có lịch sử lựa chọn.</td>
                </tr>
              ) : (
                audits.map((a, idx) => (
                  <tr key={`${a.serviceCode}-${a.revision}-${idx}`}>
                    <td>{new Date(a.actedAt).toLocaleString("vi-VN")}</td>
                    <td><code>{a.serviceCode}</code></td>
                    <td>{a.oldIsSelected ? "Có" : "Không"} → {a.newIsSelected ? "Có" : "Không"}</td>
                    <td>{a.oldQuantity} → {a.newQuantity}</td>
                    <td>{a.revision}</td>
                    <td>{a.actorUserId || "—"}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
