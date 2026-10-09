"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { PartyTypeahead } from "@/components/PartyTypeahead";

type CardOption = { id: string; label: string };

export type SurchargeFormValues = {
  code?: string;
  name?: string;
  direction?: string;
  calculationMode?: string;
  basis?: string | null;
  currencyCode?: string;
  rateAmountPercent?: number;
  transportMode?: string | null;
  routeCode?: string | null;
  dangerousGoods?: boolean | null;
  rateCardId?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  vatRate?: number | null;
  vendorPartyId?: string | null;
  customerPartyId?: string | null;
  customerGroupCode?: string | null;
};

export function SurchargeForm({
  cards,
  initial,
  surchargeId,
  versionId,
  lockCode,
}: {
  cards: CardOption[];
  initial?: SurchargeFormValues;
  surchargeId?: string;
  versionId?: string;
  lockCode?: boolean;
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const editing = Boolean(surchargeId && versionId);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const amount = Number(String(fd.get("rateAmountPercent") ?? "").replace(",", "."));
    if (!Number.isFinite(amount) || amount < 0) {
      setError("Đơn giá, số tiền hoặc phần trăm không hợp lệ.");
      setBusy(false);
      return;
    }
    const dg = String(fd.get("dangerousGoods") ?? "");
    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      direction: String(fd.get("direction") ?? "buy"),
      calculationMode: String(fd.get("calculationMode") ?? "unit_rate"),
      basis: String(fd.get("basis") ?? "").trim() || null,
      currencyCode: String(fd.get("currencyCode") ?? "VND").trim().toUpperCase(),
      rateAmountPercent: amount,
      transportMode: String(fd.get("transportMode") ?? "").trim() || null,
      routeCode: String(fd.get("routeCode") ?? "").trim() || null,
      dangerousGoods: dg === "" ? null : dg === "true",
      rateCardId: String(fd.get("rateCardId") ?? "").trim() || null,
      validFrom: toOffset(fd.get("validFrom")),
      validTo: toOffset(fd.get("validTo")),
      publish: fd.get("publish") === "on",
      vatRate: parseVat(fd.get("vatRate")),
      vendorPartyId: String(fd.get("vendorPartyId") ?? "").trim() || null,
      customerPartyId: String(fd.get("customerPartyId") ?? "").trim() || null,
      customerGroupCode: String(fd.get("customerGroupCode") ?? "").trim() || null,
    };
    try {
      const url = editing
        ? `/bff/surcharges/${surchargeId}/versions/${versionId}`
        : "/bff/surcharges";
      const res = await fetch(url, {
        method: editing ? "PUT" : "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      const payload = (await res.json().catch(() => ({}))) as { message?: string; surchargeId?: string };
      if (!res.ok) {
        setError(payload.message || "Không lưu được phụ phí.");
        return;
      }
      router.push(`/rate-cards/surcharges/${payload.surchargeId || surchargeId}`);
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const dgValue = initial?.dangerousGoods == null ? "" : initial.dangerousGoods ? "true" : "false";

  return (
    <form onSubmit={onSubmit}>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="code">Mã <span className="req">*</span></label>
          <input id="code" name="code" required maxLength={64} defaultValue={initial?.code ?? ""} readOnly={lockCode} />
        </div>
        <div className="field">
          <label htmlFor="name">Tên <span className="req">*</span></label>
          <input id="name" name="name" required maxLength={256} defaultValue={initial?.name ?? ""} />
        </div>
        <div className="field">
          <label htmlFor="direction">Chiều <span className="req">*</span></label>
          <select id="direction" name="direction" defaultValue={initial?.direction ?? "buy"}>
            <option value="buy">Mua — chi phí dự kiến</option>
            <option value="sell">Bán — doanh thu dự kiến</option>
            <option value="both">Cả hai — khi chính sách cho phép</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="transportMode">Phương thức</label>
          <select id="transportMode" name="transportMode" defaultValue={initial?.transportMode ?? ""}>
            <option value="">Không giới hạn</option>
            <option value="air">Air</option>
            <option value="sea">Sea</option>
            <option value="road">Road</option>
            <option value="rail">Rail</option>
            <option value="express">Express</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="calculationMode">Cách tính <span className="req">*</span></label>
          <select id="calculationMode" name="calculationMode" defaultValue={initial?.calculationMode ?? "unit_rate"}>
            <option value="unit_rate">Đơn giá × số lượng</option>
            <option value="fixed_rate">Số tiền cố định</option>
            <option value="container_rate">Theo container</option>
            <option value="weight_break">Bậc trọng lượng</option>
            <option value="percentage">Phần trăm cước chính</option>
            <option value="composite">Công thức có sàn/trần</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="basis">Cơ sở tính</label>
          <select id="basis" name="basis" defaultValue={initial?.basis ?? "chargeable_weight"}>
            <option value="chargeable_weight">Trọng lượng tính cước</option>
            <option value="gross_weight">Trọng lượng thực</option>
            <option value="quantity">Số lượng</option>
            <option value="bill">Bill</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="rateAmountPercent">Đơn giá / số tiền / % <span className="req">*</span></label>
          <input id="rateAmountPercent" name="rateAmountPercent" inputMode="decimal" required defaultValue={initial?.rateAmountPercent ?? ""} />
        </div>
        <div className="field">
          <label htmlFor="currencyCode">Tiền tệ <span className="req">*</span></label>
          <input id="currencyCode" name="currencyCode" required maxLength={3} defaultValue={initial?.currencyCode ?? "VND"} />
        </div>
        <div className="field">
          <label htmlFor="routeCode">Tuyến</label>
          <input id="routeCode" name="routeCode" maxLength={64} defaultValue={initial?.routeCode ?? ""} placeholder="Để trống = mọi tuyến" />
        </div>
        <div className="field">
          <label htmlFor="dangerousGoods">Hàng nguy hiểm</label>
          <select id="dangerousGoods" name="dangerousGoods" defaultValue={dgValue}>
            <option value="">Không giới hạn</option>
            <option value="true">Chỉ hàng nguy hiểm</option>
            <option value="false">Chỉ hàng thường</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="rateCardId">Bảng giá</label>
          <select id="rateCardId" name="rateCardId" defaultValue={initial?.rateCardId ?? ""}>
            <option value="">Mọi bảng giá phù hợp</option>
            {cards.map((card) => (
              <option key={card.id} value={card.id}>{card.label}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="vatRate">Thuế suất VAT (%)</label>
          <input id="vatRate" name="vatRate" inputMode="decimal" placeholder="Để trống nếu chưa khai báo" defaultValue={initial?.vatRate ?? ""} />
        </div>
        <div className="field">
          <PartyTypeahead name="vendorPartyId" label="Nhà cung cấp (mua)" roleCode="vendor" defaultId={initial?.vendorPartyId} hint="Chỉ dùng cho chiều mua. Để trống nếu giá chung." />
        </div>
        <div className="field">
          <PartyTypeahead name="customerPartyId" label="Khách hàng (bán)" roleCode="customer" defaultId={initial?.customerPartyId} hint="Chỉ dùng cho chiều bán. Không chọn cùng nhóm khách hàng." />
        </div>
        <div className="field">
          <label htmlFor="customerGroupCode">Nhóm khách hàng</label>
          <input id="customerGroupCode" name="customerGroupCode" maxLength={64} defaultValue={initial?.customerGroupCode ?? ""} placeholder="Mã nhóm trên đối tác" />
        </div>
        <div className="field">
          <label htmlFor="validFrom">Hiệu lực từ</label>
          <input id="validFrom" name="validFrom" type="date" defaultValue={dateInput(initial?.validFrom)} />
        </div>
        <div className="field">
          <label htmlFor="validTo">Hiệu lực đến</label>
          <input id="validTo" name="validTo" type="date" defaultValue={dateInput(initial?.validTo)} />
        </div>
      </div>
      {editing ? null : (
        <label className="checkbox-field">
          <input type="checkbox" name="publish" /> Phát hành ngay
        </label>
      )}
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      <div className="cta-row">
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? "Đang lưu…" : editing ? "Lưu phiên bản nháp" : "Tạo phụ phí"}
        </button>
      </div>
    </form>
  );
}

function parseVat(value: FormDataEntryValue | null) {
  const raw = String(value ?? "").trim().replace(",", ".");
  if (!raw) return null;
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

function dateInput(value?: string | null): string {
  if (!value) return "";
  return value.slice(0, 10);
}

function toOffset(value: FormDataEntryValue | null): string | null {
  const text = String(value ?? "").trim();
  if (!text) return null;
  return text.length === 10 ? `${text}T00:00:00Z` : text;
}
