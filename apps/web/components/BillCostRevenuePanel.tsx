"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import {
  canActualize,
  canConfirm,
  maturityLabelKey,
  type CostListItem,
  type RevenueListItem,
} from "@/lib/costs-revenues";
import { formatMoney } from "@/lib/money";
import { AdjustCostRevenueButton } from "@/components/AdjustCostRevenueButton";

type Kind = "cost" | "revenue";
type Action = "confirm" | "actualize";

type PendingAction = {
  kind: Kind;
  action: Action;
  id: string;
  amount: number;
  currencyCode: string;
  label: string;
};

type Props = {
  terms: TerminologyMap;
  billId: string;
  costs: CostListItem[] | null;
  costsError: string | null;
  revenues: RevenueListItem[] | null;
  revenuesError: string | null;
  /** When set, show only that section (Bill financial view tabs). */
  focus?: "costs" | "revenues" | "all";
};

function attributionLabel(
  terms: TerminologyMap,
  attributionType: string
): string {
  if (attributionType?.toLowerCase() === "direct") {
    return term(terms, "ATTRIBUTION_DIRECT", "Trực tiếp");
  }
  if (attributionType?.toLowerCase() === "shared") {
    return term(terms, "ATTRIBUTION_SHARED", "Chung");
  }
  return attributionType;
}

function maturityVi(terms: TerminologyMap, maturity: string): string {
  const key = maturityLabelKey(maturity);
  const fallback =
    maturity?.toLowerCase() === "expected"
      ? "Dự kiến"
      : maturity?.toLowerCase() === "confirmed"
        ? "Đã xác nhận"
        : maturity?.toLowerCase() === "actual"
          ? "Thực tế"
          : maturity;
  return term(terms, key, fallback);
}

