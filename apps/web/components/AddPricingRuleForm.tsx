"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  versionId: string;
  defaultCurrency: string;
};

export function AddPricingRuleForm({ versionId, defaultCurrency }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const unitRaw = String(fd.get("unitAmount") ?? "").trim();
    const unitAmount = Number(unitRaw.replace(",", "."));
    if (!Number.isFinite(unitAmount) || unitAmount < 0) {
      setError("Số tiền / đơn giá không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      calcMethod: String(fd.get("calcMethod") ?? "fixed").trim(),
      unitAmount,
      currencyCode: String(fd.get("currencyCode") ?? defaultCurrency)
        .trim()
        .toUpperCase(),
      applicability: null,
      serviceTypeCode: String(fd.get("serviceTypeCode") ?? "").trim() || null,
      partyTypeCode: String(fd.get("partyTypeCode") ?? "").trim() || null,
      routeCode: String(fd.get("routeCode") ?? "").trim() || null,
      minAmount: null,
      maxAmount: null,
      sortOrder: 0,
    };

    if (!body.code || !body.name) {
      setError("Nhập mã và tên quy tắc.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch(`/bff/rate-versions/${versionId}/rules`, {
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
              ? "Bạn không có quyền thêm quy tắc."
              : res.status === 409
                ? "Chỉ thêm quy tắc trên phiên bản nháp."
                : "Thêm quy tắc thất bại.")
        );
        return;
      }

      startTransition(() => router.refresh());
      e.currentTarget.reset();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <h4 className="section-title sm">Thêm quy tắc</h4>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor={`code-${versionId}`}>Mã quy tắc</label>
          <input
            id={`code-${versionId}`}
            name="code"
            required
            maxLength={64}
            placeholder="VD: FREIGHT"
          />
        </div>
        <div className="field">
          <label htmlFor={`name-${versionId}`}>Tên</label>
          <input id={`name-${versionId}`} name="name" required maxLength={256} />
        </div>
        <div className="field">
          <label htmlFor={`calcMethod-${versionId}`}>Cách tính</label>
          <select
            id={`calcMethod-${versionId}`}
            name="calcMethod"
            defaultValue="fixed"
          >
            <option value="fixed">Cố định</option>
            <option value="unit_rate">Đơn giá × số lượng</option>
            <option value="percent_of_base">% trên cơ sở</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor={`unitAmount-${versionId}`}>Số tiền / đơn giá</label>
          <input
            id={`unitAmount-${versionId}`}
            name="unitAmount"
            inputMode="decimal"
            required
          />
        </div>
        <div className="field">
          <label htmlFor={`currencyCode-${versionId}`}>Tiền tệ</label>
          <input
            id={`currencyCode-${versionId}`}
            name="currencyCode"
            defaultValue={defaultCurrency}
            maxLength={3}
            required
          />
        </div>
        <div className="field">
          <label htmlFor={`serviceTypeCode-${versionId}`}>
            Loại dịch vụ (lọc, tuỳ chọn)
          </label>
          <input id={`serviceTypeCode-${versionId}`} name="serviceTypeCode" />
        </div>
        <div className="field">
          <label htmlFor={`partyTypeCode-${versionId}`}>
            Mã loại đối tác (lọc, tuỳ chọn)
          </label>
          <input id={`partyTypeCode-${versionId}`} name="partyTypeCode" />
        </div>
        <div className="field">
          <label htmlFor={`routeCode-${versionId}`}>
            Mã tuyến (lọc, tuỳ chọn)
          </label>
          <input id={`routeCode-${versionId}`} name="routeCode" />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang thêm…" : "Thêm quy tắc"}
        </button>
      </div>
    </form>
  );
}
