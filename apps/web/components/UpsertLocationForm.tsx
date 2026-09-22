"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { LOCATION_TYPE_OPTIONS } from "@/lib/reference-masters";

export function UpsertLocationForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const aliases = String(fd.get("aliases") ?? "")
      .split(/[,;\n]/)
      .map((s) => s.trim())
      .filter(Boolean)
      .map((aliasCode) => ({ aliasCode, sourceSystem: null }));
    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      locationType: String(fd.get("locationType") ?? "other"),
      countryCode: String(fd.get("countryCode") ?? "").trim() || null,
      subdivision: String(fd.get("subdivision") ?? "").trim() || null,
      city: String(fd.get("city") ?? "").trim() || null,
      iataCode: String(fd.get("iataCode") ?? "").trim() || null,
      unlocode: String(fd.get("unlocode") ?? "").trim() || null,
      terminalCode: String(fd.get("terminalCode") ?? "").trim() || null,
      isActive: fd.get("isActive") === "on",
      aliases,
    };
    if (!body.code || !body.name) {
      setError("Nhập mã và tên địa điểm.");
      setBusy(false);
      return;
    }
    try {
      const res = await fetch("/bff/admin/locations", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được địa điểm.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const disabled = busy || pending;
  return (
    <form onSubmit={onSubmit} className="stack-form">
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      <div className="cw-field">
        <label htmlFor="loc-code">Mã <span className="req">*</span></label>
        <input id="loc-code" name="code" maxLength={30} required disabled={disabled} placeholder="SGN" />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-name">Tên <span className="req">*</span></label>
        <input id="loc-name" name="name" maxLength={256} required disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-type">Loại</label>
        <select id="loc-type" name="locationType" disabled={disabled} defaultValue="airport">
          {LOCATION_TYPE_OPTIONS.map((t) => <option key={t.id} value={t.id}>{t.label}</option>)}
        </select>
      </div>
      <div className="cw-field">
        <label htmlFor="loc-country">Quốc gia (ISO)</label>
        <input id="loc-country" name="countryCode" maxLength={2} disabled={disabled} placeholder="VN" />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-sub">Tỉnh / bang</label>
        <input id="loc-sub" name="subdivision" disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-city">Thành phố</label>
        <input id="loc-city" name="city" disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-iata">IATA</label>
        <input id="loc-iata" name="iataCode" maxLength={3} disabled={disabled} placeholder="SGN" />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-un">UN/LOCODE</label>
        <input id="loc-un" name="unlocode" maxLength={5} disabled={disabled} placeholder="VNSGN" />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-term">Mã nhà ga / cầu cảng</label>
        <input id="loc-term" name="terminalCode" disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="loc-alias">Alias nguồn ngoài</label>
        <textarea id="loc-alias" name="aliases" disabled={disabled} placeholder="Mỗi mã một dòng, hoặc cách nhau bởi dấu phẩy" />
      </div>
      <label className="checkbox-field">
        <input type="checkbox" name="isActive" defaultChecked disabled={disabled} /> Đang dùng
      </label>
      <button className="btn btn-primary" type="submit" disabled={disabled}>Lưu địa điểm</button>
    </form>
  );
}
