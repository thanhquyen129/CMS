"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

const SOURCE_DEFAULT = "lcms_manual";

type Kind = "order" | "shipment" | "leg" | "movement";

type Props = {
  kind: Kind;
  shipments?: { id: string; shipmentNo: string }[];
};

const META: Record<
  Kind,
  { title: string; numberLabel: string; endpoint: string; numberField: string }
> = {
  order: {
    title: "Nhập tham chiếu đơn hàng",
    numberLabel: "Số đơn hàng",
    endpoint: "/bff/orders",
    numberField: "orderNo",
  },
  shipment: {
    title: "Nhập tham chiếu lô hàng",
    numberLabel: "Số lô hàng",
    endpoint: "/bff/shipments",
    numberField: "shipmentNo",
  },
  leg: {
    title: "Nhập tham chiếu chặng",
    numberLabel: "Số chặng",
    endpoint: "/bff/transport-legs",
    numberField: "legNo",
  },
  movement: {
    title: "Nhập tham chiếu chuyến",
    numberLabel: "Số chuyến",
    endpoint: "/bff/transport-movements",
    numberField: "movementNo",
  },
};

/** Manual Reference Entry — same canonical upsert as API/import (SCP-003). */
export function ManualReferenceForm({ kind, shipments }: Props) {
  const router = useRouter();
  const meta = META[kind];
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const number = String(fd.get("number") ?? "").trim();
    const sourceSystem =
      String(fd.get("sourceSystem") ?? "").trim() || SOURCE_DEFAULT;
    const externalId = String(fd.get("externalId") ?? "").trim() || number;
    const operationalStatus =
      String(fd.get("operationalStatus") ?? "").trim() || "active";

    if (!number) {
      setError(`Nhập ${meta.numberLabel.toLowerCase()}.`);
      setSubmitting(false);
      return;
    }

    const body: Record<string, unknown> = {
      [meta.numberField]: number,
      sourceSystem,
      externalId,
      operationalStatus,
      isActive: true,
    };

    if (kind === "leg") {
      const shipmentId = String(fd.get("shipmentId") ?? "").trim();
      if (!shipmentId) {
        setError("Chọn lô hàng chứa chặng này.");
        setSubmitting(false);
        return;
      }
      body.shipmentId = shipmentId;
    }

    try {
      const res = await fetch(meta.endpoint, {
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
        setError(payload.message || "Không lưu được tham chiếu.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <h3 className="section-title sm">{meta.title}</h3>
      <p className="muted small">
        Tham chiếu tài chính — không điều phối vận tải. Nguồn mặc định: nhập tay
        LCMS.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor={`${kind}-number`}>{meta.numberLabel}</label>
          <input
            id={`${kind}-number`}
            name="number"
            required
            maxLength={64}
            disabled={busy}
          />
        </div>
        {kind === "leg" ? (
          <div className="field">
            <label htmlFor="leg-shipment">Lô hàng</label>
            <select
              id="leg-shipment"
              name="shipmentId"
              required
              disabled={busy || !shipments?.length}
              defaultValue=""
            >
              <option value="" disabled>
                {shipments?.length ? "Chọn lô hàng" : "Chưa có lô hàng"}
              </option>
              {(shipments ?? []).map((s) => (
                <option key={s.id} value={s.id}>
                  {s.shipmentNo}
                </option>
              ))}
            </select>
          </div>
        ) : null}
        <div className="field">
          <label htmlFor={`${kind}-source`}>Hệ thống nguồn</label>
          <input
            id={`${kind}-source`}
            name="sourceSystem"
            defaultValue={SOURCE_DEFAULT}
            maxLength={64}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor={`${kind}-ext`}>Mã ngoài (để trống = số trên)</label>
          <input
            id={`${kind}-ext`}
            name="externalId"
            maxLength={128}
            disabled={busy}
          />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang lưu…" : "Lưu tham chiếu"}
        </button>
      </div>
    </form>
  );
}
