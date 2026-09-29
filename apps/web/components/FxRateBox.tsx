"use client";

import { useEffect, useState } from "react";
import { formatMoney } from "@/lib/money";

type Preview = {
  currencyCode: string;
  reportingCurrencyCode: string;
  sameCurrency: boolean;
  rate: number | null;
  sourceName: string | null;
  rateDate: string | null;
  reportingAmount: number | null;
  rateAvailable: boolean;
  canOverride: boolean;
  message: string | null;
};

export function readFx(fd: FormData): { fxRate: number | null; fxOverrideReason: string | null } {
  const raw = String(fd.get("fxRate") ?? "").trim().replace(",", ".");
  const n = raw ? Number(raw) : NaN;
  const reason = String(fd.get("fxOverrideReason") ?? "").trim() || null;
  return {
    fxRate: Number.isFinite(n) && n > 0 ? n : null,
    fxOverrideReason: reason,
  };
}

export function FxRateBox({
  currency,
  amount,
  asOf,
  disabled,
}: {
  currency: string;
  amount: string;
  asOf: string;
  disabled?: boolean;
}) {
  const [preview, setPreview] = useState<Preview | null>(null);

  useEffect(() => {
    const code = currency.trim().toUpperCase();
    if (code.length !== 3) {
      setPreview(null);
      return;
    }
    const q = new URLSearchParams({ currencyCode: code });
    if (asOf) q.set("asOf", asOf);
    const n = Number(amount.replace(",", "."));
    if (Number.isFinite(n)) q.set("amount", String(n));
    let cancelled = false;
    void fetch(`/bff/fx-rates/preview?${q}`, { cache: "no-store" })
      .then(async (res) => (res.ok ? ((await res.json()) as Preview) : null))
      .then((row) => {
        if (!cancelled) setPreview(row);
      })
      .catch(() => {
        if (!cancelled) setPreview(null);
      });
    return () => {
      cancelled = true;
    };
  }, [currency, amount, asOf]);

  if (!preview || preview.sameCurrency) return null;

  return (
    <fieldset className="group-box">
      <legend>Tỷ giá sang {preview.reportingCurrencyCode}</legend>
      {preview.rateAvailable ? (
        <p className="note">
          Tỷ giá {preview.rate} · {preview.sourceName ?? "Sổ tỷ giá"}
          {preview.rateDate ? ` · ${preview.rateDate}` : ""}
          {preview.reportingAmount != null
            ? ` · Số báo cáo ${formatMoney(preview.reportingAmount, preview.reportingCurrencyCode)}`
            : ""}
        </p>
      ) : (
        <div className="alert alert-error" role="alert">
          {preview.message ?? "Chưa có tỷ giá. Không ghi số tiền khi thiếu tỷ giá."}
        </div>
      )}
      {preview.canOverride ? (
        <div className="form-grid">
          <div className="field">
            <label htmlFor="fxRate">Tỷ giá nhập tay</label>
            <input
              id="fxRate"
              name="fxRate"
              type="number"
              inputMode="decimal"
              min={0}
              step="any"
              disabled={disabled}
              placeholder={preview.rate != null ? String(preview.rate) : ""}
            />
          </div>
          <div className="field">
            <label htmlFor="fxOverrideReason">Lý do ghi đè</label>
            <input id="fxOverrideReason" name="fxOverrideReason" maxLength={512} disabled={disabled} />
          </div>
        </div>
      ) : null}
    </fieldset>
  );
}
