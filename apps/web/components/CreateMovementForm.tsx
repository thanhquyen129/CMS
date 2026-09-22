"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CreateSection, CreateWorkspace } from "@/components/CreateWorkspace";
import {
  formStr,
  generateBusinessNo,
  manualSource,
} from "@/lib/create-workspace";

/** UI-02 Tạo Chuyến — financial movement reference; not TMS dispatch. */
export function CreateMovementForm() {
  const router = useRouter();
  const today = new Date().toISOString().slice(0, 10);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const [autoNo, setAutoNo] = useState(false);
  const [movementNo, setMovementNo] = useState("");
  const submitting = busy || pending;

  async function submit(status: "draft" | "active") {
    setError(null);
    const form = document.getElementById(
      "create-movement-form"
    ) as HTMLFormElement | null;
    if (!form) return;
    const fd = new FormData(form);
    let number = String(fd.get("movementNo") ?? "").trim();
    if (autoNo && !number) {
      number = generateBusinessNo("MOV");
      setMovementNo(number);
    }
    if (!number) {
      setError("Nhập số chuyến.");
      return;
    }

    setBusy(true);
    try {
      const res = await fetch("/bff/transport-movements", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          movementNo: number,
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
        setError(payload.message || "Không lưu được chuyến.");
        return;
      }
      const created = (await res.json()) as { id?: string };
      startTransition(() =>
        router.push(
          created.id
            ? `/operations/movements/${created.id}`
            : "/operations?tab=movements"
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
        onClick={() => router.push("/operations?tab=movements")}
      >
        Hủy
      </button>
      <button
        className="btn"
        type="button"
        disabled={submitting}
        onClick={() => void submit("draft")}
      >
        Lưu nháp
      </button>
      <button
        className="btn"
        type="submit"
        form="create-movement-form"
        disabled={submitting}
      >
        {submitting ? "Đang lưu…" : "✓  Lưu chuyến"}
      </button>
    </>
  );

  return (
    <form id="create-movement-form" onSubmit={onSubmit} noValidate>
      <CreateWorkspace
        breadcrumbs={[
          { href: "/dashboard", label: "Trang chủ" },
          { href: "/bills", label: "Đơn hàng vận chuyển" },
          { href: "/operations?tab=movements", label: "Chặng & Chuyến" },
          { label: "Tạo chuyến" },
        ]}
        title="Tạo chuyến"
        lede="Ghi nhận chuyến vận chuyển làm tham chiếu tài chính — không điều phối TMS."
        actions={actions}
        steps={[
          { id: "1", label: "Thông tin chung" },
          { id: "2", label: "Kiểm tra & hoàn tất" },
        ]}
        currentStep="1"
        error={error}
        summary={
          <>
            <h2>Tóm tắt chuyến</h2>
            <div className="create-sumrow">
              <span>Số chuyến</span>
              <b>{movementNo || "Chưa nhập"}</b>
            </div>
            <div className="create-sumrow">
              <span>Nguồn dữ liệu</span>
              <b>MANUAL</b>
            </div>
            <div className="create-statusbox">
              <strong>Trạng thái khi lưu</strong>
              <span className="create-tag">Nháp / Đang xử lý</span>
              <p className="muted" style={{ fontSize: 11, lineHeight: 1.5, margin: "8px 0 0" }}>
                Liên kết Bill hoặc chặng thực hiện sau trên hồ sơ chuyến.
              </p>
            </div>
            <div className="create-notice">
              <b>ⓘ Lưu ý</b>
              <br />
              Chuyến là tham chiếu vận hành cho neo tài chính. Tạo chuyến không sinh chi phí,
              doanh thu hay lệnh điều vận.
            </div>
            <button className="btn" type="submit" disabled={submitting}>
              ✓  Lưu chuyến
            </button>
            <button
              className="btn btn-ghost"
              type="button"
              disabled={submitting}
              onClick={() => void submit("draft")}
            >
              Lưu nháp
            </button>
          </>
        }
      >
        <CreateSection
          title="1. Thông tin chung"
          hint="Số chuyến — trường bắt buộc theo API"
          badge={<span className="create-tag">Nguồn: Nhập thủ công</span>}
        >
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="movementNo">
                Số chuyến <span className="req">*</span>
              </label>
              <input
                id="movementNo"
                name="movementNo"
                maxLength={64}
                value={movementNo}
                onChange={(e) => setMovementNo(e.target.value)}
                disabled={submitting}
                placeholder="Nhập số chuyến"
              />
              <p className="cw-hint">Mã nghiệp vụ hiện có hoặc tự động tạo.</p>
              <div className="create-checks" style={{ marginTop: 7 }}>
                <label>
                  <input
                    type="checkbox"
                    checked={autoNo}
                    disabled={submitting}
                    onChange={(e) => {
                      const on = e.target.checked;
                      setAutoNo(on);
                      if (on && !movementNo)
                        setMovementNo(generateBusinessNo("MOV"));
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
              <label>Hệ thống nguồn</label>
              <input readOnly defaultValue="LCMS" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="externalId">ID tham chiếu hệ thống nguồn</label>
              <input
                id="externalId"
                name="externalId"
                maxLength={128}
                disabled={submitting}
                placeholder="Để trống = số chuyến"
              />
            </div>
          </div>
        </CreateSection>

        <CreateSection
          title="2. Kiểm tra & hoàn tất"
          hint="Xác nhận số chuyến trước khi lưu"
        >
          <p className="muted small">
            Sau khi lưu, gắn Bill trên hồ sơ chuyến nếu cần. Gỡ liên kết trực tiếp thực hiện từ
            hồ sơ Bill (Financial View).
          </p>
        </CreateSection>
      </CreateWorkspace>
    </form>
  );
}
