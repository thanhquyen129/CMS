"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import {
  PARTY_CREDIT_MODE_OPTIONS,
  PARTY_KIND_OPTIONS,
  PARTY_LEGAL_TYPE_OPTIONS,
  PARTY_ROLE_OPTIONS,
} from "@/lib/party";
import { findPartyDuplicates, type PartyDuplicateHit } from "@/lib/parties-client";
import { VnAddressFields } from "@/components/VnAddressFields";
import { PartyTypeahead } from "@/components/PartyTypeahead";

export function CreateBusinessPartyForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [duplicates, setDuplicates] = useState<PartyDuplicateHit[]>([]);

  async function checkDuplicates(taxId: string, phone: string, email: string) {
    const hits = await findPartyDuplicates({ taxId, phone, email });
    setDuplicates(hits);
  }

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

    if (!name) {
      setError("Nhập tên đối tác.");
      setSubmitting(false);
      return;
    }

    const paymentRaw = String(fd.get("paymentTermDays") ?? "").trim();
    const creditRaw = String(fd.get("creditLimit") ?? "").trim();
    const province = String(fd.get("province") ?? "").trim() || null;
    const vatRaw = String(fd.get("vatRegistered") ?? "");

    const body = {
      code: code,
      name,
      legalName: String(fd.get("legalName") ?? "").trim() || null,
      shortName: String(fd.get("shortName") ?? "").trim() || null,
      taxId: String(fd.get("taxId") ?? "").trim() || null,
      phone: String(fd.get("phone") ?? "").trim() || null,
      email: String(fd.get("email") ?? "").trim() || null,
      invoiceEmail: String(fd.get("invoiceEmail") ?? "").trim() || null,
      website: String(fd.get("website") ?? "").trim() || null,
      partyKind: String(fd.get("partyKind") ?? "organization"),
      legalType: String(fd.get("legalType") ?? "").trim() || null,
      groupCode: String(fd.get("groupCode") ?? "").trim() || null,
      externalCode: String(fd.get("externalCode") ?? "").trim() || null,
      industryCode: String(fd.get("industryCode") ?? "").trim() || null,
      vatRegistered: vatRaw === "yes" ? true : vatRaw === "no" ? false : null,
      addressLine1: String(fd.get("addressLine1") ?? "").trim() || null,
      addressLine2: String(fd.get("addressLine2") ?? "").trim() || null,
      ward: String(fd.get("ward") ?? "").trim() || null,
      district: String(fd.get("district") ?? "").trim() || null,
      city: province,
      province,
      countryCode:
        String(fd.get("countryCode") ?? "").trim().toUpperCase() || "VN",
      postalCode: String(fd.get("postalCode") ?? "").trim() || null,
      defaultCurrencyCode:
        String(fd.get("defaultCurrencyCode") ?? "").trim().toUpperCase() ||
        "VND",
      paymentTermDays: paymentRaw ? Number(paymentRaw) : null,
      creditLimit: creditRaw ? Number(creditRaw) : null,
      creditLimitCurrencyCode:
        String(fd.get("creditLimitCurrencyCode") ?? "").trim().toUpperCase() ||
        "VND",
      creditControlMode: String(fd.get("creditControlMode") ?? "advisory"),
      parentPartyId: String(fd.get("parentPartyId") ?? "").trim() || null,
      notes: String(fd.get("notes") ?? "").trim() || null,
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
              ? "Mã đối tác, MST hoặc mã đối chiếu đã tồn tại."
              : "Tạo đối tác thất bại.")
        );
        return;
      }

      const created = (await res.json()) as { id?: string };
      if (created.id) {
        startTransition(() => router.push(`/admin/parties/${created.id}`));
      } else {
        startTransition(() => router.push("/admin/parties"));
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
      <fieldset className="group-box">
        <legend>Định danh</legend>
        <p className="muted" style={{ marginTop: 0 }}>
          Để trống mã — hệ thống cấp <span className="mono-id">DT-yymmdd-xxxx</span>.
          MST là khóa duy nhất trong thuê bao.
        </p>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="partyCode">Mã đối tác</label>
            <input
              id="partyCode"
              name="code"
              type="text"
              maxLength={64}
              disabled={busy}
              placeholder="Tự cấp nếu để trống"
            />
          </div>
          <div className="field">
            <label htmlFor="partyName">Tên giao dịch</label>
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
            <label htmlFor="partyShortName">Tên viết tắt</label>
            <input
              id="partyShortName"
              name="shortName"
              type="text"
              maxLength={128}
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
              onBlur={(e) => {
                const form = e.currentTarget.form;
                if (!form) return;
                void checkDuplicates(
                  e.currentTarget.value,
                  String(new FormData(form).get("phone") ?? ""),
                  String(new FormData(form).get("email") ?? "")
                );
              }}
            />
          </div>
          <div className="field">
            <label htmlFor="partyKind">Loại</label>
            <select id="partyKind" name="partyKind" disabled={busy} defaultValue="organization">
              {PARTY_KIND_OPTIONS.map((k) => (
                <option key={k.code} value={k.code}>
                  {k.label}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="partyLegalType">Loại pháp lý</label>
            <select id="partyLegalType" name="legalType" disabled={busy} defaultValue="">
              <option value="">— Chưa chọn —</option>
              {PARTY_LEGAL_TYPE_OPTIONS.map((k) => (
                <option key={k.code} value={k.code}>
                  {k.label}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="partyGroup">Nhóm đối tác</label>
            <input
              id="partyGroup"
              name="groupCode"
              maxLength={64}
              disabled={busy}
              placeholder="VD: VIP, nội địa"
            />
          </div>
          <div className="field">
            <label htmlFor="partyExternal">Mã đối chiếu ngoài</label>
            <input
              id="partyExternal"
              name="externalCode"
              maxLength={64}
              disabled={busy}
              placeholder="Mã TMS / ERP"
            />
          </div>
          <div className="field">
            <label htmlFor="partyIndustry">Ngành hàng</label>
            <input id="partyIndustry" name="industryCode" maxLength={64} disabled={busy} />
          </div>
          <div style={{ gridColumn: "1 / -1" }}>
            <PartyTypeahead
              name="parentPartyId"
              label="Công ty mẹ / nhóm"
              usableOnly={false}
              disabled={busy}
              hint="Không bắt buộc. Dùng khi chi nhánh/công ty con thuộc một pháp nhân khác."
            />
          </div>
        </div>
      </fieldset>

      {duplicates.length > 0 ? (
        <div className="alert alert-warning" role="status">
          Có thể trùng hồ sơ:{" "}
          {duplicates.map((d) => `${d.code} (${d.matchOn})`).join(", ")}. Kiểm tra
          trước khi lưu.
        </div>
      ) : null}

      <fieldset className="group-box">
        <legend>Liên hệ &amp; địa chỉ</legend>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="partyPhone">Điện thoại</label>
            <input id="partyPhone" name="phone" maxLength={64} disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="partyEmail">Email</label>
            <input id="partyEmail" name="email" type="email" maxLength={256} disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="partyInvoiceEmail">Email hóa đơn</label>
            <input
              id="partyInvoiceEmail"
              name="invoiceEmail"
              type="email"
              maxLength={256}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="partyWebsite">Website</label>
            <input id="partyWebsite" name="website" maxLength={256} disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="partyVat">Kê khai VAT</label>
            <select id="partyVat" name="vatRegistered" disabled={busy} defaultValue="">
              <option value="">Chưa rõ</option>
              <option value="yes">Có</option>
              <option value="no">Không</option>
            </select>
          </div>
        </div>
        <VnAddressFields
          disabled={busy}
          defaults={{ countryCode: "VN" }}
        />
      </fieldset>

      <fieldset className="group-box">
        <legend>Thiết lập tài chính</legend>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="partyCurrency">Tiền tệ mặc định</label>
            <input
              id="partyCurrency"
              name="defaultCurrencyCode"
              maxLength={3}
              defaultValue="VND"
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
            <label htmlFor="partyCredit">Hạn mức công nợ (AR)</label>
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
              maxLength={3}
              defaultValue="VND"
              disabled={busy}
            />
          </div>
          <div className="field" style={{ gridColumn: "1 / -1" }}>
            <label htmlFor="partyCreditMode">Chế độ hạn mức</label>
            <select
              id="partyCreditMode"
              name="creditControlMode"
              disabled={busy}
              defaultValue="advisory"
            >
              {PARTY_CREDIT_MODE_OPTIONS.map((m) => (
                <option key={m.code} value={m.code}>
                  {m.label}
                </option>
              ))}
            </select>
          </div>
          <div className="field" style={{ gridColumn: "1 / -1" }}>
            <label htmlFor="partyNotes">Ghi chú nội bộ</label>
            <textarea id="partyNotes" name="notes" rows={3} maxLength={2000} disabled={busy} />
          </div>
        </div>
      </fieldset>

      <fieldset className="group-box">
        <legend>Vai trò</legend>
        <p className="muted" style={{ marginTop: 0 }}>
          Bắt buộc có vai trò phù hợp trước khi gắn lên Bill / chi phí / chứng từ.
        </p>
        <div className="form-grid">
          {PARTY_ROLE_OPTIONS.map((r) => (
            <label key={r.code} className="field checkbox-field">
              <input type="checkbox" name={`role_${r.code}`} disabled={busy} /> {r.label}
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
          {busy ? "Đang lưu…" : "Tạo đối tác"}
        </button>
      </div>
    </form>
  );
}
