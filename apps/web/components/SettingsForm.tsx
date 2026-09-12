"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState } from "react";
import {
  DEFAULT_UI_PREFERENCES,
  UI_HOME_OPTIONS,
  UI_THEME_OPTIONS,
  clearUiPreferences,
  parseUiPreferences,
  persistUiPreferences,
  readUiPreferencesClient,
  type UiDensityId,
  type UiHomePath,
  type UiLayoutId,
  type UiPreferences,
  type UiThemeId,
} from "@/lib/ui-preferences";

type ProbeState = "idle" | "loading" | "ok" | "fail";

type ProbeResult = {
  health: ProbeState;
  ready: ProbeState;
  session: ProbeState;
  detail: string | null;
};

const EMPTY_PROBE: ProbeResult = {
  health: "idle",
  ready: "idle",
  session: "idle",
  detail: null,
};

function ToggleRow(props: {
  id: string;
  label: string;
  hint: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <div className="settings-toggle">
      <div>
        <label htmlFor={props.id}>{props.label}</label>
        <span className="muted small block">{props.hint}</span>
      </div>
      <input
        id={props.id}
        type="checkbox"
        className="settings-switch"
        checked={props.checked}
        onChange={(e) => props.onChange(e.target.checked)}
      />
    </div>
  );
}

