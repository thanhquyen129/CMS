"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { CommodityItem } from "@/lib/reference-masters-shared";

const FLAGS = [
  ["isDangerousGoods", "Hàng nguy hiểm (DG)"],
  ["isTemperatureControlled", "Hàng lạnh"],
  ["isOversize", "Quá khổ"],
  ["isOverweight", "Quá tải"],
  ["isHighValue", "Giá trị cao"],
] as const;

export function UpsertCommodityForm({ parents }: { parents: CommodityItem[] }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      category: String(fd.get("category") ?? "").trim() || null,
      parentId: String(fd.get("parentId") ?? "").trim() || null,
      specialHandling: String(fd.get("specialHandling") ?? "").trim() || null,
      isActive: true,
      isDangerousGoods: fd.get("isDangerousGoods") === "on",
      isTemperatureControlled: fd.get("isTemperatureControlled") === "on",
      isOversize: fd.get("isOversize") === "on",
      isOverweight: fd.get("isOverweight") === "on",
      isHighValue: fd.get("isHighValue") === "on",
    };
    try {
      const res = await fetch("/bff/admin/commodities", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được loại hàng.");
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
        <label htmlFor="cm-code">Mã</label>
        <input id="cm-code" name="code" required disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="cm-name">Tên</label>
        <input id="cm-name" name="name" required disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="cm-cat">Nhóm</label>
        <input id="cm-cat" name="category" disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="cm-parent">Loại hàng cha</label>
        <select id="cm-parent" name="parentId" disabled={disabled} defaultValue="">
          <option value="">Không</option>
          {parents.filter((p) => p.isActive).map((p) => (
            <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
          ))}
        </select>
      </div>
      <div className="create-checks">
        {FLAGS.map(([name, label]) => (
          <label key={name}>
            <input type="checkbox" name={name} disabled={disabled} /> {label}
          </label>
        ))}
      </div>
      <div className="cw-field">
        <label htmlFor="cm-note">Xử lý đặc biệt</label>
        <input id="cm-note" name="specialHandling" disabled={disabled} />
      </div>
      <button className="btn btn-primary" type="submit" disabled={disabled}>Lưu loại hàng</button>
    </form>
  );
}
