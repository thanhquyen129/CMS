"use client";

import { useEffect, useState } from "react";

type PolicyRow = {
  id: string;
  policyKey: string;
  title: string;
  ownerUserId: string | null;
  version: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  status: string;
  bodyJson: string | null;
  notes: string | null;
};

const STATUS_VI: Record<string, string> = {
  draft: "Nháp",
  active: "Hiệu lực",
  superseded: "Đã thay thế",
  retired: "Ngừng",
};

export function PolicyRegistryPanel() {
  const [rows, setRows] = useState<PolicyRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editKey, setEditKey] = useState<string | null>(null);
  const [notes, setNotes] = useState("");
  const [status, setStatus] = useState("active");
  const [newVersion, setNewVersion] = useState(false);

  async function load() {
    setError(null);
    const res = await fetch("/bff/policies", { headers: { Accept: "application/json" } });
    if (!res.ok) {
      setError("Không tải được sổ chính sách.");
      return;
    }
    setRows((await res.json()) as PolicyRow[]);
  }

  useEffect(() => {
    void load();
  }, []);

  async function ensureCatalog() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch("/bff/policies/ensure-catalog", { method: "POST" });
      if (!res.ok) {
        setError("Không khởi tạo được 13 khóa chính sách.");
        return;
      }
      await load();
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    if (!editKey) return;
    setBusy(true);
    setError(null);
    try {
      const row = rows.find((r) => r.policyKey === editKey);
      const res = await fetch("/bff/policies", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          policyKey: editKey,
          title: row?.title,
          ownerUserId: row?.ownerUserId,
          effectiveFrom: row?.effectiveFrom ?? new Date().toISOString().slice(0, 10),
          effectiveTo: row?.effectiveTo,
          status,
          bodyJson: row?.bodyJson ?? "{}",
          notes: notes.trim() || null,
          createNewVersion: newVersion,
        }),
      });
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Không lưu được chính sách.");
        return;
      }
      setEditKey(null);
      await load();
    } finally {
      setBusy(false);
    }
  }

  return (
    <fieldset className="group-box">
      <legend>Sổ chính sách (13 khóa)</legend>
      <p className="note">
        Mỗi khóa có chủ sở hữu, phiên bản và ngày hiệu lực. Không ghi đè im lặng bản đang hiệu lực — dùng
        phiên bản mới khi cần.
      </p>
      <div className="toolbar-row">
        <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => void ensureCatalog()}>
          Khởi tạo 13 khóa
        </button>
      </div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {rows.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có chính sách. Bấm Khởi tạo 13 khóa.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Khóa</th>
                <th scope="col">Tiêu đề</th>
                <th scope="col">Ver</th>
                <th scope="col">Hiệu lực từ</th>
                <th scope="col">Trạng thái</th>
                <th scope="col">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id}>
                  <td>
                    <code>{r.policyKey}</code>
                  </td>
                  <td>{r.title}</td>
                  <td>{r.version}</td>
                  <td>{r.effectiveFrom}</td>
                  <td>{STATUS_VI[r.status] ?? r.status}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-link"
                      onClick={() => {
                        setEditKey(r.policyKey);
                        setNotes(r.notes ?? "");
                        setStatus(r.status);
                        setNewVersion(false);
                      }}
                    >
                      Sửa
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editKey ? (
        <div className="form-grid" style={{ marginTop: "1rem" }}>
          <p>
            Đang sửa: <strong>{editKey}</strong>
          </p>
          <label>
            Trạng thái
            <select value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="draft">Nháp</option>
              <option value="active">Hiệu lực</option>
              <option value="superseded">Đã thay thế</option>
              <option value="retired">Ngừng</option>
            </select>
          </label>
          <label>
            Ghi chú
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={3} />
          </label>
          <label className="checkbox-row">
            <input type="checkbox" checked={newVersion} onChange={(e) => setNewVersion(e.target.checked)} />
            Tạo phiên bản mới (supersede bản active cũ)
          </label>
          <div className="toolbar-row">
            <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void save()}>
              Lưu
            </button>
            <button type="button" className="btn btn-secondary" disabled={busy} onClick={() => setEditKey(null)}>
              Hủy
            </button>
          </div>
        </div>
      ) : null}
    </fieldset>
  );
}
