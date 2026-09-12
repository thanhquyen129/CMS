"use client";

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
  costs: CostListItem[] | null;
  costsError: string | null;
  revenues: RevenueListItem[] | null;
  revenuesError: string | null;
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
  costs,
  costsError,
  revenues,
  revenuesError,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState<Record<string, true>>({});
  const [isPending, startTransition] = useTransition();
  const [submitting, setSubmitting] = useState(false);

  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");

  const closeDialog = useCallback(() => {
    if (submitting) return;
    setPending(null);
  }, [submitting]);

  const openAction = useCallback((p: PendingAction) => {
    setError(null);
    setPending(p);
  }, []);

  const runAction = useCallback(async () => {
    if (!pending) return;
    setSubmitting(true);
    setError(null);

    const base =
      pending.kind === "cost"
        ? `/bff/costs/${pending.id}`
        : `/bff/revenues/${pending.id}`;
    const path =
      pending.action === "confirm" ? `${base}/confirm` : `${base}/actualize`;

    try {
      const res = await fetch(path, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({}),
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
        if (res.status === 403) {
          setBlocked((prev) => ({
            ...prev,
            [`${pending.kind}:${pending.action}:${pending.id}`]: true,
          }));
          setPending(null);
        }
        return;
      }

      setPending(null);
      startTransition(() => {
        router.refresh();
      });
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [pending, router]);

  const isBlocked = (kind: Kind, action: Action, id: string) =>
    Boolean(blocked[`${kind}:${action}:${id}`]);

  const dialogCopy =
    pending?.action === "confirm"
      ? {
          title: `Xác nhận ${pending.kind === "cost" ? costLabel : revenueLabel}`,
          body: `Chuyển từ ${expected} → ${confirmed}. Số tiền ${formatMoney(pending.amount, pending.currencyCode)} sẽ ghi vào lớp ${confirmed}. Lớp ${expected} được giữ nguyên — không ghi đè im lặng.`,
          cta: `Xác nhận ${pending.kind === "cost" ? costLabel : revenueLabel}`,
        }
      : pending
        ? {
            title: `Ghi nhận ${actual} — ${pending.kind === "cost" ? costLabel : revenueLabel}`,
            body: `Chuyển từ ${confirmed} → ${actual}. Số tiền ${formatMoney(pending.amount, pending.currencyCode)}. Các lớp ${expected}/${confirmed} được giữ nguyên.`,
            cta: `Ghi nhận ${actual}`,
          }
        : null;

  return (
    <div className="maturity-actions">
      <h2 className="section-title">
        Xác nhận {costLabel} / {revenueLabel}
      </h2>
      <p className="muted small">
        Một thao tác mỗi dòng: xác nhận ({expected} → {confirmed}) hoặc ghi nhận{" "}
        {actual} ({confirmed} → {actual}). Không ghi đè lớp trưởng thành trước đó.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <h3 className="section-title sm">{costLabel}</h3>
      {costsError ? (
        <div className="alert alert-error" role="alert">
          {costsError}
        </div>
      ) : !costs || costs.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có dòng {costLabel.toLowerCase()} gắn Bill này.
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
                        <span className="muted small">Không có quyền</span>
                      ) : c.financialMaturity?.toLowerCase() === "actual" ? (
                        <span className="muted small">{actual}</span>
                      ) : c.recordStatus !== "active" ? (
                        <span className="muted small">Không hiệu lực</span>
                      ) : (
                        <span className="muted small">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <h3 className="section-title sm">{revenueLabel}</h3>
      {revenuesError ? (
        <div className="alert alert-error" role="alert">
          {revenuesError}
        </div>
      ) : !revenues || revenues.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có dòng {revenueLabel.toLowerCase()} gắn Bill này.
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
                        <span className="muted small">Không có quyền</span>
                      ) : r.financialMaturity?.toLowerCase() === "actual" ? (
                        <span className="muted small">{actual}</span>
                      ) : r.recordStatus !== "active" ? (
                        <span className="muted small">Không hiệu lực</span>
                      ) : (
                        <span className="muted small">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

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
