"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { LocationItem } from "@/lib/reference-masters-shared";

export function UpsertRouteForm({ locations }: { locations: LocationItem[] }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const active = locations.filter((l) => l.isActive);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const stops = fd.getAll("stops").map(String).filter(Boolean);
    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      originLocationId: String(fd.get("originLocationId") ?? ""),
      destinationLocationId: String(fd.get("destinationLocationId") ?? ""),
      transportModeCode: String(fd.get("transportModeCode") ?? "").trim() || null,
      serviceTypeCode: String(fd.get("serviceTypeCode") ?? "").trim() || null,
      isActive: true,
      intermediateLocationIds: stops,
    };
    if (!body.code || !body.name || !body.originLocationId || !body.destinationLocationId) {
      setError("Nhập mã, tên, điểm đi và điểm đến.");
      setBusy(false);
      return;
    }
    try {
      const res = await fetch("/bff/admin/routes", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được tuyến.");
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

  const disabled = busy || pending || active.length < 2;
  return (
    <form onSubmit={onSubmit} className="stack-form">
      {active.length < 2 ? <p className="note">Cần ít nhất hai địa điểm đang dùng trước khi tạo tuyến.</p> : null}
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      <div className="cw-field">
        <label htmlFor="rt-code">Mã tuyến</label>
        <input id="rt-code" name="code" required disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="rt-name">Tên tuyến</label>
        <input id="rt-name" name="name" required disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="rt-origin">Điểm đi</label>
        <select id="rt-origin" name="originLocationId" required disabled={disabled} defaultValue="">
          <option value="">Chọn</option>
          {active.map((l) => <option key={l.id} value={l.id}>{l.code} — {l.name}</option>)}
        </select>
      </div>
      <div className="cw-field">
        <label htmlFor="rt-dest">Điểm đến</label>
        <select id="rt-dest" name="destinationLocationId" required disabled={disabled} defaultValue="">
          <option value="">Chọn</option>
          {active.map((l) => <option key={`d-${l.id}`} value={l.id}>{l.code} — {l.name}</option>)}
        </select>
      </div>
      <div className="cw-field">
        <label htmlFor="rt-mode">Phương thức</label>
        <input id="rt-mode" name="transportModeCode" disabled={disabled} placeholder="air / sea / road" />
      </div>
      <div className="cw-field">
        <label htmlFor="rt-svc">Dịch vụ</label>
        <input id="rt-svc" name="serviceTypeCode" disabled={disabled} />
      </div>
      <fieldset className="group-box">
        <legend>Điểm trung gian</legend>
        {active.map((l) => (
          <label key={`s-${l.id}`} className="checkbox-field">
            <input type="checkbox" name="stops" value={l.id} disabled={disabled} />
            {l.code} — {l.name}
          </label>
        ))}
      </fieldset>
      <button className="btn btn-primary" type="submit" disabled={disabled}>Lưu tuyến</button>
    </form>
  );
}