export function BillCostRevenuePanel({
  terms,
  billId,
  costs,
  costsError,
  revenues,
  revenuesError,
  focus = "all",
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [overrideAmount, setOverrideAmount] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState<Record<string, true>>({});
  const [isPending, startTransition] = useTransition();
  const [submitting, setSubmitting] = useState(false);

  const showCosts = focus === "all" || focus === "costs";
  const showRevenues = focus === "all" || focus === "revenues";

  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");

  const closeDialog = useCallback(() => {
    if (submitting) return;
    setPending(null);
    setOverrideAmount("");
  }, [submitting]);

  const openAction = useCallback((p: PendingAction) => {
    setError(null);
    setPending(p);
    setOverrideAmount(String(p.amount));
  }, []);

  const runAction = useCallback(async () => {
    if (!pending) return;
    setSubmitting(true);
    setError(null);

    const parsed = Number(String(overrideAmount).replace(",", "."));
    if (!Number.isFinite(parsed) || parsed < 0) {
      setError("Số tiền không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const base =
      pending.kind === "cost"
        ? `/bff/costs/${pending.id}`
        : `/bff/revenues/${pending.id}`;
    const path =
      pending.action === "confirm" ? `${base}/confirm` : `${base}/actualize`;
    const body =
      pending.action === "confirm"
        ? { confirmedAmount: parsed }
        : { actualAmount: parsed };

    try {
      const res = await fetch(path, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        const msg =
          body.message ||
          (res.status === 403
            ? "Bạn không có quyền thực hiện thao tác này."
            : res.status === 409
              ? "Không thể thực hiện vì xung đột trạng thái. Tải lại trang và thử lại."
              : "Thao tác thất bại.");
        setError(msg);
        if (res.status === 403 || res.status === 409) {
          setBlocked((prev) => ({
            ...prev,
            [`${pending.kind}:${pending.action}:${pending.id}`]: true,
          }));
          setPending(null);
          if (res.status === 409) {
            startTransition(() => {
              router.refresh();
            });
          }
        }
        return;
      }

      setPending(null);
      setOverrideAmount("");
      startTransition(() => {
        router.refresh();
      });
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [pending, overrideAmount, router]);

  const isBlocked = (kind: Kind, action: Action, id: string) =>
    Boolean(blocked[`${kind}:${action}:${id}`]);

  const dialogCopy =
    pending?.action === "confirm"
      ? {
          title: `Xác nhận ${pending.kind === "cost" ? costLabel : revenueLabel}`,
          body: `Chuyển từ ${expected} → ${confirmed}. Có thể chỉnh số ghi vào lớp ${confirmed} (Dự kiến và Đã xác nhận độc lập). Lớp ${expected} (${formatMoney(pending.amount, pending.currencyCode)}) được giữ nguyên — không ghi đè im lặng.`,
          amountLabel: `Số ${confirmed}`,
          cta: `Xác nhận ${pending.kind === "cost" ? costLabel : revenueLabel}`,
        }
      : pending
        ? {
            title: `Ghi nhận ${actual} — ${pending.kind === "cost" ? costLabel : revenueLabel}`,
            body: `Chuyển từ ${confirmed} → ${actual}. Có thể chỉnh số lớp ${actual}. Các lớp ${expected}/${confirmed} được giữ nguyên.`,
            amountLabel: `Số ${actual}`,
            cta: `Ghi nhận ${actual}`,
          }
        : null;

  return (
    <div className="maturity-actions">
      <h2 className="section-title">
        {focus === "costs"
          ? costLabel
          : focus === "revenues"
            ? revenueLabel
            : `Xác nhận ${costLabel} / ${revenueLabel}`}
      </h2>
      <p className="muted small">
        Một thao tác mỗi dòng: xác nhận ({expected} → {confirmed}) hoặc ghi nhận{" "}
        {actual} ({confirmed} → {actual}) — dialog cho phép nhập số lớp đích.
        Điều chỉnh (delta + lý do) ghi lịch sử, không silent overwrite.
      </p>
      <p className="cta-row" style={{ marginTop: 0 }}>
        {showCosts ? (
          <Link className="btn btn-sm" href={`/bills/${billId}/costs/new`}>
            Tạo {costLabel.toLowerCase()}
          </Link>
        ) : null}{" "}
        {showRevenues ? (
          <Link className="btn btn-sm" href={`/bills/${billId}/revenues/new`}>
            Tạo {revenueLabel.toLowerCase()}
          </Link>
        ) : null}{" "}
        {showCosts ? (
          <Link className="btn btn-sm btn-ghost" href="/costs/shared">
            {costLabel} {term(terms, "ATTRIBUTION_SHARED", "Chung").toLowerCase()}
          </Link>
        ) : null}
      </p>
      {showCosts ? (
        <p className="note">
          {costLabel} trực tiếp gắn Bill này; {costLabel.toLowerCase()} chung phân bổ từ{" "}
          <Link className="row-link" href="/costs/shared">
            màn phân bổ
          </Link>{" "}
          (≥2 Bill → nháp → chốt). Phần phân bổ đã chốt hiện ở hồ sơ tài chính / lợi nhuận.
        </p>
      ) : null}

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {showCosts ? (
        <>
      <h3 className="section-title sm">{costLabel}</h3>
      {costsError ? (
        <div className="alert alert-error" role="alert">
          {costsError}
        </div>
      ) : !costs || costs.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có dòng {costLabel.toLowerCase()} gắn Bill này.{" "}
          <Link className="row-link" href={`/bills/${billId}/costs/new`}>
            Tạo {costLabel.toLowerCase()}
          </Link>
          .
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Loại / Gán</th>
                <th scope="col">Ngày hiệu lực</th>
                <th scope="col">Trưởng thành</th>
                <th scope="col" className="num">
                  Số tiền
                </th>
                <th scope="col">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {costs.map((c) => {
                const confirmOk =
                  canConfirm(c.financialMaturity, c.recordStatus) &&
                  !isBlocked("cost", "confirm", c.id);
                const actualizeOk =
                  canActualize(c.financialMaturity, c.recordStatus) &&
                  !isBlocked("cost", "actualize", c.id);
                return (
                  <tr key={c.id}>
                    <td>
                      {c.costTypeCode || "—"}
                      <span className="muted small block">
                        {attributionLabel(terms, c.attributionType)}
                        {c.recordStatus !== "active" ? ` · ${c.recordStatus}` : ""}
                      </span>
                    </td>
                    <td>{c.effectiveDate}</td>
                    <td>
                      <span className={`maturity-pill maturity-${c.financialMaturity}`}>
                        {maturityVi(terms, c.financialMaturity)}
                      </span>
                    </td>
                    <td className="num">{formatMoney(c.amount, c.currencyCode)}</td>
                    <td>
                      <div className="row-actions">
                        {confirmOk ? (
                          <button
                            type="button"
                            className="btn btn-sm"
                            disabled={submitting || isPending}
                            onClick={() =>
                              openAction({
                                kind: "cost",
                                action: "confirm",
                                id: c.id,
                                amount: c.amount,
                                currencyCode: c.currencyCode,
                                label: costLabel,
                              })
                            }
                          >
                            Xác nhận {costLabel}
                          </button>
                        ) : actualizeOk ? (
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            disabled={submitting || isPending}
                            onClick={() =>
                              openAction({
                                kind: "cost",
                                action: "actualize",
                                id: c.id,
                                amount: c.amount,
                                currencyCode: c.currencyCode,
                                label: costLabel,
                              })
                            }
                          >
                            Ghi nhận {actual}
                          </button>
                        ) : isBlocked("cost", "confirm", c.id) ||
                          isBlocked("cost", "actualize", c.id) ? (
                          <span className="muted small">Không thể thao tác</span>
                        ) : c.financialMaturity?.toLowerCase() === "actual" ? (
                          <span className="muted small">{actual}</span>
                        ) : c.recordStatus !== "active" ? (
                          <span className="muted small">Không hiệu lực</span>
                        ) : (
                          <span className="muted small">—</span>
                        )}
                        {c.recordStatus === "active" ? (
                          <AdjustCostRevenueButton
                            terms={terms}
                            kind="cost"
                            lineId={c.id}
                            currentAmount={c.amount}
                            currencyCode={c.currencyCode}
                            financialMaturity={c.financialMaturity}
                          />
                        ) : null}
                        <Link
                          className="btn btn-ghost btn-sm"
                          href={`/costs/${c.id}`}
                        >
                          Lịch sử
                        </Link>
                        {c.recordStatus === "active" ? (
                          <Link
                            className="btn btn-ghost btn-sm"
                            href={`/ap-ar/exposures/new?kind=payable&billId=${encodeURIComponent(billId)}&costId=${encodeURIComponent(c.id)}&amount=${encodeURIComponent(String(c.amount))}&currency=${encodeURIComponent(c.currencyCode)}`}
                          >
                            Exposure
                          </Link>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
        </>
      ) : null}

      {showRevenues ? (
        <>
      <h3 className="section-title sm">{revenueLabel}</h3>
      {revenuesError ? (
        <div className="alert alert-error" role="alert">
          {revenuesError}
        </div>
      ) : !revenues || revenues.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có dòng {revenueLabel.toLowerCase()} gắn Bill này.{" "}
          <Link className="row-link" href={`/bills/${billId}/revenues/new`}>
            Tạo {revenueLabel.toLowerCase()}
          </Link>
          .
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Loại</th>
                <th scope="col">Ngày hiệu lực</th>
                <th scope="col">Trưởng thành</th>
                <th scope="col" className="num">
                  Số tiền
                </th>
                <th scope="col">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {revenues.map((r) => {
                const confirmOk =
                  canConfirm(r.financialMaturity, r.recordStatus) &&
                  !isBlocked("revenue", "confirm", r.id);
                const actualizeOk =
                  canActualize(r.financialMaturity, r.recordStatus) &&
                  !isBlocked("revenue", "actualize", r.id);
                return (
                  <tr key={r.id}>
                    <td>{r.revenueTypeCode || "—"}</td>
                    <td>{r.effectiveDate}</td>
                    <td>
                      <span className={`maturity-pill maturity-${r.financialMaturity}`}>
                        {maturityVi(terms, r.financialMaturity)}
                      </span>
                    </td>
                    <td className="num">{formatMoney(r.amount, r.currencyCode)}</td>
                    <td>
                      <div className="row-actions">
                        {confirmOk ? (
                          <button
                            type="button"
                            className="btn btn-sm"
                            disabled={submitting || isPending}
                            onClick={() =>
                              openAction({
                                kind: "revenue",
                                action: "confirm",
                                id: r.id,
                                amount: r.amount,
                                currencyCode: r.currencyCode,
                                label: revenueLabel,
                              })
                            }
                          >
                            Xác nhận {revenueLabel}
                          </button>
                        ) : actualizeOk ? (
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            disabled={submitting || isPending}
                            onClick={() =>
                              openAction({
                                kind: "revenue",
                                action: "actualize",
                                id: r.id,
                                amount: r.amount,
                                currencyCode: r.currencyCode,
                                label: revenueLabel,
                              })
                            }
                          >
                            Ghi nhận {actual}
                          </button>
                        ) : isBlocked("revenue", "confirm", r.id) ||
                          isBlocked("revenue", "actualize", r.id) ? (
                          <span className="muted small">Không thể thao tác</span>
                        ) : r.financialMaturity?.toLowerCase() === "actual" ? (
                          <span className="muted small">{actual}</span>
                        ) : r.recordStatus !== "active" ? (
                          <span className="muted small">Không hiệu lực</span>
                        ) : (
                          <span className="muted small">—</span>
                        )}
                        {r.recordStatus === "active" ? (
                          <AdjustCostRevenueButton
                            terms={terms}
                            kind="revenue"
                            lineId={r.id}
                            currentAmount={r.amount}
                            currencyCode={r.currencyCode}
                            financialMaturity={r.financialMaturity}
                          />
                        ) : null}
                        <Link
                          className="btn btn-ghost btn-sm"
                          href={`/revenues/${r.id}`}
                        >
                          Lịch sử
                        </Link>
                        {r.recordStatus === "active" ? (
                          <Link
                            className="btn btn-ghost btn-sm"
                            href={`/ap-ar/exposures/new?kind=receivable&billId=${encodeURIComponent(billId)}&revenueId=${encodeURIComponent(r.id)}&amount=${encodeURIComponent(String(r.amount))}&currency=${encodeURIComponent(r.currencyCode)}`}
                          >
                            Exposure
                          </Link>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
        </>
      ) : null}

      {pending && dialogCopy ? (
        <div
          className="dialog-backdrop"
          role="presentation"
          onClick={closeDialog}
          onKeyDown={(e) => {
            if (e.key === "Escape") closeDialog();
          }}
        >
          <div
            className="dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby={dialogTitleId}
            onClick={(e) => e.stopPropagation()}
          >
            <h2 id={dialogTitleId}>{dialogCopy.title}</h2>
            <p>{dialogCopy.body}</p>
            <div className="field" style={{ marginTop: "0.75rem" }}>
              <label htmlFor="maturity-override-amount">
                {dialogCopy.amountLabel} ({pending.currencyCode})
              </label>
              <input
                id="maturity-override-amount"
                type="number"
                inputMode="decimal"
                min={0}
                step="any"
                value={overrideAmount}
                disabled={submitting}
                onChange={(e) => setOverrideAmount(e.target.value)}
              />
            </div>
            {error ? (
              <div className="alert alert-error" role="alert">
                {error}
              </div>
            ) : null}
            <div className="dialog-actions">
              <button
                type="button"
                className="btn btn-ghost"
                disabled={submitting}
                onClick={closeDialog}
              >
                Hủy
              </button>
              <button
                type="button"
                className="btn"
                disabled={submitting}
                onClick={() => void runAction()}
              >
                {submitting ? "Đang xử lý…" : dialogCopy.cta}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
