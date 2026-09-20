"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { PartyTypeahead } from "@/components/PartyTypeahead";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  defaultBillType?: string;
};

export function CreateBillForm({
  terms,
  defaultBillType = "freight",
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const billLabel = term(terms, "BILL", "Bill");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const billNo = String(fd.get("billNo") ?? "").trim();
    const billType = String(fd.get("billType") ?? "").trim();
    if (!billNo) {
      setError(`Nhập số ${billLabel}.`);
      setSubmitting(false);
      return;
    }
    if (!billType) {
      setError("Nhập loại Bill.");
      setSubmitting(false);
      return;
    }

    const body = {
      billNo,
      billType,
      sourceSystem: String(fd.get("sourceSystem") ?? "").trim() || null,
      externalId: String(fd.get("externalId") ?? "").trim() || null,
      customerPartyId: String(fd.get("customerPartyId") ?? "").trim() || null,
    };

    try {
      const res = await fetch("/bff/bills", {
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
            (res.status === 403
              ? `Bạn không có quyền tạo ${billLabel}.`
              : res.status === 409
                ? `Số ${billLabel} đã tồn tại trong thuê bao.`
                : `Tạo ${billLabel} thất bại.`)
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      if (created.id) {
        startTransition(() => router.push(`/bills/${created.id}`));
      } else {
        startTransition(() => router.push("/bills"));
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        {billLabel} là neo tài chính. Tạo xong mới gắn {term(terms, "COST", "Chi phí")} /{" "}
        {term(terms, "REVENUE", "Doanh thu")}.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-sections cols-2">
        <fieldset className="group-box">
          <legend>Định danh</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="billNo">Số {billLabel}</label>
              <input
                id="billNo"
                name="billNo"
                required
                maxLength={64}
                disabled={busy}
                autoComplete="off"
                placeholder="VD: BL-2026-001"
              />
            </div>
            <div className="field">
              <label htmlFor="billType">Loại</label>
              <input
                id="billType"
                name="billType"
                required
                maxLength={64}
                defaultValue={defaultBillType}
                disabled={busy}
                autoComplete="off"
              />
            </div>
          </div>
        </fieldset>
        <fieldset className="group-box">
          <legend>Nguồn (tuỳ chọn)</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="sourceSystem">Hệ thống nguồn</label>
              <input
                id="sourceSystem"
                name="sourceSystem"
                maxLength={64}
                disabled={busy}
                autoComplete="off"
              />
            </div>
            <div className="field">
              <label htmlFor="externalId">Mã ngoài</label>
              <input
                id="externalId"
                name="externalId"
                maxLength={128}
                disabled={busy}
                autoComplete="off"
              />
            </div>
            <PartyTypeahead
              name="customerPartyId"
              label="Khách hàng"
              roleCode="customer"
              disabled={busy}
              hint="Tìm theo mã, tên, MST hoặc SĐT. Bắt buộc vai trò khách hàng."
            />
          </div>
        </fieldset>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang tạo…" : `Tạo ${billLabel}`}
        </button>
      </div>
    </form>
  );
}
