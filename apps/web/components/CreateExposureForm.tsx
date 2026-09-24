"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CurrencySelect } from "@/components/CurrencySelect";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";

type Kind = "payable" | "receivable";

export type ExposureLinkOption = {
  id: string;
  label: string;
  amount: number;
  currencyCode: string;
};

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  defaultBillId?: string;
  defaultCurrency?: string;
  defaultAmount?: number;
  defaultCostId?: string;
  defaultRevenueId?: string;
  costOptions?: ExposureLinkOption[];
  revenueOptions?: ExposureLinkOption[];
};

export function CreateExposureForm({
  terms,
  kind,
  defaultBillId,
  defaultCurrency = "VND",
  defaultAmount,
  defaultCostId,
  defaultRevenueId,
  costOptions = [],
  revenueOptions = [],
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [amount, setAmount] = useState(
    defaultAmount != null && Number.isFinite(defaultAmount)
      ? String(defaultAmount)
      : ""
  );
  const [currency, setCurrency] = useState(defaultCurrency);
  const [linkedId, setLinkedId] = useState(
    kind === "payable" ? defaultCostId ?? "" : defaultRevenueId ?? ""
  );

  const isPayable = kind === "payable";
  const exposureLabel = isPayable
    ? term(terms, "PAYABLE_EXPOSURE", "Nghĩa vụ phải trả (exposure)")
    : term(terms, "RECEIVABLE_EXPOSURE", "Quyền thu dự kiến (exposure)");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const recognizedTarget = isPayable ? apLabel : arLabel;
  const linkOptions = isPayable ? costOptions : revenueOptions;
  const linkLabel = isPayable ? costLabel : revenueLabel;

  function onLinkChange(id: string) {
    setLinkedId(id);
    const opt = linkOptions.find((o) => o.id === id);
    if (opt) {
      setAmount(String(opt.amount));
      setCurrency(opt.currencyCode);
    }
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const parsed = Number(amountRaw.replace(",", "."));
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError("Số tiền phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }

    const billIdRaw = String(fd.get("billId") ?? "").trim();
    const effectiveDateRaw = String(fd.get("effectiveDate") ?? "").trim();
    const dueDateRaw = String(fd.get("dueDate") ?? "").trim();
    const linkRaw = String(fd.get("linkedId") ?? "").trim();

    const body: Record<string, unknown> = {
      amount: parsed,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      effectiveDate: effectiveDateRaw || null,
      dueDate: dueDateRaw || null,
      billId: billIdRaw || null,
      counterpartyId: null,
      financialDocumentId: null,
      notes: String(fd.get("notes") ?? "").trim() || null,
      sourceType: null,
      sourceId: null,
    };

    if (isPayable) {
      body.costId = linkRaw || null;
    } else {
      body.revenueId = linkRaw || null;
    }

    const endpoint = isPayable
      ? "/bff/payable-exposures"
      : "/bff/receivable-exposures";

    try {
      const res = await fetch(endpoint, {
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
              ? "Bạn không có quyền tạo exposure."
              : res.status === 409
                ? "Không thể tạo vì xung đột trạng thái."
                : "Tạo exposure thất bại.")
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      if (created.id) {
        startTransition(() =>
          router.push(
            `/ap-ar/exposures/${created.id}/recognize?kind=${kind}`
          )
        );
      } else {
        startTransition(() => router.push("/ap-ar?tab=exposure"));
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
        Exposure ≠ {recognizedTarget}. Đây là nghĩa vụ / quyền{" "}
        <strong>dự kiến</strong> — chưa phải {isPayable ? costLabel : revenueLabel}{" "}
        và chưa phải sổ đã ghi nhận. Có thể gắn {linkLabel.toLowerCase()} (liên
        kết, không tạo thêm {linkLabel.toLowerCase()}). Sau khi tạo → ghi nhận
        sang {recognizedTarget}.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-sections cols-2">
        <fieldset className="group-box">
          <legend>Số tiền &amp; hạn</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="amount">Số tiền</label>
              <input
                id="amount"
                name="amount"
                type="number"
                inputMode="decimal"
                min={0}
                step="any"
                required
                disabled={busy}
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
            </div>
            <CurrencySelect
              id="currencyCode"
              value={currency}
              onChange={setCurrency}
              disabled={busy}
            />
            <div className="field">
              <label htmlFor="effectiveDate">Ngày hiệu lực</label>
              <input
                id="effectiveDate"
                name="effectiveDate"
                type="date"
                disabled={busy}
              />
            </div>
            <div className="field">
              <label htmlFor="dueDate">Hạn</label>
              <input id="dueDate" name="dueDate" type="date" disabled={busy} />
            </div>
          </div>
        </fieldset>
        <fieldset className="group-box">
          <legend>Liên kết</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="billId">{billLabel} (tuỳ chọn, UUID)</label>
              <input
                id="billId"
                name="billId"
                defaultValue={defaultBillId ?? ""}
                disabled={busy}
                placeholder="Gắn Bill nếu có"
                autoComplete="off"
              />
            </div>
            {linkOptions.length > 0 ? (
              <div className="field">
                <label htmlFor="linkedId">
                  Gắn {linkLabel.toLowerCase()} (tuỳ chọn)
                </label>
                <select
                  id="linkedId"
                  name="linkedId"
                  disabled={busy}
                  value={linkedId}
                  onChange={(e) => onLinkChange(e.target.value)}
                >
                  <option value="">— Không gắn —</option>
                  {linkOptions.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.label} · {formatMoney(o.amount, o.currencyCode)}
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <div className="field">
                <label htmlFor="linkedId">
                  Mã {linkLabel.toLowerCase()} (UUID, tuỳ chọn)
                </label>
                <input
                  id="linkedId"
                  name="linkedId"
                  defaultValue={
                    kind === "payable"
                      ? defaultCostId ?? ""
                      : defaultRevenueId ?? ""
                  }
                  disabled={busy}
                  autoComplete="off"
                  placeholder={`Gắn ${linkLabel.toLowerCase()} nếu biết UUID`}
                />
              </div>
            )}
            <div className="field field-span">
              <label htmlFor="notes">Ghi chú</label>
              <input id="notes" name="notes" maxLength={2048} disabled={busy} />
            </div>
          </div>
        </fieldset>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang tạo…" : `Tạo ${exposureLabel}`}
        </button>
      </div>
    </form>
  );
}
