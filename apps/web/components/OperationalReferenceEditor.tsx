"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { SPECIAL_FLAGS } from "@/lib/create-workspace";
import {
  FIELD_GROUP_LABELS,
  TRANSPORT_MODE_OPTIONS,
  chargeableStateLabel,
  formatChargeable,
  sourceBadgeClass,
  type ApiError,
  type OprefEditResult,
  type OprefEditView,
  type OprefFieldState,
  type OprefObjectType,
} from "@/lib/opref-edit";

type Props = {
  objectType: OprefObjectType;
  objectId: string;
  /** Where "Tính giá lại" goes (Bill only). */
  ratingHref?: string;
};

const CW = "chargeable_weight_kg";

function base(type: OprefObjectType, id: string) {
  return `/bff/operational-references/${type}/${encodeURIComponent(id)}`;
}

function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function fromLocalInput(local: string): string | null {
  if (!local.trim()) return null;
  const d = new Date(local);
  return Number.isNaN(d.getTime()) ? null : d.toISOString().replace(/\.\d{3}Z$/, "Z");
}

function initialDraft(view: OprefEditView): Record<string, string> {
  const draft: Record<string, string> = {};
  for (const f of view.fields) {
    draft[f.code] = f.kind === "datetime" ? toLocalInput(f.value) : f.value ?? "";
  }
  if (view.chargeable) {
    draft[CW] = view.chargeable.value === null ? "" : String(view.chargeable.value);
  }
  return draft;
}

function normalize(field: OprefFieldState | null, raw: string): string | null {
  const text = raw.trim();
  if (!text) return null;
  if (field?.kind === "datetime") return fromLocalInput(text);
  if (field?.kind === "decimal" || field?.kind === "integer" || !field) return text.replace(",", ".");
  return text;
}

function sameValue(field: OprefFieldState | null, original: string | null, next: string | null): boolean {
  if (original === next) return true;
  if (original === null || next === null) return false;
  if (!field || field.kind === "decimal" || field.kind === "integer") {
    return Number(original) === Number(next);
  }
  if (field.kind === "datetime") return new Date(original).getTime() === new Date(next).getTime();
  if (field.kind === "flags") {
    const a = original.split(",").map((s) => s.trim()).filter(Boolean).sort().join(",");
    const b = next.split(",").map((s) => s.trim()).filter(Boolean).sort().join(",");
    return a === b;
  }
  return false;
}