export function SettingsForm() {
  const importId = useId();
  const fileRef = useRef<HTMLInputElement>(null);
  const [prefs, setPrefs] = useState<UiPreferences>(DEFAULT_UI_PREFERENCES);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [probe, setProbe] = useState<ProbeResult>(EMPTY_PROBE);
  const [utilMsg, setUtilMsg] = useState<string | null>(null);

  useEffect(() => {
    setPrefs(readUiPreferencesClient());
    setReady(true);
  }, []);

  function update(partial: Partial<UiPreferences>) {
    const next = { ...prefs, ...partial };
    setPrefs(next);
    persistUiPreferences(next);
    setSavedAt(new Date().toLocaleTimeString("vi-VN"));
    setUtilMsg(null);
  }

  async function runDiagnostics() {
    setProbe({ health: "loading", ready: "loading", session: "loading", detail: null });
    const detailParts: string[] = [];

    async function hit(
      url: string,
      key: "health" | "ready" | "session",
    ): Promise<ProbeState> {
      try {
        const res = await fetch(url, { cache: "no-store" });
        const text = await res.text();
        detailParts.push(`${key}: HTTP ${res.status} ${text.slice(0, 120)}`);
        return res.ok ? "ok" : "fail";
      } catch (e) {
        detailParts.push(`${key}: ${e instanceof Error ? e.message : "lỗi mạng"}`);
        return "fail";
      }
    }

    const [health, readyState, session] = await Promise.all([
      hit("/bff/system/health", "health"),
      hit("/bff/system/ready", "ready"),
      hit("/bff/auth/session", "session"),
    ]);

    setProbe({
      health,
      ready: readyState,
      session,
      detail: detailParts.join(" · "),
    });
  }

  function exportPrefs() {
    const blob = new Blob([JSON.stringify(prefs, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `cms-ui-preferences-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setUtilMsg("Đã tải file cấu hình giao diện.");
  }

  function onImportFile(file: File | null) {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const applied = parseUiPreferences(String(reader.result ?? ""));
        persistUiPreferences(applied);
        setPrefs(applied);
        setSavedAt(new Date().toLocaleTimeString("vi-VN"));
        setUtilMsg("Đã nhập cấu hình từ file.");
      } catch {
        setUtilMsg("File không hợp lệ — cần JSON cấu hình CMS UI.");
      }
    };
    reader.readAsText(file);
  }

  function resetAll() {
    if (!window.confirm("Khôi phục toàn bộ tùy chọn giao diện về mặc định?")) return;
    clearUiPreferences();
    setPrefs(readUiPreferencesClient());
    setSavedAt(new Date().toLocaleTimeString("vi-VN"));
    setUtilMsg("Đã khôi phục mặc định (Invoika Soft + menu ngang).");
  }

  if (!ready) {
    return (
      <div className="stack">
        <div className="skeleton-line w-55" />
        <div className="skeleton-block" />
      </div>
    );
  }

  return (
    <div className="stack settings-form">
      <section className="settings-section" aria-labelledby="settings-basic">
        <h2 id="settings-basic" className="section-title sm">
          Thiết lập cơ bản
        </h2>
        <p className="note">
          Áp dụng ngay trên trình duyệt này. Không đổi số liệu tài chính hay quyền trên máy chủ.
        </p>

        <div className="form-grid settings-grid">
          <div className="field">
            <label htmlFor="ui-layout">Bố cục điều hướng</label>
            <select
              id="ui-layout"
              value={prefs.layout}
              onChange={(e) => update({ layout: e.target.value as UiLayoutId })}
            >
              <option value="horizontal">Ngang (Invoika)</option>
              <option value="vertical">Dọc (sidebar)</option>
            </select>
          </div>

          <div className="field">
            <label htmlFor="ui-density">Mật độ giao diện</label>
            <select
              id="ui-density"
              value={prefs.density}
              onChange={(e) => update({ density: e.target.value as UiDensityId })}
            >
              <option value="comfortable">Thoáng</option>
              <option value="compact">Gọn</option>
            </select>
          </div>

          <div className="field">
            <label htmlFor="ui-home">Trang vào sau đăng nhập</label>
            <select
              id="ui-home"
              value={prefs.homePath}
              onChange={(e) => update({ homePath: e.target.value as UiHomePath })}
            >
              {UI_HOME_OPTIONS.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="ui-lang">Ngôn ngữ giao diện</label>
            <select id="ui-lang" value="vi" disabled>
              <option value="vi">Tiếng Việt (CP6.5)</option>
            </select>
            <span className="muted small block">Khóa theo thuật ngữ vận hành.</span>
          </div>
        </div>
      </section>

      <section className="settings-section" aria-labelledby="settings-display">
        <h2 id="settings-display" className="section-title sm">
          Hiển thị &amp; tiện nghi
        </h2>
        <p className="note">Bật/tắt hành vi shell. Thao tác tiền (xóa nợ, đảo phân bổ…) vẫn luôn yêu cầu xác nhận.</p>

        <div className="settings-toggles">
          <ToggleRow
            id="ui-zebra"
            label="Sọc xen kẽ bảng"
            hint="Dễ đọc danh sách dài (Bill, hàng đợi, sao kê)."
            checked={prefs.tableZebra}
            onChange={(v) => update({ tableZebra: v })}
          />
          <ToggleRow
            id="ui-sticky"
            label="Ghim thanh điều hướng"
            hint="Menu ngang/dọc giữ khi cuộn trang."
            checked={prefs.stickyNav}
            onChange={(v) => update({ stickyNav: v })}
          />
          <ToggleRow
            id="ui-motion"
            label="Giảm chuyển động"
            hint="Tắt animation skeleton / hiệu ứng ngắn."
            checked={prefs.reduceMotion}
            onChange={(v) => update({ reduceMotion: v })}
          />
          <ToggleRow
            id="ui-labels"
            label="Nhãn nhóm menu (ngang)"
            hint="Hiện «Chính / Hàng đợi / Hệ thống» khi layout ngang."
            checked={prefs.showNavLabels}
            onChange={(v) => update({ showNavLabels: v })}
          />
          <ToggleRow
            id="ui-queues"
            label="Hiện nhóm hàng đợi trên menu"
            hint="Ẩn ngoại lệ / phê duyệt / đối soát nếu không dùng hàng ngày."
            checked={prefs.showQueues}
            onChange={(v) => update({ showQueues: v })}
          />
        </div>
      </section>

      <section className="settings-section" aria-labelledby="settings-theme">
        <h2 id="settings-theme" className="section-title sm">
          Theme mặc định
        </h2>
        <p className="note">
          Chọn bộ màu / cảm giác mặc định. Chọn theme sẽ gợi ý bố cục phù hợp (có thể đổi lại ở trên).
        </p>

        <div className="theme-picker" role="radiogroup" aria-label="Theme mặc định">
          {UI_THEME_OPTIONS.map((opt) => {
            const selected = prefs.theme === opt.id;
            return (
              <button
                key={opt.id}
                type="button"
                role="radio"
                aria-checked={selected}
                className={`theme-card${selected ? " selected" : ""}`}
                onClick={() => {
                  const next: Partial<UiPreferences> = { theme: opt.id as UiThemeId };
                  if (opt.id === "invoika") next.layout = "horizontal";
                  if (opt.id === "soft-purple" || opt.id === "classic") next.layout = "vertical";
                  update(next);
                }}
              >
                <div className="theme-swatches" aria-hidden="true">
                  {opt.swatches.map((c) => (
                    <span key={c} style={{ background: c }} />
                  ))}
                </div>
                <strong>{opt.label}</strong>
                <span className="muted small">{opt.description}</span>
                {selected ? <span className="theme-badge">Đang dùng</span> : null}
              </button>
            );
          })}
        </div>
      </section>

      <section className="settings-section" aria-labelledby="settings-shortcuts">
        <h2 id="settings-shortcuts" className="section-title sm">
          Lối tắt vận hành
        </h2>
        <p className="note">Đi thẳng tới màn làm việc thường dùng.</p>
        <div className="settings-shortcuts">
          {UI_HOME_OPTIONS.filter((o) => o.id !== "/settings").map((o) => (
            <Link key={o.id} className="btn btn-ghost btn-sm" href={o.id}>
              {o.label}
            </Link>
          ))}
        </div>
      </section>

      <section className="settings-section" aria-labelledby="settings-utils">
        <h2 id="settings-utils" className="section-title sm">
          Tiện ích quản trị
        </h2>
        <p className="note">
          Chẩn đoán kết nối, xuất/nhập cấu hình UI, khôi phục mặc định. Không xóa dữ liệu Bill/AP-AR.
        </p>

        <div className="row-actions" style={{ marginBottom: "0.85rem" }}>
          <button type="button" className="btn btn-sm" onClick={() => void runDiagnostics()}>
            Kiểm tra hệ thống
          </button>
          <button type="button" className="btn btn-ghost btn-sm" onClick={exportPrefs}>
            Xuất cấu hình UI
          </button>
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={() => fileRef.current?.click()}
          >
            Nhập cấu hình UI
          </button>
          <button type="button" className="btn btn-ghost btn-sm" onClick={resetAll}>
            Khôi phục mặc định
          </button>
          <input
            id={importId}
            ref={fileRef}
            type="file"
            accept="application/json,.json"
            className="sr-only"
            onChange={(e) => {
              onImportFile(e.target.files?.[0] ?? null);
              e.target.value = "";
            }}
          />
        </div>

        {(probe.health !== "idle" || probe.ready !== "idle" || probe.session !== "idle") && (
          <dl className="metric-grid settings-probe">
            <div>
              <dt>API /health</dt>
              <dd data-state={probe.health}>
                {probe.health === "loading" ? "…" : probe.health === "ok" ? "OK" : "Lỗi"}
              </dd>
            </div>
            <div>
              <dt>API /ready</dt>
              <dd data-state={probe.ready}>
                {probe.ready === "loading" ? "…" : probe.ready === "ok" ? "OK" : "Lỗi"}
              </dd>
            </div>
            <div>
              <dt>Phiên đăng nhập</dt>
              <dd data-state={probe.session}>
                {probe.session === "loading"
                  ? "…"
                  : probe.session === "ok"
                    ? "Có cookie"
                    : "Không hợp lệ"}
              </dd>
            </div>
          </dl>
        )}
        {probe.detail ? <p className="muted small mono-id">{probe.detail}</p> : null}
        {utilMsg ? (
          <p className="alert alert-info" role="status">
            {utilMsg}
          </p>
        ) : null}
      </section>

      {savedAt ? (
        <p className="muted small" role="status">
          Đã áp dụng lúc {savedAt}.
        </p>
      ) : null}
    </div>
  );
}
