"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { BillTypeahead } from "@/components/BillTypeahead";
import { DateInputVi } from "@/components/DateInputVi";
import { PartyTypeahead } from "@/components/PartyTypeahead";
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
  defaultBillNo?: string;
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
  defaultBillNo,
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
  const initialLinks = isPayable ? costOptions : revenueOptions;
  const [options, setOptions] = useState(initialLinks);
  const [exception, setException] = useState(false);
  const exposureLabel = isPayable
    ? term(terms, "PAYABLE_EXPOSURE", "Nghĩa vụ phải trả (exposure)")
    : term(terms, "RECEIVABLE_EXPOSURE", "Quyền thu dự kiến (exposure)");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const recognizedTarget = isPayable ? apLabel : arLabel;
  const linkLabel = isPayable ? costLabel : revenueLabel;

  async function loadForBill(id: string) {
    if (!id) {
      setOptions([]);
      return;
    }
    const path = isPayable ? `/bff/costs?billId=${encodeURIComponent(id)}` : `/bff/revenues?billId=${encodeURIComponent(id)}`;
    const res = await fetch(path, { headers: { Accept: "application/json" }, cache: "no-store" });
    if (!res.ok) return;
    const payload = (await res.json()) as { items?: Array<Record<string, unknown>> } | Array<Record<string, unknown>>;
    const rows = Array.isArray(payload) ? payload : payload.items ?? [];
    setOptions(
      rows
        .filter((row) => !row.recordStatus || row.recordStatus === "active")
        .map((row) => ({
          id: String(row.id),
          label: `${String(isPayable ? row.costTypeCode || "Chi phí" : row.revenueTypeCode || "Doanh thu")} · ${String(row.financialMaturity || "")}`,
          amount: Number(row.amount ?? 0),
          currencyCode: String(row.currencyCode || "VND"),
        }))
    );
  }

  function onLinkChange(id: string) {
    setLinkedId(id);
    const opt = options.find((o) => o.id === id);
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
    const linkRawEarly = String(fd.get("linkedId") ?? "").trim();
    const notesEarly = String(fd.get("notes") ?? "").trim();
    const partyEarly = String(fd.get("counterpartyId") ?? "").trim();
    if (!exception && (!billIdRaw || !linkRawEarly)) {
      setError(`Chọn Bill và khoản ${linkLabel.toLowerCase()}, hoặc bật luồng ngoại lệ rồi chọn đối tác và nhập lý do.`);
      setSubmitting(false);
      return;
    }
    if (exception && (!partyEarly || notesEarly.length < 3)) {
      setError("Luồng ngoại lệ cần đối tác đúng vai trò và căn cứ/lý do.");
      setSubmitting(false);
      return;
    }
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
      counterpartyId: partyEarly || null,
      financialDocumentId: null,
      notes: notesEarly || null,
      sourceType: exception ? "exception" : null,
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
        {exposureLabel} khác {isPayable ? costLabel : revenueLabel} lớp dự kiến và khác {recognizedTarget}.
        Phải gắn khoản nguồn, chứng từ, hoặc luồng ngoại lệ có đối tác và lý do. Không nhập UUID.
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
              <DateInputVi id="effectiveDate" name="effectiveDate" disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="dueDate">Ngày đến hạn thanh toán</label>
              <DateInputVi id="dueDate" name="dueDate" disabled={busy} />
            </div>
          </div>
        </fieldset>
        <fieldset className="group-box">
          <legend>Liên kết</legend>
          <div className="form-grid">
            <BillTypeahead
              label={billLabel}
              defaultId={defaultBillId}
              defaultLabel={defaultBillNo}
              disabled={busy}
              onSelect={(bill) => {
                void loadForBill(bill?.id ?? "");
                if (!bill) setLinkedId("");
              }}
            />
            <div className="field">
              <label htmlFor="linkedId">Khoản {linkLabel.toLowerCase()}</label>
              <select
                id="linkedId"
                name="linkedId"
                disabled={busy || exception}
                value={linkedId}
                onChange={(e) => onLinkChange(e.target.value)}
              >
                <option value="">{options.length === 0 ? "Chọn Bill để tải danh sách" : "Chọn khoản nguồn"}</option>
                {options.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.label} · {formatMoney(o.amount, o.currencyCode)}
                  </option>
                ))}
              </select>
              <p className="note">Danh sách lọc theo Bill. Không nhập UUID.</p>
            </div>
            <div className="field field-span">
              <label>
                <input type="checkbox" checked={exception} disabled={busy} onChange={(e) => setException(e.target.checked)} />
                {" "}Luồng ngoại lệ (đối tác + lý do, có kiểm soát)
              </label>
            </div>
            {exception ? (
              <PartyTypeahead
                name="counterpartyId"
                label="Đối tác ngoại lệ"
                roleCode={isPayable ? "vendor" : "customer"}
                disabled={busy}
                required
                hint={isPayable ? "Chỉ nhà cung cấp hoặc bên nhận tiền." : "Chỉ khách hàng, bên trả tiền hoặc bill-to."}
              />
            ) : null}
            <div className="field field-span">
              <label htmlFor="notes">Căn cứ / lý do ngoại lệ</label>
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
