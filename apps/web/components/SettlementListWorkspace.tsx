"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import { DrawerTabs } from "./list/DrawerTabs";
import {
  allocationStatusLabel,
  settlementBillLinkLabel,
  type CollectionItem,
  type PaymentItem,
} from "@/lib/settlements";
import { formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";

type Row = PaymentItem | CollectionItem;

type Props = {
  terms: TerminologyMap;
  items: Row[];
  kind: "payment" | "collection";
  billLabel: string;
  unappliedLabel: string;
  availableLabel: string;
};

function allocatedPct(row: Row): number {
  if (row.amount <= 0) return 0;
  return Math.min(100, Math.max(0, (row.allocatedAmount / row.amount) * 100));
}

export function SettlementListWorkspace({
  terms,
  items,
  kind,
  billLabel,
  unappliedLabel,
  availableLabel,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [tab, setTab] = useState("overview");
  const selected = useMemo(
    () => items.find((r) => r.id === selectedId) ?? null,
    [items, selectedId]
  );
  const close = useCallback(() => {
    setSelectedId(null);
    setTab("overview");
  }, []);
  const select = useCallback((id: string) => {
    setSelectedId(id);
    setTab("overview");
  }, []);

  const detailHref = (id: string) =>
    kind === "payment" ? `/settlements/payments/${id}` : `/settlements/collections/${id}`;

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Ngày</th>
              <th scope="col" className="num">
                Số tiền
              </th>
              <th scope="col" className="num">
                {unappliedLabel}
              </th>
              <th scope="col" className="num">
                {availableLabel}
              </th>
              <th scope="col">Phân bổ</th>
              <th scope="col">{billLabel}</th>
              <th scope="col">Tham chiếu</th>
              <th scope="col">
                <span className="sr-only">Mở</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((row) => {
              const active = selectedId === row.id;
              const pct = allocatedPct(row);
              return (
                <tr
                  key={row.id}
                  className={active ? "row-selected" : undefined}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
                  onClick={() => select(row.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      select(row.id);
                    }
                  }}
                >
                  <td>
                    <button
                      type="button"
                      className="row-link"
                      style={{
                        background: "none",
                        border: "none",
                        padding: 0,
                        font: "inherit",
                        cursor: "pointer",
                      }}
                      onClick={(e) => {
                        e.stopPropagation();
                        select(row.id);
                      }}
                    >
                      {row.valueDate}
                    </button>
                  </td>
                  <td className="num">{formatMoney(row.amount, row.currencyCode)}</td>
                  <td className="num">
                    {formatMoney(row.unappliedAmount, row.currencyCode)}
                  </td>
                  <td className="num">
                    {formatMoney(row.availableToAllocate, row.currencyCode)}
                  </td>
                  <td>
                    <span className="alloc-progress" aria-label={`${pct.toFixed(0)}% đã phân bổ`}>
                      <span className="alloc-progress-track">
                        <span
                          className="alloc-progress-fill"
                          style={{ width: `${pct}%` }}
                        />
                      </span>
                      <span className="alloc-progress-pct">{pct.toFixed(0)}%</span>
                    </span>
                  </td>
                  <td>
                    {row.billId ? (
                      <Link
                        className="row-link"
                        href={`/bills/${row.billId}`}
                        onClick={(e) => e.stopPropagation()}
                      >
                        {settlementBillLinkLabel(row.billId, row.billNo, billLabel)}
                      </Link>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td>{row.referenceNo ?? "—"}</td>
                  <td>
                    <Link
                      className="btn btn-ghost btn-sm"
                      href={detailHref(row.id)}
                      onClick={(e) => e.stopPropagation()}
                    >
                      Hồ sơ
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <DetailDrawer
        open={Boolean(selected)}
        onClose={close}
        title={selected ? formatMoney(selected.amount, selected.currencyCode) : null}
        subtitle={selected ? selected.valueDate : null}
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={detailHref(selected.id)}>
                Mở hồ sơ / phân bổ
              </Link>
              {selected.billId ? (
                <Link className="btn btn-sm btn-ghost" href={`/bills/${selected.billId}`}>
                  {billLabel}
                </Link>
              ) : null}
            </div>
          ) : null
        }
      >
        {selected ? (
          <>
            <DrawerTabs
              tabs={[
                { id: "overview", label: "Tổng quan" },
                {
                  id: "allocations",
                  label: "Phân bổ",
                  badge: selected.allocations.length || undefined,
                },
              ]}
              activeId={tab}
              onChange={setTab}
            />
            {tab === "overview" ? (
              <div className="stack">
                <span
                  className="alloc-progress"
                  aria-label={`${allocatedPct(selected).toFixed(0)}% đã phân bổ`}
                >
                  <span className="alloc-progress-track">
                    <span
                      className="alloc-progress-fill"
                      style={{ width: `${allocatedPct(selected)}%` }}
                    />
                  </span>
                  <span className="alloc-progress-pct">
                    {allocatedPct(selected).toFixed(0)}% đã phân bổ
                  </span>
                </span>
                <dl className="metric-grid" style={{ marginTop: "0.75rem" }}>
                  <div>
                    <dt>Số tiền</dt>
                    <dd>{formatMoney(selected.amount, selected.currencyCode)}</dd>
                  </div>
                  <div>
                    <dt>Đã áp dụng</dt>
                    <dd>{formatMoney(selected.appliedAmount, selected.currencyCode)}</dd>
                  </div>
                  <div>
                    <dt>Đã phân bổ</dt>
                    <dd>{formatMoney(selected.allocatedAmount, selected.currencyCode)}</dd>
                  </div>
                  <div>
                    <dt>{unappliedLabel}</dt>
                    <dd>{formatMoney(selected.unappliedAmount, selected.currencyCode)}</dd>
                  </div>
                  <div>
                    <dt>{availableLabel}</dt>
                    <dd>
                      {formatMoney(selected.availableToAllocate, selected.currencyCode)}
                    </dd>
                  </div>
                  <div>
                    <dt>Tham chiếu</dt>
                    <dd>{selected.referenceNo ?? "—"}</dd>
                  </div>
                </dl>
              </div>
            ) : null}
            {tab === "allocations" ? (
              selected.allocations.length === 0 ? (
                <p className="empty-state" role="status">
                  Chưa có phân bổ. Mở hồ sơ đầy đủ để tạo phân bổ nháp.
                </p>
              ) : (
                <ul className="stack-list">
                  {selected.allocations.map((a) => (
                    <li key={a.id}>
                      {formatMoney(a.amount, a.currencyCode)} ·{" "}
                      {allocationStatusLabel(terms, a.allocationStatus)}
                    </li>
                  ))}
                </ul>
              )
            ) : null}
          </>
        ) : null}
      </DetailDrawer>
    </>
  );
}
