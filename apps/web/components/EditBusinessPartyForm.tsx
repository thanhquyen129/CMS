"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { BusinessParty } from "@/lib/party";

export function EditBusinessPartyForm({ party }: { party: BusinessParty }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setOk(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const name = String(fd.get("name") ?? "").trim();
    if (!name) {
      setError("Tên đối tác không được để trống.");
      setSubmitting(false);
      return;
    }

    const paymentRaw = String(fd.get("paymentTermDays") ?? "").trim();
    const creditRaw = String(fd.get("creditLimit") ?? "").trim();

    const body = {
      name,
      isActive: fd.get("isActive") === "on",
      legalName: String(fd.get("legalName") ?? "").trim() || null,
      taxId: String(fd.get("taxId") ?? "").trim() || null,
      phone: String(fd.get("phone") ?? "").trim() || null,
      email: String(fd.get("email") ?? "").trim() || null,
      website: String(fd.get("website") ?? "").trim() || null,
      addressLine1: String(fd.get("addressLine1") ?? "").trim() || null,
      addressLine2: String(fd.get("addressLine2") ?? "").trim() || null,
      ward: String(fd.get("ward") ?? "").trim() || null,
      district: String(fd.get("district") ?? "").trim() || null,
      city: String(fd.get("city") ?? "").trim() || null,
      province: String(fd.get("province") ?? "").trim() || null,
      countryCode:
        String(fd.get("countryCode") ?? "").trim().toUpperCase() || null,
      postalCode: String(fd.get("postalCode") ?? "").trim() || null,
      defaultCurrencyCode:
        String(fd.get("defaultCurrencyCode") ?? "").trim().toUpperCase() ||
        null,
      paymentTermDays: paymentRaw ? Number(paymentRaw) : null,
      creditLimit: creditRaw ? Number(creditRaw) : null,
      creditLimitCurrencyCode:
        String(fd.get("creditLimitCurrencyCode") ?? "").trim().toUpperCase() ||
        null,
      notes: String(fd.get("notes") ?? "").trim() || null,
    };

    try {
      const res = await fetch(`/bff/admin/parties/${party.id}`, {
        method: "PUT",
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
        setError(payload.message || "Cập nhật đối tác thất bại.");
        return;
      }

      setOk("Đã lưu hồ sơ đối tác.");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  async function onDelete() {
    if (
      !window.confirm(
        `Ngừng và xóa mềm đối tác ${party.code}? Thao tác không xóa lịch sử chứng từ.`
      )
    ) {
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(`/bff/admin/parties/${party.id}`, {
        method: "DELETE",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Xóa đối tác thất bại.");
        return;
      }
      startTransition(() => router.push("/admin/parties"));
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
        <div className="form-grid">
          <div className="field">
            <label>Mã đối tác</label>
            <input type="text" value={party.code} disabled readOnly />
          </div>
          <div className="field">
            <label htmlFor="editName">Tên đối tác</label>
            <input
              id="editName"
              name="name"
              type="text"
              required
              maxLength={256}
              defaultValue={party.name}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editLegalName">Tên pháp lý</label>
            <input
              id="editLegalName"
              name="legalName"
              type="text"
              maxLength={256}
              defaultValue={party.legalName ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editTaxId">Mã số thuế</label>
            <input
              id="editTaxId"
              name="taxId"
              type="text"
              maxLength={32}
              defaultValue={party.taxId ?? ""}
              disabled={busy}
            />
          </div>
          <label className="field checkbox-field">
            <input
              type="checkbox"
              name="isActive"
              defaultChecked={party.isActive}
              disabled={busy}
            />{" "}
            Đang dùng
          </label>
        </div>
      </fieldset>

      <fieldset className="group-box">
        <legend>Liên hệ & địa chỉ</legend>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="editPhone">Điện thoại</label>
            <input
              id="editPhone"
              name="phone"
              defaultValue={party.phone ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editEmail">Email</label>
            <input
              id="editEmail"
              name="email"
              type="email"
              defaultValue={party.email ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editWebsite">Website</label>
            <input
              id="editWebsite"
              name="website"
              defaultValue={party.website ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editAddr1">Địa chỉ dòng 1</label>
            <input
              id="editAddr1"
              name="addressLine1"
              defaultValue={party.addressLine1 ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editAddr2">Địa chỉ dòng 2</label>
            <input
              id="editAddr2"
              name="addressLine2"
              defaultValue={party.addressLine2 ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editWard">Phường/Xã</label>
            <input
              id="editWard"
              name="ward"
              defaultValue={party.ward ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editDistrict">Quận/Huyện</label>
            <input
              id="editDistrict"
              name="district"
              defaultValue={party.district ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editCity">Thành phố</label>
            <input
              id="editCity"
              name="city"
              defaultValue={party.city ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editProvince">Tỉnh/TP</label>
            <input
              id="editProvince"
              name="province"
              defaultValue={party.province ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editCountry">Quốc gia</label>
            <input
              id="editCountry"
              name="countryCode"
              maxLength={2}
              defaultValue={party.countryCode ?? "VN"}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editPostal">Mã bưu chính</label>
            <input
              id="editPostal"
              name="postalCode"
              defaultValue={party.postalCode ?? ""}
              disabled={busy}
            />
          </div>
        </div>
      </fieldset>

      <fieldset className="group-box">
        <legend>Thiết lập tài chính</legend>
        <p className="muted" style={{ marginTop: 0 }}>
          Hạn mức công nợ mang tính tham chiếu trên hồ sơ — chưa chặn chứng từ.
        </p>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="editCur">Tiền tệ mặc định</label>
            <input
              id="editCur"
              name="defaultCurrencyCode"
              maxLength={3}
              defaultValue={party.defaultCurrencyCode ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editTerms">Điều khoản TT (ngày)</label>
            <input
              id="editTerms"
              name="paymentTermDays"
              type="number"
              min={0}
              max={3650}
              defaultValue={party.paymentTermDays ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editCredit">Hạn mức công nợ</label>
            <input
              id="editCredit"
              name="creditLimit"
              type="number"
              min={0}
              step="1"
              defaultValue={party.creditLimit ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="editCreditCur">Tiền tệ hạn mức</label>
            <input
              id="editCreditCur"
              name="creditLimitCurrencyCode"
              maxLength={3}
              defaultValue={party.creditLimitCurrencyCode ?? ""}
              disabled={busy}
            />
          </div>
          <div className="field" style={{ gridColumn: "1 / -1" }}>
            <label htmlFor="editNotes">Ghi chú</label>
            <textarea
              id="editNotes"
              name="notes"
              rows={3}
              maxLength={2000}
              defaultValue={party.notes ?? ""}
              disabled={busy}
            />
          </div>
        </div>
      </fieldset>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {ok ? (
        <div className="alert alert-success" role="status">
          {ok}
        </div>
      ) : null}

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang lưu…" : "Lưu hồ sơ"}
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={busy}
          onClick={onDelete}
        >
          Xóa mềm
        </button>
      </div>
    </form>
  );
}