export function OperationalReferenceEditor({ objectType, objectId, ratingHref }: Props) {
  const router = useRouter();
  const [view, setView] = useState<OprefEditView | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const res = await fetch(`${base(objectType, objectId)}/edit`, { cache: "no-store" });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      const payload = (await res.json().catch(() => ({}))) as OprefEditView & ApiError;
      if (!res.ok) {
        setLoadError(payload.message ?? "Không tải được thông tin vận hành.");
        return;
      }
      setView(payload);
      setDraft(initialDraft(payload));
    } catch {
      setLoadError("Không kết nối được máy chủ. Thử lại sau.");
    }
  }, [objectType, objectId]);

  useEffect(() => {
    void load();
  }, [load]);

  const changes = useMemo(() => {
    if (!view) return [] as { code: string; value: string | null; field: OprefFieldState | null }[];
    const list: { code: string; value: string | null; field: OprefFieldState | null }[] = [];
    for (const f of view.fields) {
      if (!f.editable) continue;
      const next = normalize(f, draft[f.code] ?? "");
      if (!sameValue(f, f.value, next)) list.push({ code: f.code, value: next, field: f });
    }
    if (view.chargeable) {
      const original = view.chargeable.value === null ? null : String(view.chargeable.value);
      const next = normalize(null, draft[CW] ?? "");
      if (!sameValue(null, original, next)) list.push({ code: CW, value: next, field: null });
    }
    return list;
  }, [draft, view]);

  const cwNeedsReason = useMemo(() => {
    const cw = view?.chargeable;
    if (!cw) return false;
    return cw.state !== "missing" && !(cw.state === "manual" && !cw.isConfirmed);
  }, [view]);

  const reasonRequired = changes.some((c) => (c.field ? c.field.requiresReason : cwNeedsReason));

  async function send(body: Record<string, unknown>): Promise<OprefEditResult | null> {
    const res = await fetch(base(objectType, objectId), {
      method: "PATCH",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
        ...(view?.rowVersion ? { "If-Match": view.rowVersion } : {}),
      },
      body: JSON.stringify(body),
    });
    if (res.status === 401) {
      window.location.href = "/login";
      return null;
    }
    const payload = (await res.json().catch(() => ({}))) as OprefEditResult & ApiError & {
      errors?: Record<string, string[]>;
    };
    if (!res.ok) {
      const detail = payload.errors ? Object.values(payload.errors).flat().join(" ") : "";
      setError(
        [payload.message, detail].filter(Boolean).join(" ") ||
          (res.status === 409
            ? "Dữ liệu đã bị thay đổi bởi người khác. Tải lại và thử lại."
            : "Lưu thất bại.")
      );
      return null;
    }
    return payload;
  }

  function afterSave(result: OprefEditResult) {
    const parts = [
      result.changedFields.length > 0
        ? `Đã lưu ${result.changedFields.length} thay đổi.`
        : "Không có thay đổi.",
    ];
    if (result.ratingsMarkedStale > 0) {
      parts.push(`${result.ratingsMarkedStale} lần tính giá chuyển sang “Cần tính giá lại”. Kết quả cũ vẫn được giữ.`);
    }
    setNotice(parts.join(" "));
    setEditing(false);
    setReason("");
    void load();
    router.refresh();
  }

  async function onSave() {
    setError(null);
    setNotice(null);
    if (changes.length === 0) {
      setError("Chưa có thay đổi nào.");
      return;
    }
    if (reasonRequired && !reason.trim()) {
      setError("Nhập Lý do ghi đè cho trường lấy từ hệ thống nguồn / số đã xác nhận.");
      return;
    }
    setBusy(true);
    try {
      const result = await send({
        changes: Object.fromEntries(changes.map((c) => [c.code, c.value])),
        reason: reason.trim() || null,
      });
      if (result) afterSave(result);
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function onRevert(code: string) {
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      const result = await send({ changes: {}, revertFields: [code], reason: reason.trim() || null });
      if (result) afterSave(result);
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function onConfirmCw() {
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      const res = await fetch(`${base(objectType, objectId)}/chargeable-weight/confirm`, {
        method: "POST",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      const payload = (await res.json().catch(() => ({}))) as ApiError;
      if (!res.ok) {
        setError(payload.message ?? "Không xác nhận được Trọng lượng tính cước.");
        return;
      }
      setNotice("Đã xác nhận Trọng lượng tính cước.");
      void load();
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  if (loadError) {
    return (
      <div className="alert alert-error" role="alert">
        {loadError}
      </div>
    );
  }
  if (!view) {
    return (
      <p className="muted" role="status">
        Đang tải thông tin vận hành…
      </p>
    );
  }

  const cw = view.chargeable;
  const staleRatings = view.currentRatings.filter((r) => r.stale);
  const groups = Array.from(new Set(view.fields.map((f) => f.group)));

  return (
    <section className="panel opref-editor" aria-labelledby={`opref-${objectId}`}>
      <div className="opref-head">
        <div>
          <h3 className="section-title sm" id={`opref-${objectId}`}>
            Thông tin vận hành &amp; ngữ cảnh tính giá
          </h3>
          <p className="muted small">
            Nguồn dữ liệu: {view.sourceLabel}
            {view.objectType !== "bill" && view.linkedBillCount > 0
              ? ` · Liên kết ${view.linkedBillCount} Bill`
              : ""}
          </p>
        </div>
        {!editing ? (
          <button
            type="button"
            className="btn btn-sm"
            disabled={!view.canEdit || busy}
            title={view.canEdit ? undefined : view.readOnlyReason ?? undefined}
            onClick={() => {
              setError(null);
              setNotice(null);
              setDraft(initialDraft(view));
              setEditing(true);
            }}
          >
            Sửa thông tin {view.objectLabel}
          </button>
        ) : null}
      </div>

      {!view.canEdit && view.readOnlyReason ? <p className="muted small">{view.readOnlyReason}</p> : null}
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {notice ? (
        <div className="alert alert-info" role="status">
          {notice}
        </div>
      ) : null}

      {staleRatings.length > 0 ? (
        <div className="alert alert-warning" role="status">
          <strong>Cần tính giá lại</strong> — {staleRatings.length} lần tính giá dùng dữ liệu đã thay đổi. Kết quả cũ
          giữ nguyên, không tự cập nhật Chi phí / Doanh thu.
          <ul className="readiness-list">
            {staleRatings.map((r) => (
              <li key={r.ratingId}>
                {view.objectType === "bill" ? null : (
                  <>
                    <Link className="row-link" href={`/bills/${r.billId}?tab=rating#bill-rating`}>
                      {r.billNo}
                    </Link>
                    :{" "}
                  </>
                )}
                {r.staleReason ?? "Ngữ cảnh tính giá đã đổi."}
              </li>
            ))}
          </ul>
          {view.objectType === "bill" && ratingHref ? (
            <Link className="btn btn-sm btn-ghost" href={ratingHref}>
              Tính giá lại
            </Link>
          ) : null}
        </div>
      ) : null}

      {cw ? (
        <div className="opref-cw">
          <dl className="kv-list">
            <div>
              <dt>Trọng lượng tính cước</dt>
              <dd className={cw.value === null ? "opref-cw-value is-missing" : "opref-cw-value"}>
                {cw.value === null ? "Chưa xác định / Chưa tính được" : formatChargeable(cw.value, cw.uom)}
              </dd>
            </div>
            <div>
              <dt>Trạng thái</dt>
              <dd>
                <span className={sourceBadgeClass(cw.state === "missing" ? "manual" : cw.state === "source" ? "import" : cw.state)}>
                  {chargeableStateLabel(cw)}
                </span>
              </dd>
            </div>
            {cw.ruleCode ? (
              <div>
                <dt>Quy tắc</dt>
                <dd>{cw.ruleCode}</dd>
              </div>
            ) : null}
            {cw.state === "override" ? (
              <div>
                <dt>Lý do ghi đè</dt>
                <dd>
                  {cw.overrideReason ?? "—"}
                  {cw.sourceValue !== null ? (
                    <span className="muted small block">Giá trị nguồn: {formatChargeable(cw.sourceValue, cw.uom)}</span>
                  ) : null}
                </dd>
              </div>
            ) : null}
          </dl>
          {cw.value === null && cw.missingReason ? <p className="muted small">{cw.missingReason}</p> : null}
          <div className="row-actions">
            {!cw.isConfirmed && cw.state !== "override" ? (
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                disabled={!cw.canConfirm || busy || !view.canEdit}
                title={cw.canConfirm ? undefined : "Chưa có Trọng lượng tính cước hợp lệ để xác nhận."}
                onClick={onConfirmCw}
              >
                Xác nhận TL tính cước
              </button>
            ) : null}
            {cw.state === "override" && view.canEdit ? (
              <button type="button" className="btn btn-sm btn-ghost" disabled={busy} onClick={() => onRevert(CW)}>
                Bỏ ghi đè
              </button>
            ) : null}
          </div>
        </div>
      ) : null}

      {editing ? (
        <div className="stack">
          {groups.map((group) => (
            <div className="opref-group" key={group}>
              <h4>{FIELD_GROUP_LABELS[group] ?? group}</h4>
              <div className="form-grid">
                {view.fields
                  .filter((f) => f.group === group)
                  .map((f) => (
                    <FieldInput
                      key={f.code}
                      field={f}
                      value={draft[f.code] ?? ""}
                      commodities={view.commodities}
                      disabled={busy}
                      onChange={(v) => setDraft((d) => ({ ...d, [f.code]: v }))}
                      onRevert={() => onRevert(f.code)}
                    />
                  ))}
                {group === "cargo" && cw ? (
                  <div className="field">
                    <label htmlFor={`${objectId}-${CW}`}>Trọng lượng tính cước ({cw.uom === "W/M" ? "W/M" : "kg"})</label>
                    <input
                      id={`${objectId}-${CW}`}
                      inputMode="decimal"
                      placeholder="Chưa xác định"
                      value={draft[CW] ?? ""}
                      disabled={busy || (cwNeedsReason && !view.canOverrideChargeable)}
                      onChange={(ev) => setDraft((d) => ({ ...d, [CW]: ev.target.value }))}
                    />
                    <div className="opref-field-meta">
                      {cwNeedsReason
                        ? view.canOverrideChargeable
                          ? "Sửa số này là ghi đè — cần lý do."
                          : "Bạn không có quyền ghi đè Trọng lượng tính cước."
                        : "Để trống để hệ thống tính khi đủ dữ liệu."}
                    </div>
                  </div>
                ) : null}
              </div>
            </div>
          ))}

          {reasonRequired ? (
            <div className="field field-span">
              <label htmlFor={`${objectId}-reason`}>Lý do ghi đè *</label>
              <input
                id={`${objectId}-reason`}
                value={reason}
                maxLength={500}
                onChange={(ev) => setReason(ev.target.value)}
                placeholder="VD: Khách xác nhận lại số cân theo phiếu cân ngày …"
              />
            </div>
          ) : null}

          <p className="muted small">
            {changes.length === 0
              ? "Chưa có thay đổi."
              : `${changes.length} trường sẽ thay đổi. Trường dùng để tính giá sẽ chuyển lần tính giá liên quan sang “Cần tính giá lại”.`}
          </p>
          <div className="cta-row row-actions">
            <button type="button" className="btn" disabled={busy || changes.length === 0} onClick={onSave}>
              {busy ? "Đang lưu…" : "Lưu thay đổi"}
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              disabled={busy}
              onClick={() => {
                setEditing(false);
                setError(null);
                setReason("");
                setDraft(initialDraft(view));
              }}
            >
              Hủy
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function FieldInput({
  field,
  value,
  commodities,
  disabled,
  onChange,
  onRevert,
}: {
  field: OprefFieldState;
  value: string;
  commodities: { value: string; label: string }[];
  disabled: boolean;
  onChange: (value: string) => void;
  onRevert: () => void;
}) {
  const id = `opref-${field.code}`;
  const locked = !field.editable || disabled;
  let control: ReactNode;
  switch (field.kind) {
    case "transport_mode": {
      const options = TRANSPORT_MODE_OPTIONS.some((o) => o.value === value) || !value
        ? TRANSPORT_MODE_OPTIONS
        : [{ value, label: value }, ...TRANSPORT_MODE_OPTIONS];
      control = (
        <select id={id} value={value} disabled={locked} onChange={(ev) => onChange(ev.target.value)}>
          <option value="">Chưa chọn</option>
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      );
      break;
    }
    case "commodity":
      control = (
        <select id={id} value={value} disabled={locked} onChange={(ev) => onChange(ev.target.value)}>
          <option value="">Chưa chọn loại hàng</option>
          {commodities.map((c) => (
            <option key={c.value} value={c.value}>
              {c.label}
            </option>
          ))}
        </select>
      );
      break;
    case "flags": {
      const selected = new Set(value.split(",").map((s) => s.trim()).filter(Boolean));
      control = (
        <div className="create-checks" id={id}>
          {SPECIAL_FLAGS.map((f) => (
            <label key={f.value}>
              <input
                type="checkbox"
                checked={selected.has(f.value)}
                disabled={locked}
                onChange={(ev) => {
                  const next = new Set(selected);
                  if (ev.target.checked) next.add(f.value);
                  else next.delete(f.value);
                  onChange(Array.from(next).join(","));
                }}
              />
              {f.label}
            </label>
          ))}
        </div>
      );
      break;
    }
    case "datetime":
      control = (
        <input id={id} type="datetime-local" value={value} disabled={locked} onChange={(ev) => onChange(ev.target.value)} />
      );
      break;
    default:
      control = (
        <input
          id={id}
          value={value}
          disabled={locked}
          inputMode={field.kind === "decimal" ? "decimal" : field.kind === "integer" ? "numeric" : undefined}
          placeholder={field.kind === "decimal" || field.kind === "integer" ? "Chưa có" : undefined}
          onChange={(ev) => onChange(ev.target.value)}
        />
      );
  }

  return (
    <div className={field.kind === "flags" || field.code === "cargo_description" ? "field field-span" : "field"}>
      <label htmlFor={id}>
        {field.label}
        {field.ratingRelevant ? <span className="muted small"> · dùng tính giá</span> : null}
      </label>
      {control}
      <div className="opref-field-meta">
        <span className={sourceBadgeClass(field.source)}>{field.sourceLabel}</span>
        {field.requiresReason && field.editable ? <span>Sửa cần lý do</span> : null}
        {!field.editable && field.lockReason ? <span>{field.lockReason}</span> : null}
        {field.source === "override" ? (
          <>
            {field.sourceValue !== null ? <span>Nguồn: {field.sourceValue}</span> : null}
            {field.overrideReason ? <span>Lý do: {field.overrideReason}</span> : null}
            {field.editable ? (
              <button type="button" className="btn btn-sm btn-ghost" disabled={disabled} onClick={onRevert}>
                Bỏ ghi đè
              </button>
            ) : null}
          </>
        ) : null}
      </div>
    </div>
  );
}
