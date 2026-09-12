"use client";

import { useEffect, useState } from "react";
import {
  DEFAULT_UI_PREFERENCES,
  UI_THEME_OPTIONS,
  persistUiPreferences,
  readUiPreferencesClient,
  type UiDensityId,
  type UiLayoutId,
  type UiPreferences,
  type UiThemeId,
} from "@/lib/ui-preferences";

export function SettingsForm() {
  const [prefs, setPrefs] = useState<UiPreferences>(DEFAULT_UI_PREFERENCES);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    setPrefs(readUiPreferencesClient());
    setReady(true);
  }, []);

  function update(partial: Partial<UiPreferences>) {
    const next = { ...prefs, ...partial };
    setPrefs(next);
    persistUiPreferences(next);
    setSavedAt(new Date().toLocaleTimeString("vi-VN"));
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
          Áp dụng ngay trên trình duyệt này. Đồng bộ theo thuê bao (máy chủ) sẽ bổ sung sau.
        </p>

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
          <label htmlFor="ui-lang">Ngôn ngữ giao diện</label>
          <select id="ui-lang" value="vi" disabled>
            <option value="vi">Tiếng Việt (CP6.5)</option>
          </select>
          <span className="muted small block">Khóa theo thuật ngữ vận hành — không đổi trong lát cắt này.</span>
        </div>
      </section>

      <section className="settings-section" aria-labelledby="settings-theme">
        <h2 id="settings-theme" className="section-title sm">
          Theme mặc định
        </h2>
        <p className="note">
          Chọn bộ màu / cảm giác mặc định cho shell. Không copy mã template thương mại — chỉ phong cách tương đương.
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

      {savedAt ? (
        <p className="muted small" role="status">
          Đã áp dụng lúc {savedAt}.
        </p>
      ) : null}
    </div>
  );
}
