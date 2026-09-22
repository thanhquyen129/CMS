"use client";

import type { FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CreateSection, CreateWorkspace } from "@/components/CreateWorkspace";
import {
  formStr,
  generateBusinessNo,
  manualSource,
} from "@/lib/create-workspace";

export type ShipmentOption = { id: string; shipmentNo: string };

type Props = {
  shipments: ShipmentOption[];
  shipmentsError?: string | null;
};

/** UI-02 Tạo Chặng — financial reference under Shipment; not TMS dispatch. */
export function CreateLegForm({ shipments, shipmentsError }: Props) {
  const router = useRouter();
  const today = new Date().toISOString().slice(0, 10);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const [autoNo, setAutoNo] = useState(false);
  const [legNo, setLegNo] = useState("");
  const [shipmentId, setShipmentId] = useState("");
  const submitting = busy || pending;
  const noShipments = !shipmentsError && shipments.length === 0;
  const shipmentLabel =
    shipments.find((s) => s.id === shipmentId)?.shipmentNo ?? "Chưa chọn";

  async function submit(status: "draft" | "active") {
    setError(null);
    if (shipmentsError) {
      setError(shipmentsError);
      return;
    }
    if (noShipments) {
      setError("Chưa có lô hàng để gắn chặng. Tạo Shipment trước.");
      return;
    }
    const form = document.getElementById("create-leg-form") as HTMLFormElement | null;
    if (!form) return;
    const fd = new FormData(form);
    let number = String(fd.get("legNo") ?? "").trim();
    if (autoNo && !number) {
      number = generateBusinessNo("LEG");
      setLegNo(number);
    }
    if (!number) {
      setError("Nhập số chặng.");
      return;
    }
    const shipId = String(fd.get("shipmentId") ?? "").trim();
    if (!shipId) {
      setError("Chọn lô hàng chứa chặng này.");
      return;
    }

    setBusy(true);
    try {
      const res = await fetch("/bff/transport-legs", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          legNo: number,
          shipmentId: shipId,
          sourceSystem: manualSource(),
          externalId: formStr(fd, "externalId") || number,
          operationalStatus: status,
          isActive: status !== "draft",
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được chặng.");
        return;
      }
      const created = (await res.json()) as { id?: string };
      startTransition(() =>
        router.push(
          created.id ? `/operations/legs/${created.id}` : "/operations?tab=legs"
        )
      );
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    await submit("active");
  }

  const actions = (
    <>
      <button
        className="btn btn-ghost"
        type="button"
        disabled={submitting}
        onClick={() => router.push("/operations?tab=legs")}
      >
        Hủy
      </button>
      <button
        className="btn"
        type="button"
        disabled={submitting || noShipments || !!shipmentsError}
        onClick={() => void submit("draft")}
      >
        Lưu nháp
      </button>
      <button
        className="btn"
        type="submit"
        form="create-leg-form"
        disabled={submitting || noShipments || !!shipmentsError}
      >
        {submitting ? "Đang lưu…" : "✓  Lưu chặng"}
      </button>
    </>
  );

  return (
    <form id="create-leg-form" onSubmit={onSubmit} noValidate>
      <CreateWorkspace
        breadcrumbs={[
          { href: "/dashboard", label: "Trang chủ" },
          { href: "/bills", label: "Đơn hàng vận chuyển" },
          { href: "/operations?tab=legs", label: "Chặng & Chuyến" },
          { label: "Tạo chặng" },
        ]}
        title="Tạo chặng"
        lede="Ghi nhận chặng vận chuyển làm tham chiếu tài chính dưới Shipment — không điều phối TMS."
        actions={actions}
        steps={[
          { id: "1", label: "Thông tin chung" },
          { id: "2", label: "Kiểm tra & hoàn tất" },
        ]}
        currentStep="1"
        error={error}
        summary={
          <>
            <h2>Tóm tắt chặng</h2>
            <div className="create-sumrow">
              <span>Số chặng</span>
              <b>{legNo || "Chưa nhập"}</b>
            </div>
            <div className="create-sumrow">
              <span>Lô hàng</span>
              <b>{shipmentLabel}</b>
            </div>
            <div className="create-sumrow">
              <span>Nguồn dữ liệu</span>
              <b>MANUAL</b>
            </div>
            <div className="create-statusbox">
              <strong>Trạng thái khi lưu</strong>
              <span className="create-tag">Nháp / Đang xử lý</span>
              <p className="muted" style={{ fontSize: 11, lineHeight: 1.5, margin: "8px 0 0" }}>
                Chặng thuộc đúng một Shipment (1:N). Liên kết Bill thực hiện sau trên hồ sơ chặng.
              </p>
            </div>
            <div className="create-notice">
              <b>ⓘ Lưu ý</b>
              <br />
              Chặng là tham chiếu vận hành cho neo tài chính. Tạo chặng không sinh chi phí, doanh thu hay lệnh điều vận.
            </div>
            <button
              className="btn"
              type="submit"
              disabled={submitting || noShipments || !!shipmentsError}
            >
              ✓  Lưu chặng
            </button>
            <button
              className="btn btn-ghost"
              type="button"
              disabled={submitting || noShipments || !!shipmentsError}
              onClick={() => void submit("draft")}
            >
              Lưu nháp
            </button>
          </>
        }
      >
        {shipmentsError ? (
          <div className="alert alert-error" role="alert">
            {shipmentsError}
          </div>
        ) : null}
        {noShipments ? (
          <div className="empty-state" role="status">
            Chưa có lô hàng để gắn chặng.{" "}
            <Link className="row-link" href="/shipments/new">
              Tạo Shipment
            </Link>{" "}
            trước, rồi quay lại màn này.
          </div>
        ) : null}

        <CreateSection
          title="1. Thông tin chung"
          hint="Số chặng và lô hàng chứa chặng — trường bắt buộc theo API"
          badge={<span className="create-tag">Nguồn: Nhập thủ công</span>}
        >
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="legNo">
                Số chặng <span className="req">*</span>
              </label>
              <input
                id="legNo"
                name="legNo"
                maxLength={64}
                value={legNo}
                onChange={(e) => setLegNo(e.target.value)}
                disabled={submitting || noShipments}
                placeholder="Nhập số chặng"
              />
              <p className="cw-hint">Mã nghiệp vụ hiện có hoặc tự động tạo.</p>
              <div className="create-checks" style={{ marginTop: 7 }}>
                <label>
                  <input
                    type="checkbox"
                    checked={autoNo}
                    disabled={submitting || noShipments}
                    onChange={(e) => {
                      const on = e.target.checked;
                      setAutoNo(on);
                      if (on && !legNo) setLegNo(generateBusinessNo("LEG"));
                    }}
                  />
                  Tự động tạo mã
                </label>
              </div>
            </div>
            <div className="cw-field">
              <label>Ngày tạo</label>
              <input type="date" defaultValue={today} readOnly />
            </div>
            <div className="cw-field">
              <label>Trạng thái</label>
              <input readOnly defaultValue="Nháp" />
            </div>
            <div className="cw-field">
              <label>Nguồn dữ liệu</label>
              <input readOnly defaultValue="Nhập thủ công (MANUAL)" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="shipmentId">
                Lô hàng <span className="req">*</span>
              </label>
              <select
                id="shipmentId"
                name="shipmentId"
                required
                disabled={submitting || noShipments}
                value={shipmentId}
                onChange={(e) => setShipmentId(e.target.value)}
              >
                <option value="" disabled>
                  Chọn lô hàng
                </option>
                {shipments.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.shipmentNo}
                  </option>
                ))}
              </select>
            </div>
            <div className="cw-field s6">
              <label>Hệ thống nguồn</label>
              <input readOnly defaultValue="LCMS" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="externalId">ID tham chiếu hệ thống nguồn</label>
              <input
                id="externalId"
                name="externalId"
                maxLength={128}
                disabled={submitting || noShipments}
                placeholder="Để trống = số chặng"
              />
            </div>
          </div>
        </CreateSection>

        <CreateSection
          title="2. Kiểm tra & hoàn tất"
          hint="Xác nhận số chặng và lô hàng trước khi lưu"
        >
          <p className="muted small">
            Sau khi lưu, gắn Bill trên hồ sơ chặng nếu cần. Gỡ liên kết (unlink) chưa có trên
            phiên bản này.
          </p>
        </CreateSection>
      </CreateWorkspace>
    </form>
  );
}
