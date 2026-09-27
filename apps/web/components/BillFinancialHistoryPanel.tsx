"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApArLedgerTable } from "./ApArLedgerPanel";
import { apArLedgerHref, type ApArLedger } from "@/lib/ap-ar-ledger";
import { formatMoney } from "@/lib/money";

type Props = {
  billId: string;
  title?: string;
};

/** Bill history = AR/AP ledger events on this Bill (recognition, allocation, adjustment, write-off, reversals). */
export function BillFinancialHistoryPanel({ billId, title = "Lịch sử công nợ" }: Props) {
  const [ledgers, setLedgers] = useState<ApArLedger[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`/bff/bills/${billId}/financial-history`, {
        headers: { Accept: "application/json" },
        cache: "no-store",
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Không tải được lịch sử công nợ của Bill.");
        setLedgers(null);
        return;
      }
      setLedgers((await res.json()) as ApArLedger[]);
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
      setLedgers(null);
    } finally {
      setLoading(false);
    }
  }, [billId]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <section className="panel-section" aria-labelledby={`bill-history-${billId}`}>
      <h2 id={`bill-history-${billId}`} className="section-title">
        {title}
      </h2>
      {loading ? (
        <p className="muted">Đang tải lịch sử công nợ…</p>
      ) : error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : !ledgers || ledgers.length === 0 ? (
        <div className="empty-state" role="status">
          Bill chưa có khoản phải thu / phải trả được ghi nhận.
        </div>
      ) : (
        <div className="stack">
          {ledgers.map((ledger) => {
            const kind = ledger.kind === "ar" ? "receivable" : "payable";
            return (
              <div key={ledger.accountId}>
                <div className="toolbar-row" style={{ justifyContent: "space-between" }}>
                  <h3 className="section-title sm" style={{ margin: 0 }}>
                    {ledger.kind === "ar" ? "Khoản phải thu" : "Khoản phải trả"} · số dư{" "}
                    {formatMoney(ledger.currentOutstanding, ledger.currencyCode)}
                  </h3>
                  <Link
                    className="btn btn-ghost btn-sm"
                    href={apArLedgerHref(kind, ledger.accountId, ledger.billId)}
                  >
                    Mở sổ công nợ
                  </Link>
                </div>
                <ApArLedgerTable kind={kind} ledger={ledger} reverse={null} compact />
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
