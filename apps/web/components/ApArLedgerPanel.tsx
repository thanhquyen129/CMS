"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Fragment, useCallback, useEffect, useId, useState } from "react";
import {
  cashDetailHref,
  formatSignedMoney,
  ledgerEntryLabel,
  ledgerStatusLabel,
  type ApArLedger,
  type ApArLedgerEntry,
  type ApArLedgerKind,
} from "@/lib/ap-ar-ledger";
import { withRowVersion } from "@/lib/idempotency";
import { formatDateTimeVi, formatDateVi, formatMoney } from "@/lib/money";
import { BalanceImpact } from "./BalanceImpact";
import { ResponsiveData } from "./list/ResponsiveData";
import { TxnItem, TxnList } from "./list/TxnList";

type LoaderProps = {
  kind: ApArLedgerKind;
  accountId: string;
  /** AR/AP row version — required to reverse a write-off from this panel. */
  rowVersion?: string | null;
  canReverse?: boolean;
  layout?: "auto" | "list";
};

/** Loads and renders the AR/AP ledger ("Sổ công nợ") for one account. */
export function ApArLedgerPanel({
  kind,
  accountId,
  rowVersion,
  canReverse = true,
  layout = "auto",
}: LoaderProps) {
  const router = useRouter();
  const [ledger, setLedger] = useState<ApArLedger | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const base = kind === "receivable" ? "/bff/accounts-receivable" : "/bff/accounts-payable";

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${base}/${accountId}/ledger`, {
        headers: { Accept: "application/json" },
        cache: "no-store",
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Không tải được sổ công nợ.");
        setLedger(null);
        return;
      }
      setLedger((await res.json()) as ApArLedger);
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
      setLedger(null);
    } finally {
      setLoading(false);
    }
  }, [accountId, base]);

  useEffect(() => {
    void load();
  }, [load]);

  const onReversed = useCallback(() => {
    void load();
    router.refresh();
  }, [load, router]);

  if (loading) return <p className="muted">Đang tải sổ công nợ…</p>;
  if (error) {
    return (
      <div className="alert alert-error" role="alert">
        {error}
      </div>
    );
  }
  if (!ledger) return null;

  return (
    <ApArLedgerTable
      kind={kind}
      ledger={ledger}
      layout={layout}
      reverse={
        canReverse
          ? { base, accountId, rowVersion: rowVersion ?? null, onDone: onReversed }
          : null
      }
    />
  );
}

type ReverseConfig = {
  base: string;
  accountId: string;
  rowVersion: string | null;
  onDone: () => void;
};

type TableProps = {
  kind: ApArLedgerKind;
  ledger: ApArLedger;
  reverse: ReverseConfig | null;
  /** "list" inside drawers / side panels; "auto" switches on container width. */
  layout?: "auto" | "list";
};

function statusPillClass(entry: ApArLedgerEntry): string {
  return entry.status === "reversed" ? "status-pill status-canceled" : "status-pill status-confirmed";
}

export function ApArLedgerTable({ kind, ledger, reverse, layout = "auto" }: TableProps) {
  const [expanded, setExpanded] = useState<string | null>(null);
  const byId = new Map(ledger.entries.map((e) => [e.id, e]));
  const toggle = (id: string) => setExpanded((cur) => (cur === id ? null : id));
  const detail = (entry: ApArLedgerEntry) => (
    <LedgerEntryDetail
      entry={entry}
      kind={kind}
      related={byId}
      onJump={setExpanded}
      reverse={reverse}
      currentOutstanding={ledger.currentOutstanding}
    />
  );

  const table = (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">Ngày</th>
            <th scope="col">Loại</th>
            <th scope="col" className="num">
              Số tiền
            </th>
            <th scope="col" className="num">
              Số dư sau
            </th>
            <th scope="col">Trạng thái</th>
            <th scope="col">Người thực hiện</th>
            <th scope="col">Lý do</th>
            <th scope="col">
              <span className="sr-only">Mở</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {ledger.entries.map((entry) => {
            const open = expanded === entry.id;
            return (
              <Fragment key={entry.id}>
                <tr className={open ? "row-selected" : undefined}>
                  <td style={{ whiteSpace: "nowrap" }}>{formatDateVi(entry.businessDate)}</td>
                  <td>{ledgerEntryLabel(entry, kind)}</td>
                  <td className="num">{formatSignedMoney(entry.amount, entry.currencyCode)}</td>
                  <td className="num">{formatMoney(entry.balanceAfter, entry.currencyCode)}</td>
                  <td>
                    <span className={statusPillClass(entry)}>{ledgerStatusLabel(entry)}</span>
                  </td>
                  <td>{entry.actorName?.trim() || "—"}</td>
                  <td>
                    <span className="txn-reason" title={entry.reason?.trim() || undefined}>
                      {entry.reason?.trim() || "—"}
                    </span>
                  </td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      aria-expanded={open}
                      onClick={() => toggle(entry.id)}
                    >
                      {open ? "Thu gọn" : "Mở"}
                    </button>
                  </td>
                </tr>
                {open ? (
                  <tr>
                    <td colSpan={8}>{detail(entry)}</td>
                  </tr>
                ) : null}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );

  const list = (
    <TxnList label="Biến động công nợ">
      {ledger.entries.map((entry) => {
        const open = expanded === entry.id;
        return (
          <TxnItem
            key={entry.id}
            selected={open}
            title={ledgerEntryLabel(entry, kind)}
            amount={formatSignedMoney(entry.amount, entry.currencyCode)}
            amountTone={entry.amount < 0 ? "neg" : null}
            sub={
              <>
                Số dư sau{" "}
                <span className="num">{formatMoney(entry.balanceAfter, entry.currencyCode)}</span>
              </>
            }
            status={<span className={statusPillClass(entry)}>{ledgerStatusLabel(entry)}</span>}
            meta={[formatDateVi(entry.businessDate), entry.actorName?.trim()]
              .filter(Boolean)
              .join(" · ")}
            reason={entry.reason}
            actions={
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                aria-expanded={open}
                onClick={() => toggle(entry.id)}
              >
                {open ? "Thu gọn" : "Mở"}
              </button>
            }
            detail={open ? detail(entry) : null}
          />
        );
      })}
    </TxnList>
  );

  return (
    <div>
      {!ledger.reconciled ? (
        <div className="alert alert-error" role="alert">
          Số dư theo sổ ({formatMoney(ledger.ledgerBalance, ledger.currencyCode)}) lệch số dư
          hiện tại ({formatMoney(ledger.currentOutstanding, ledger.currencyCode)}). Tạm dừng tất
          toán khoản này và{" "}
          <Link className="row-link" href="/ap-ar/reconciliation">
            mở Đối soát số dư công nợ
          </Link>
          .
        </div>
      ) : null}
      {ledger.entries.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có biến động công nợ.
        </div>
      ) : (
        <ResponsiveData layout={layout} table={table} list={list} />
      )}
    </div>
  );
}

function LedgerEntryDetail({
  entry,
  kind,
  related,
  onJump,
  reverse,
  currentOutstanding,
}: {
  entry: ApArLedgerEntry;
  kind: ApArLedgerKind;
  related: Map<string, ApArLedgerEntry>;
  onJump: (id: string) => void;
  reverse: ReverseConfig | null;
  currentOutstanding: number;
}) {
  const cashHref = cashDetailHref(entry, kind);
  const cashLabel = kind === "receivable" ? "phiếu thu" : "phiếu chi";
  const original = entry.reversesEntryId ? related.get(entry.reversesEntryId) : undefined;
  const reversal = entry.reversedByEntryId ? related.get(entry.reversedByEntryId) : undefined;
  const canReverseWriteOff =
    reverse !== null && entry.entryType === "write_off" && entry.status !== "reversed";

  return (
    <div className="stack">
      <dl className="metric-grid">
        <div>
          <dt>Mã giao dịch</dt>
          <dd>
            <code className="mono-id">{entry.sourceId}</code>
          </dd>
        </div>
        <div>
          <dt>Ngày nghiệp vụ</dt>
          <dd>{formatDateVi(entry.businessDate)}</dd>
        </div>
        <div>
          <dt>Thời điểm ghi</dt>
          <dd>{formatDateTimeVi(entry.occurredAt)}</dd>
        </div>
        <div>
          <dt>Số dư trước → sau</dt>
          <dd>
            {formatMoney(entry.balanceBefore, entry.currencyCode)} →{" "}
            {formatMoney(entry.balanceAfter, entry.currencyCode)}
          </dd>
        </div>
        <div>
          <dt>Người thực hiện</dt>
          <dd>{entry.actorName?.trim() || "—"}</dd>
        </div>
        <div>
          <dt>Lý do</dt>
          <dd>{entry.reason?.trim() || "—"}</dd>
        </div>
        {entry.cashAmount != null && entry.cashCurrencyCode ? (
          <div>
            <dt>Số tiền trên {cashLabel}</dt>
            <dd>{formatMoney(entry.cashAmount, entry.cashCurrencyCode)}</dd>
          </div>
        ) : null}
        {entry.sourceLabel ? (
          <div>
            <dt>Số tham chiếu</dt>
            <dd>{entry.sourceLabel}</dd>
          </div>
        ) : null}
      </dl>
      <div className="cta-row">
        {cashHref ? (
          <Link className="btn btn-ghost btn-sm" href={cashHref}>
            Mở {cashLabel}
          </Link>
        ) : null}
        {original ? (
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => onJump(original.id)}>
            Xem bút toán gốc ({ledgerEntryLabel(original, kind).toLowerCase()})
          </button>
        ) : null}
        {reversal ? (
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => onJump(reversal.id)}>
            Xem {ledgerEntryLabel(reversal, kind).toLowerCase()}
          </button>
        ) : null}
        {canReverseWriteOff && reverse ? (
          <ReverseWriteOffButton
            entry={entry}
            reverse={reverse}
            currentOutstanding={currentOutstanding}
          />
        ) : null}
      </div>
    </div>
  );
}

function ReverseWriteOffButton({
  entry,
  reverse,
  currentOutstanding,
}: {
  entry: ApArLedgerEntry;
  reverse: ReverseConfig;
  currentOutstanding: number;
}) {
  const titleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const restore = Math.abs(entry.amount);

  const submit = useCallback(async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do hoàn tác xóa nợ.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(
        `${reverse.base}/${reverse.accountId}/write-offs/${entry.sourceId}/reverse`,
        {
          method: "POST",
          headers: withRowVersion(
            { "Content-Type": "application/json", Accept: "application/json" },
            reverse.rowVersion
          ),
          body: JSON.stringify({ reason: trimmed }),
        }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(
          body.message ||
            (res.status === 409
              ? "Không hoàn tác được (đã hoàn tác hoặc dữ liệu đã đổi). Tải lại trang."
              : "Hoàn tác xóa nợ thất bại.")
        );
        return;
      }
      setOpen(false);
      setReason("");
      reverse.onDone();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [entry.sourceId, reason, reverse]);

  return (
    <>
      <button
        type="button"
        className="btn btn-sm"
        onClick={() => {
          setError(null);
          setReason("");
          setOpen(true);
        }}
      >
        Hoàn tác xóa nợ
      </button>
      {open ? (
        <div
          className="dialog-backdrop"
          role="presentation"
          onClick={(e) => {
            if (e.target === e.currentTarget && !submitting) setOpen(false);
          }}
        >
          <div className="dialog" role="dialog" aria-modal="true" aria-labelledby={titleId}>
            <h2 id={titleId}>Hoàn tác xóa nợ</h2>
            <p>
              Ghi bút toán bù{" "}
              <strong>{formatSignedMoney(restore, entry.currencyCode)}</strong> vào số dư công nợ.
              Bút toán xóa nợ gốc được giữ nguyên và liên kết với bút toán hoàn tác; Doanh thu và
              Lợi nhuận không đổi.
            </p>
            <BalanceImpact
              before={currentOutstanding}
              after={currentOutstanding + restore}
              currencyCode={entry.currencyCode}
            />
            <div className="field">
              <label htmlFor={`rwo-reason-${entry.id}`}>Lý do hoàn tác</label>
              <input
                id={`rwo-reason-${entry.id}`}
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={1024}
                disabled={submitting}
                required
                placeholder="Ví dụ: khách xác nhận thanh toán phần còn lại"
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
                onClick={() => setOpen(false)}
                disabled={submitting}
              >
                Đóng
              </button>
              <button type="button" className="btn" onClick={submit} disabled={submitting}>
                {submitting ? "Đang ghi…" : "Xác nhận hoàn tác"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
