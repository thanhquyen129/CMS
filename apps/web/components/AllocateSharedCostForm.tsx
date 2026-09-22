"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";
import type { AllocationBasis } from "@/lib/costs-revenues";

export type BillOption = {
  id: string;
  billNo: string;
};

type Props = {
  terms: TerminologyMap;
  costId: string;
  allocatableAmount: number;
  currencyCode: string;
  bills: BillOption[];
  hasDraft: boolean;
};

type RowState = {
  selected: boolean;
  basisValue: string;
};

export function AllocateSharedCostForm({
  terms,
  costId,
  allocatableAmount,
  currencyCode,
  bills,
  hasDraft,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [basis, setBasis] = useState<AllocationBasis>("equal");
  const [rows, setRows] = useState<Record<string, RowState>>(() =>
    Object.fromEntries(
      bills.map((b) => [b.id, { selected: false, basisValue: "1" }])
    )
  );

  const costLabel = term(terms, "COST", "Chi phí");
  const billLabel = term(terms, "BILL", "Bill");
  const allocLabel = term(terms, "COST_ALLOCATION", "Phân bổ chi phí");
  const draftLabel = term(terms, "ALLOCATION_DRAFT", "Phân bổ nháp");

  const selectedCount = useMemo(
    () => Object.values(rows).filter((r) => r.selected).length,
    [rows]
  );

  const measured =
    basis === "gross_kg" ||
    basis === "chargeable" ||
    basis === "cbm" ||
    basis === "package_count" ||
    basis === "teu";
  const needsBasis = !measured && basis !== "equal";
  const basisColumn =
    basis === "manual_percent"
      ? "Phần trăm"
      : basis === "manual_amount"
        ? "Số tiền"
        : basis === "quantity"
          ? "Số lượng"
          : "Tỷ lệ";

  if (hasDraft) {
    return (
      <p className="note">
        Đã có phiên {draftLabel.toLowerCase()}. Chốt hoặc tải lại trước khi tạo phiên mới.
      </p>
    );
  }

  if (bills.length < 2) {
    return (
      <div className="empty-state" role="status">
        Cần ít nhất 2 {billLabel} trong phạm vi để phân bổ {costLabel.toLowerCase()} chung.{" "}
        Tạo thêm {billLabel} rồi quay lại.
      </div>
    );
  }

  function toggleBill(id: string) {
    setRows((prev) => ({
      ...prev,
      [id]: { ...prev[id], selected: !prev[id]?.selected },
    }));
  }

  function setBasisValue(id: string, value: string) {
    setRows((prev) => ({
      ...prev,
      [id]: { ...prev[id], basisValue: value },
    }));
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const selected = bills.filter((b) => rows[b.id]?.selected);
    if (selected.length < 2) {
      setError(`Chọn ít nhất 2 ${billLabel}.`);
      setSubmitting(false);
      return;
    }

    const details: {
      billId: string;
      basisValue: number | null;
      manualOverrideAmount: null;
      overrideReason: null;
    }[] = [];

    for (const b of selected) {
      let basisValue: number | null = null;
      if (needsBasis) {
        const raw = String(rows[b.id]?.basisValue ?? "").trim();
        const n = Number(raw.replace(",", "."));
        if (!Number.isFinite(n) || n <= 0) {
          setError(
            `Giá trị cơ sở cho ${b.billNo} phải lớn hơn 0 (${basis === "quantity" ? "số lượng" : "tỷ lệ"}).`
          );
          setSubmitting(false);
          return;
        }
        basisValue = n;
      }
      details.push({
        billId: b.id,
        basisValue,
        manualOverrideAmount: null,
        overrideReason: null,
      });
    }

    try {
      const res = await fetch(`/bff/costs/${costId}/allocations`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          allocationBasis: basis,
          details,
        }),
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
              ? `Bạn không có quyền tạo ${allocLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không tạo được phiên phân bổ (trạng thái lệch). Tải lại trang."
                : "Tạo phân bổ nháp thất bại.")
        );
        return;
      }

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
      <p className="note">
        Tạo {draftLabel.toLowerCase()} từ số{" "}
        <strong>{formatMoney(allocatableAmount, currencyCode)}</strong>. Chọn ≥2{" "}
        {billLabel}. Kg, trọng lượng tính cước, CBM, kiện và TEU lấy từ số đo trên {billLabel}. Chốt mới ghi vào hồ sơ{" "}
        {billLabel} (conservation C-005).
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="field">
        <label htmlFor="allocation-basis">Cơ sở phân bổ</label>
        <select
          id="allocation-basis"
          value={basis}
          disabled={busy}
          onChange={(ev) => setBasis(ev.target.value as AllocationBasis)}
        >
          <option value="equal">Chia đều</option>
          <option value="quantity">Theo số lượng</option>
          <option value="gross_kg">Theo kg</option>
          <option value="chargeable">Theo trọng lượng tính cước</option>
          <option value="cbm">Theo CBM</option>
          <option value="package_count">Theo số kiện</option>
          <option value="teu">Theo TEU</option>
          <option value="manual_ratio">Tỷ lệ thủ công</option>
          <option value="manual_percent">Theo phần trăm</option>
          <option value="manual_amount">Theo số tiền</option>
        </select>
      </div>
      {measured ? (
        <p className="muted">Số đo bằng 0 trên mọi {billLabel} thì không chia đều.</p>
      ) : null}

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Chọn</th>
              <th scope="col">{billLabel}</th>
              {needsBasis ? (
                <th scope="col" className="num">
                  {basisColumn}
                </th>
              ) : null}
            </tr>
          </thead>
          <tbody>
            {bills.map((b) => {
              const row = rows[b.id];
              return (
                <tr key={b.id}>
                  <td>
                    <input
                      type="checkbox"
                      checked={!!row?.selected}
                      onChange={() => toggleBill(b.id)}
                      disabled={busy}
                      aria-label={`Chọn ${b.billNo}`}
                    />
                  </td>
                  <td>{b.billNo}</td>
                  {needsBasis ? (
                    <td className="num">
                      <input
                        type="number"
                        inputMode="decimal"
                        min={0}
                        step="any"
                        value={row?.basisValue ?? "1"}
                        onChange={(ev) => setBasisValue(b.id, ev.target.value)}
                        disabled={busy || !row?.selected}
                        style={{ width: "6rem", textAlign: "right" }}
                        aria-label={`${basisColumn} ${b.billNo}`}
                      />
                    </td>
                  ) : null}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <p className="meta-line muted">
        Đã chọn {selectedCount} {billLabel}
        {selectedCount < 2 ? " (cần ≥ 2)" : ""}.
      </p>

      <div className="cta-row">
        <button
          type="submit"
          className="btn"
          disabled={busy || selectedCount < 2}
        >
          {busy ? "Đang tạo…" : `Tạo ${draftLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
