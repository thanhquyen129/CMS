"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { PARTY_ROLE_OPTIONS } from "@/lib/party";

export function CreateBusinessPartyForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const code = String(fd.get("code") ?? "").trim();
    const name = String(fd.get("name") ?? "").trim();
    const roleCodes = PARTY_ROLE_OPTIONS.map((r) => r.code).filter(
      (c) => fd.get(`role_${c}`) === "on"
    );

    if (!code || !name) {
      setError("Nhập mã và tên đối tác.");
      setSubmitting(false);
      return;
    }

    const paymentRaw = String(fd.get("paymentTermDays") ?? "").trim();
    const creditRaw = String(fd.get("creditLimit") ?? "").trim();

    const body = {
      code,
      name,
      legalName: String(fd.get("legalName") ?? "").trim() || null,
      taxId: String(fd.get("taxId") ?? "").trim() || null,
      phone: String(fd.get("phone") ?? "").trim() || null,
      email: String(fd.get("email") ?? "").trim() || null,
      defaultCurrencyCode:
        String(fd.get("defaultCurrencyCode") ?? "").trim().toUpperCase() ||
        null,
      paymentTermDays: paymentRaw ? Number(paymentRaw) : null,
      creditLimit: creditRaw ? Number(creditRaw) : null,
      creditLimitCurrencyCode:
        String(fd.get("creditLimitCurrencyCode") ?? "").trim().toUpperCase() ||
        null,
      countryCode:
        String(fd.get("countryCode") ?? "").trim().toUpperCase() || null,
      roleCodes,
    };

    try {
      const res = await fetch("/bff/admin/parties", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          payload.message ||
            (res.status === 409
              ? "Mã đối tác hoặc MST đã tồn tại."
              : "Tạo đối tác thất bại.")
        );
        return;
      }

      const created = (await res.json()) as { id?: string };
      if (created.id) {
        startTransition(() => router.push(`/admin/parties/${created.id}`));
      } else {
        startTransition(() => router.refresh());
      }
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="partyCode">Mã đối tác</label>
          <input
            id="partyCode"
            name="code"
            type="text"
            required
            maxLength={64}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyName">Tên đối tác</label>
          <input
            id="partyName"
            name="name"
            type="text"
            required
            maxLength={256}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyLegalName">Tên pháp lý</label>
          <input
            id="partyLegalName"
            name="legalName"
            type="text"
            maxLength={256}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyTaxId">Mã số thuế</label>
          <input
            id="partyTaxId"
            name="taxId"
            type="text"
            maxLength={32}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyPhone">Điện thoại</label>
          <input
            id="partyPhone"
            name="phone"
            type="text"
            maxLength={64}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyEmail">Email</label>
          <input
            id="partyEmail"
            name="email"
            type="email"
            maxLength={256}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyCurrency">Tiền tệ mặc định</label>
          <input
            id="partyCurrency"
            name="defaultCurrencyCode"
            type="text"
            maxLength={3}
            placeholder="VND"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyTerms">Điều khoản TT (ngày)</label>
          <input
            id="partyTerms"
            name="paymentTermDays"
            type="number"
            min={0}
            max={3650}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyCredit">Hạn mức công nợ</label>
          <input
            id="partyCredit"
            name="creditLimit"
            type="number"
            min={0}
            step="1"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyCreditCur">Tiền tệ hạn mức</label>
          <input
            id="partyCreditCur"
            name="creditLimitCurrencyCode"
            type="text"
            maxLength={3}
            placeholder="VND"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyCountry">Quốc gia</label>
          <input
            id="partyCountry"
            name="countryCode"
            type="text"
            maxLength={2}
            placeholder="VN"
            defaultValue="VN"
            disabled={busy}
          />
        </div>
      </div>

      <fieldset className="group-box" style={{ marginTop: "0.75rem" }}>
        <legend>Vai trò</legend>
        <div className="form-grid">
          {PARTY_ROLE_OPTIONS.map((r) => (
            <label key={r.code} className="field checkbox-field">
              <input
                type="checkbox"
                name={`role_${r.code}`}
                disabled={busy}
              />{" "}
              {r.label}
            </label>
          ))}
        </div>
      </fieldset>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang lưu…" : "Thêm đối tác"}
        </button>
      </div>
    </form>
  );
}
