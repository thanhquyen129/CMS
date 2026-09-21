"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { CurrencyItem } from "@/lib/master-data";
import {
  DATE_FORMATS,
  TIME_ZONES,
  type TenantProfile,
} from "@/lib/tenant-admin-model";

type Props = {
  profile: TenantProfile;
  currencies: CurrencyItem[];
};

export function CompanyProfileForm({ profile, currencies }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const body = {
      name: String(fd.get("name") ?? "").trim(),
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
      countryCode: String(fd.get("countryCode") ?? "").trim() || null,
      postalCode: String(fd.get("postalCode") ?? "").trim() || null,
      timeZoneId: String(fd.get("timeZoneId") ?? "").trim(),
      dateFormat: String(fd.get("dateFormat") ?? "").trim(),
      defaultCurrencyCode: String(fd.get("defaultCurrencyCode") ?? "").trim(),
    };
    if (!body.name) {
      setError("Tên doanh nghiệp không được để trống.");
      setBusy(false);
      return;
    }
    try {
      const res = await fetch("/bff/tenant-profile", {
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
        setError(payload.message || "Không lưu được thông tin doanh nghiệp.");
        return;
      }
      setInfo("Đã lưu. Múi giờ chỉ dùng để hiển thị; số liệu vẫn lưu UTC.");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? (
        <div className="alert alert-success" role="status">
          {info}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor="coCode">Mã thuê bao</label>
          <input id="coCode" value={profile.code} readOnly disabled />
        </div>
        <div className="field">
          <label htmlFor="coName">Tên giao dịch</label>
          <input
            id="coName"
            name="name"
            required
            maxLength={256}
            defaultValue={profile.name}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coLegal">Tên pháp lý</label>
          <input
            id="coLegal"
            name="legalName"
            maxLength={256}
            defaultValue={profile.legalName ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coTax">Mã số thuế</label>
          <input
            id="coTax"
            name="taxId"
            maxLength={32}
            defaultValue={profile.taxId ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coPhone">Điện thoại</label>
          <input
            id="coPhone"
            name="phone"
            maxLength={64}
            defaultValue={profile.phone ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coEmail">Email</label>
          <input
            id="coEmail"
            name="email"
            type="email"
            maxLength={256}
            defaultValue={profile.email ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coWeb">Website</label>
          <input
            id="coWeb"
            name="website"
            maxLength={256}
            defaultValue={profile.website ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coAddr1">Địa chỉ</label>
          <input
            id="coAddr1"
            name="addressLine1"
            maxLength={256}
            defaultValue={profile.addressLine1 ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coAddr2">Địa chỉ dòng 2</label>
          <input
            id="coAddr2"
            name="addressLine2"
            maxLength={256}
            defaultValue={profile.addressLine2 ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coWard">Phường / xã</label>
          <input
            id="coWard"
            name="ward"
            maxLength={128}
            defaultValue={profile.ward ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coDistrict">Quận / huyện</label>
          <input
            id="coDistrict"
            name="district"
            maxLength={128}
            defaultValue={profile.district ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coCity">Thành phố</label>
          <input
            id="coCity"
            name="city"
            maxLength={128}
            defaultValue={profile.city ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coProvince">Tỉnh</label>
          <input
            id="coProvince"
            name="province"
            maxLength={128}
            defaultValue={profile.province ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coCountry">Quốc gia (ISO 2)</label>
          <input
            id="coCountry"
            name="countryCode"
            maxLength={2}
            defaultValue={profile.countryCode ?? "VN"}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coPostal">Mã bưu chính</label>
          <input
            id="coPostal"
            name="postalCode"
            maxLength={32}
            defaultValue={profile.postalCode ?? ""}
            disabled={waiting}
          />
        </div>
        <div className="field">
          <label htmlFor="coTz">Múi giờ hiển thị</label>
          <select
            id="coTz"
            name="timeZoneId"
            defaultValue={profile.timeZoneId}
            disabled={waiting}
          >
            {TIME_ZONES.map((z) => (
              <option key={z.id} value={z.id}>
                {z.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="coDate">Định dạng ngày</label>
          <select
            id="coDate"
            name="dateFormat"
            defaultValue={profile.dateFormat}
            disabled={waiting}
          >
            {DATE_FORMATS.map((d) => (
              <option key={d.id} value={d.id}>
                {d.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="coCcy">Tiền tệ mặc định</label>
          <select
            id="coCcy"
            name="defaultCurrencyCode"
            defaultValue={profile.defaultCurrencyCode}
            disabled={waiting}
          >
            {(currencies.length
              ? currencies
              : [
                  {
                    id: "vnd",
                    code: "VND",
                    name: "Việt Nam đồng",
                    decimalPlaces: 0,
                    isActive: true,
                  },
                ]
            ).map((c) => (
                <option key={c.code} value={c.code}>
                  {c.code} — {c.name}
                </option>
              )
            )}
          </select>
        </div>
      </div>
      <p className="note">
        Múi giờ và định dạng ngày chỉ để hiển thị trên chứng từ / báo cáo. Sổ tiền và audit luôn
        lưu UTC.
      </p>
      <div className="cta-row">
        <button type="submit" className="btn" disabled={waiting}>
          {waiting ? "Đang lưu…" : "Lưu thông tin doanh nghiệp"}
        </button>
      </div>
    </form>
  );
}
