"use client";

import Link from "next/link";
import { Fragment, useCallback, useEffect, useId, useState } from "react";
import {
  apArLedgerHref,
  formatSignedMoney,
  mismatchCauseLabel,
  type ApArBalanceMismatch,
  type ApArBalanceReconciliation,
} from "@/lib/ap-ar-ledger";
import { withRowVersion } from "@/lib/idempotency";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { BalanceImpact } from "./BalanceImpact";
import { ResponsiveData } from "./list/ResponsiveData";
import { TxnItem, TxnList } from "./list/TxnList";

const kindLabel = (kind: "ar" | "ap") => (kind === "ar" ? "Phải thu" : "Phải trả");

/** FIN-DATA-01: inventory of AR/AP balances that differ from the ledger + controlled, audited correction. */
export function ApArReconciliationWorkspace() {
  const [data, setData] = useState<ApArBalanceReconciliation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/bff/ap-ar/balance-reconciliation", {
        headers: { Accept: "application/json" },
        cache: "no-store",
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(
          body.message ||
            (res.status === 403
              ? "Bạn chưa được cấp quyền Đối soát số dư công nợ."
              : "Không tải được danh sách đối soát.")
        );
        setData(null);
        return;
      }
      setData((await res.json()) as ApArBalanceReconciliation);
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
      setData(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading && !data) return <p className="muted">Đang kiểm tra số dư công nợ…</p>;
  if (error) {
    return (
      <div className="alert alert-error" role="alert">
        {error}
      </div>
    );
  }
  if (!data) return null;

  const correctable = data.items.filter((i) => i.correctable).length;
  const manual = data.items.length - correctable;
  const toggle = (id: string) => setExpanded((cur) => (cur === id ? null : id));
  const onCorrected = (message: string) => {
    setNotice(message);
    setExpanded(null);
    void load();
  };

  const table = (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">Loại</th>
            <th scope="col">Bill</th>
            <th scope="col" className="num">
              Số dư hiện tại
            </th>
            <th scope="col" className="num">
              Số dư theo sổ
            </th>
            <th scope="col" className="num">
              Chênh lệch
            </th>
            <th scope="col">Nguyên nhân</th>
            <th scope="col">
              <span className="sr-only">Thao tác</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((item) => {
            const open = expanded === item.accountId;
            return (
              <Fragment key={item.accountId}>
                <tr className={open ? "row-selected" : undefined}>
                  <td>{kindLabel(item.kind)}</td>
                  <td>{item.billNo?.trim() || "—"}</td>
                  <td className="num">{formatMoney(item.currentOutstanding, item.currencyCode)}</td>
                  <td className="num">{formatMoney(item.ledgerBalance, item.currencyCode)}</td>
                  <td className="num neg">{formatSignedMoney(item.difference, item.currencyCode)}</td>
                  <td>{mismatchCauseLabel(item.cause)}</td>
                  <td>
                    <div className="row-actions">
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        aria-expanded={open}
                        onClick={() => toggle(item.accountId)}
                      >
                        {open ? "Thu gọn" : "Chi tiết"}
                      </button>
                      {item.correctable ? (
                        <CorrectButton item={item} onDone={onCorrected} />
                      ) : null}
                    </div>
                  </td>
                </tr>
                {open ? (
                  <tr>
                    <td colSpan={7}>
                      <MismatchDetail item={item} />
                    </td>
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
    <TxnList label="Khoản lệch số dư">
      {data.items.map((item) => {
        const open = expanded === item.accountId;
        return (
          <TxnItem
            key={item.accountId}
            selected={open}
            title={`${kindLabel(item.kind)} · ${item.billNo?.trim() || "chưa gắn Bill"}`}
            amount={formatSignedMoney(item.difference, item.currencyCode)}
            amountTone="neg"
            sub={
              <>
                Hiện tại{" "}
                <span className="num">{formatMoney(item.currentOutstanding, item.currencyCode)}</span>{" "}
                · theo sổ{" "}
                <span className="num">{formatMoney(item.ledgerBalance, item.currencyCode)}</span>
              </>
            }
            meta={mismatchCauseLabel(item.cause)}
            actions={
              <>
                <button
                  type="button"
                  className="btn btn-ghost btn-sm"
                  aria-expanded={open}
                  onClick={() => toggle(item.accountId)}
                >
                  {open ? "Thu gọn" : "Chi tiết"}
                </button>
                {item.correctable ? <CorrectButton item={item} onDone={onCorrected} /> : null}
              </>
            }
            detail={open ? <MismatchDetail item={item} /> : null}
          />
        );
      })}
    </TxnList>
  );

  return (
    <div className="stack">
      <dl className="metric-grid">
        <div>
          <dt>Đã kiểm tra</dt>
          <dd>
            {data.includesReceivables ? `${data.checkedReceivables} phải thu` : null}
            {data.includesReceivables && data.includesPayables ? " · " : null}
            {data.includesPayables ? `${data.checkedPayables} phải trả` : null}
          </dd>
        </div>
        <div>
          <dt>Khoản lệch</dt>
          <dd className={data.items.length > 0 ? "neg" : undefined}>{data.items.length}</dd>
        </div>
        <div>
          <dt>Đối soát được</dt>
          <dd>{correctable}</dd>
        </div>
        <div>
          <dt>Cần kiểm tra thủ công</dt>
          <dd className={manual > 0 ? "neg" : undefined}>{manual}</dd>
        </div>
      </dl>
      <p className="meta-line muted">
        Kiểm tra lúc {formatDateTimeVi(data.generatedAt)}
        {!data.includesReceivables ? " · không gồm phải thu (chưa có quyền xem doanh thu)" : ""}
        {!data.includesPayables ? " · không gồm phải trả (chưa có quyền xem chi phí)" : ""}{" "}
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => void load()} disabled={loading}>
          {loading ? "Đang kiểm tra…" : "Kiểm tra lại"}
        </button>
      </p>

      {notice ? (
        <div className="alert alert-success" role="status">
          {notice}
        </div>
      ) : null}

      {data.items.length === 0 ? (
        <div className="empty-state" role="status">
          Không có khoản lệch — số dư hiện tại khớp sổ công nợ.
        </div>
      ) : (
        <ResponsiveData table={table} list={list} />
      )}

      <section aria-labelledby="apar-rec-history">
        <h2 id="apar-rec-history" className="section-title">
          Lịch sử đối soát
        </h2>
        {data.recentCorrections.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có lần đối soát nào.
          </div>
        ) : (
          <TxnList label="Lịch sử đối soát">
            {data.recentCorrections.map((c) => (
              <TxnItem
                key={c.auditEventId}
                title={`Đối soát số dư ${kindLabel(c.kind).toLowerCase()}`}
                sub={
                  c.outstandingBefore != null && c.outstandingAfter != null && c.currencyCode ? (
                    <>
                      <span className="num">{formatMoney(c.outstandingBefore, c.currencyCode)}</span> →{" "}
                      <span className="num">{formatMoney(c.outstandingAfter, c.currencyCode)}</span>
                    </>
                  ) : null
                }
                meta={[formatDateTimeVi(c.occurredAt), c.actorName?.trim()].filter(Boolean).join(" · ")}
                reason={c.reason}
                actions={
                  <Link
                    className="btn btn-ghost btn-sm"
                    href={apArLedgerHref(c.kind === "ar" ? "receivable" : "payable", c.accountId)}
                  >
                    Mở sổ công nợ
                  </Link>
                }
              />
            ))}
          </TxnList>
        )}
      </section>
    </div>
  );
}

function MismatchDetail({ item }: { item: ApArBalanceMismatch }) {
  const kind = item.kind === "ar" ? "receivable" : "payable";
  const cashBase = item.kind === "ar" ? "/settlements/collections" : "/settlements/payments";
  const cashLabel = item.kind === "ar" ? "phiếu thu" : "phiếu chi";
  return (
    <div className="stack">
      <dl className="metric-grid">
        <div>
          <dt>Đã tất toán (lưu)</dt>
          <dd>{formatMoney(item.storedSettledAmount, item.currencyCode)}</dd>
        </div>
        <div>
          <dt>Đã tất toán (theo phân bổ đã chốt)</dt>
          <dd>{formatMoney(item.derivedSettledAmount, item.currencyCode)}</dd>
        </div>
        <div>
          <dt>Điều chỉnh (lưu)</dt>
          <dd>{formatMoney(item.storedAdjustmentAmount, item.currencyCode)}</dd>
        </div>
        <div>
          <dt>Điều chỉnh (theo bút toán)</dt>
          <dd>{formatMoney(item.derivedAdjustmentAmount, item.currencyCode)}</dd>
        </div>
      </dl>
      {item.legacyAllocations.length > 0 ? (
        <div>
          <h3 className="section-title sm">Phân bổ khác tiền tệ đã hủy trước bản sửa</h3>
          <TxnList label="Phân bổ liên quan">
            {item.legacyAllocations.map((l) => (
              <TxnItem
                key={l.allocationId}
                title={`${cashLabel[0].toUpperCase()}${cashLabel.slice(1)} ${l.cashReference?.trim() || ""}`.trim()}
                amount={formatMoney(l.cashAmount, l.cashCurrencyCode)}
                sub={
                  <>
                    Quy đổi khi chốt{" "}
                    <span className="num">{formatMoney(l.settledAmount, item.currencyCode)}</span>
                  </>
                }
                meta={`Hủy phân bổ ${formatDateTimeVi(l.reversedAt)}`}
                actions={
                  <Link className="btn btn-ghost btn-sm" href={`${cashBase}/${l.cashId}`}>
                    Mở {cashLabel}
                  </Link>
                }
              />
            ))}
          </TxnList>
        </div>
      ) : (
        <p className="note">
          Không tìm thấy giao dịch giải thích chênh lệch. Không tự điều chỉnh — đối chiếu sổ công nợ và
          nhật ký, rồi xử lý bằng điều chỉnh có lý do nếu cần.
        </p>
      )}
      <div className="cta-row">
        <Link className="btn btn-ghost btn-sm" href={apArLedgerHref(kind, item.accountId, item.billId)}>
          Mở sổ công nợ
        </Link>
      </div>
    </div>
  );
}

function CorrectButton({
  item,
  onDone,
}: {
  item: ApArBalanceMismatch;
  onDone: (message: string) => void;
}) {
  const titleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const base = item.kind === "ar" ? "/bff/accounts-receivable" : "/bff/accounts-payable";

  const submit = async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do đối soát.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`${base}/${item.accountId}/settlement-correction`, {
        method: "POST",
        headers: withRowVersion(
          { "Content-Type": "application/json", Accept: "application/json" },
          item.rowVersion
        ),
        body: JSON.stringify({ reason: trimmed }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(
          body.message ||
            (res.status === 409
              ? "Dữ liệu đã thay đổi hoặc không còn lệch. Kiểm tra lại danh sách."
              : "Đối soát thất bại.")
        );
        return;
      }
      setOpen(false);
      setReason("");
      onDone(
        `Đã đối soát ${kindLabel(item.kind).toLowerCase()} ${item.billNo?.trim() || ""}: số dư ${formatMoney(
          item.currentOutstanding,
          item.currencyCode
        )} → ${formatMoney(item.ledgerBalance, item.currencyCode)}.`
      );
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  };

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
        Đối soát
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
            <h2 id={titleId}>Đối soát số dư công nợ</h2>
            <p>
              Đưa số dư hiện tại về đúng số dư dựng từ sổ công nợ. Phân bổ, phiếu thu/chi và bút toán
              gốc giữ nguyên; lần đối soát được ghi nhật ký kèm lý do.
            </p>
            <BalanceImpact
              before={item.currentOutstanding}
              after={item.ledgerBalance}
              currencyCode={item.currencyCode}
            />
            <div className="field">
              <label htmlFor={`rec-reason-${item.accountId}`}>Lý do đối soát</label>
              <input
                id={`rec-reason-${item.accountId}`}
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={1024}
                disabled={submitting}
                required
                placeholder="Ví dụ: hủy phân bổ VND trước bản sửa 27/09"
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
              <button type="button" className="btn" onClick={() => void submit()} disabled={submitting}>
                {submitting ? "Đang ghi…" : "Xác nhận đối soát"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
