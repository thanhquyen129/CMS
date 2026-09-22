"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

export function CreateRateCardForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      partyType: String(fd.get("partyType") ?? "vendor").trim(),
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      description: String(fd.get("description") ?? "").trim() || null,
      transportMode: String(fd.get("transportMode") ?? "").trim() || null,
      routeCode: String(fd.get("routeCode") ?? "").trim() || null,
      carrierName: String(fd.get("carrierName") ?? "").trim() || null,
    };

    if (!body.code || !body.name) {
      setError("Nhập mã và tên bảng giá.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch("/bff/rate-cards", {
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
              ? "Bạn không có quyền tạo bảng giá."
              : res.status === 409
                ? "Mã bảng giá đã tồn tại."
                : "Tạo bảng giá thất bại.")
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      const next = created.id
        ? `/rate-cards/${created.id}`
        : "/rate-cards";
      startTransition(() => router.push(next));
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
        Tạo bảng giá → thêm phiên bản nháp → quy tắc → <strong>phát hành</strong>{" "}
        → tính giá trên Bill → seed chi phí <strong>Dự kiến</strong>.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="code">Mã</label>
          <input
            id="code"
            name="code"
            required
            maxLength={64}
            autoComplete="off"
            placeholder="VD: VND-FCL-2026"
          />
        </div>
        <div className="field">
          <label htmlFor="name">Tên</label>
          <input
            id="name"
            name="name"
            required
            maxLength={256}
            autoComplete="off"
          />
        </div>
        <div className="field">
          <label htmlFor="partyType">Loại giá</label>
          <select id="partyType" name="partyType" defaultValue="vendor">
            <option value="vendor">Giá mua</option>
            <option value="customer">Giá bán</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="transportMode">Phương thức</label>
          <select id="transportMode" name="transportMode" defaultValue="air">
            <option value="air">Air</option>
            <option value="sea">Sea</option>
            <option value="road">Road</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="carrierName">Hãng/NCC</label>
          <input id="carrierName" name="carrierName" maxLength={128} />
        </div>
        <div className="field">
          <label htmlFor="routeCode">Tuyến</label>
          <input id="routeCode" name="routeCode" maxLength={64} placeholder="SGN-FRA" />
        </div>
        <div className="field">
          <label htmlFor="currencyCode">Tiền tệ</label>
          <input
            id="currencyCode"
            name="currencyCode"
            defaultValue="VND"
            maxLength={3}
            required
          />
        </div>
        <div className="field field-span">
          <label htmlFor="description">Mô tả (tuỳ chọn)</label>
          <input id="description" name="description" maxLength={1024} />
        </div>
      </div>

      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang tạo…" : "Tạo bảng giá"}
        </button>
      </div>
    </form>
  );
}
