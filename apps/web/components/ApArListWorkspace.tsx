"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import { DrawerTabs } from "./list/DrawerTabs";
import { AdjustApArButton } from "./AdjustApArButton";
import { ReverseRecognizeButton } from "./ReverseRecognizeButton";
import { WriteOffButton } from "./WriteOffButton";
import {
  agingBucketLabel,
  settlementStatusLabel,
  type AccountsPayableItem,
  type AccountsReceivableItem,
} from "@/lib/ap-ar-shared";
import { formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";

type Row = AccountsPayableItem | AccountsReceivableItem;

type Props = {
  terms: TerminologyMap;
  items: Row[];
  kind: "payable" | "receivable";
  billLabel: string;
  outstandingLabel: string;
  agingLabel: string;
  showSettledAmount: boolean;
  cashLabel: string;
  cashCreateHref: string;
};

export function ApArListWorkspace({
  terms,
  items,
  kind,
  billLabel,
  outstandingLabel,
  agingLabel,
  showSettledAmount,
  cashLabel,
  cashCreateHref,
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

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">{billLabel}</th>
              <th scope="col" className="num">
                Đã ghi nhận
              </th>
              {showSettledAmount ? (
                <th scope="col" className="num">
                  Đã tất toán
                </th>
              ) : null}
              <th scope="col" className="num">
                {outstandingLabel}
              </th>
              <th scope="col">Trạng thái tất toán</th>
              <th scope="col">Hạn</th>
              <th scope="col">{agingLabel}</th>
              <th scope="col">
                <span className="sr-only">Mở</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((row) => {
              const active = selectedId === row.id;
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
                    {row.billId ? (
                      <Link
                        className="row-link"
                        href={`/bills/${row.billId}`}
                        onClick={(e) => e.stopPropagation()}
                      >
                        Mở {billLabel}
                      </Link>
                    ) : (
                      <span className="muted">—</span>
                    )}
                  </td>
                  <td className="num">
                    {formatMoney(row.recognizedAmount, row.currencyCode)}
                  </td>
                  {showSettledAmount ? (
                    <td className="num">
                      {formatMoney(row.finalizedSettledAmount, row.currencyCode)}
                    </td>
                  ) : null}
                  <td className="num">
                    {formatMoney(row.outstanding, row.currencyCode)}
                  </td>
                  <td>{settlementStatusLabel(terms, row.settlementStatus)}</td>
                  <td>{row.dueDate ?? "—"}</td>
                  <td>
                    {agingBucketLabel(row.agingBucket)}
                    {row.daysPastDue != null && row.daysPastDue > 0
                      ? ` · ${row.daysPastDue} ngày`
                      : ""}
                  </td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      onClick={(e) => {
                        e.stopPropagation();
                        select(row.id);
                      }}
                    >
                      Chi tiết
                    </button>
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
        title={
          selected
            ? `${billLabel} ${selected.billId ? selected.billId.slice(0, 8) + "…" : selected.id.slice(0, 8)}`
            : null
        }
        subtitle={
          selected ? settlementStatusLabel(terms, selected.settlementStatus) : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              {selected.outstanding > 0 ? (
                <Link className="btn btn-sm" href={cashCreateHref}>
                  Tạo {cashLabel.toLowerCase()}
                </Link>
              ) : null}
              {selected.billId ? (
                <Link
                  className="btn btn-sm btn-ghost"
                  href={`/bills/${selected.billId}`}
                >
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
                { id: "related", label: "Liên quan" },
              ]}
              activeId={tab}
              onChange={setTab}
            />
            {tab === "overview" ? (
              <dl className="metric-grid">
                <div>
                  <dt>Đã ghi nhận</dt>
                  <dd>
                    {formatMoney(selected.recognizedAmount, selected.currencyCode)}
                  </dd>
                </div>
                <div>
                  <dt>Đã tất toán</dt>
                  <dd>
                    {formatMoney(
                      selected.finalizedSettledAmount,
                      selected.currencyCode
                    )}
                  </dd>
                </div>
                <div>
                  <dt>{outstandingLabel}</dt>
                  <dd>{formatMoney(selected.outstanding, selected.currencyCode)}</dd>
                </div>
                <div>
                  <dt>Hạn</dt>
                  <dd>{selected.dueDate ?? "—"}</dd>
                </div>
                <div>
                  <dt>{agingLabel}</dt>
                  <dd>
                    {agingBucketLabel(selected.agingBucket)}
                    {selected.daysPastDue != null && selected.daysPastDue > 0
                      ? ` · ${selected.daysPastDue} ngày`
                      : ""}
                  </dd>
                </div>
              </dl>
            ) : null}
            {tab === "related" ? (
              <ul className="stack-list">
                {selected.billId ? (
                  <li>
                    <Link className="row-link" href={`/bills/${selected.billId}`}>
                      {billLabel} {selected.billId.slice(0, 8)}…
                    </Link>
                  </li>
                ) : (
                  <li className="muted">Chưa gắn {billLabel}.</li>
                )}
                <li>
                  <Link className="row-link" href={cashCreateHref}>
                    Tạo {cashLabel.toLowerCase()} và phân bổ
                  </Link>
                </li>
              </ul>
            ) : null}
            <div className="cta-row" style={{ marginTop: "1rem" }}>
              {selected.recordStatus.toLowerCase() === "active" ? (
                <AdjustApArButton
                  kind={kind}
                  accountsId={selected.id}
                  currencyCode={selected.currencyCode}
                  outstanding={selected.outstanding}
                  rowVersion={selected.rowVersion}
                />
              ) : null}
              {selected.outstanding > 0 ? (
                <WriteOffButton
                  terms={terms}
                  kind={kind}
                  accountsId={selected.id}
                  outstanding={selected.outstanding}
                  currencyCode={selected.currencyCode}
                  rowVersion={selected.rowVersion}
                />
              ) : null}
              <ReverseRecognizeButton
                terms={terms}
                kind={kind}
                accountsId={selected.id}
                outstanding={selected.outstanding}
                currencyCode={selected.currencyCode}
                settledAmount={selected.finalizedSettledAmount}
                recordStatus={selected.recordStatus}
                rowVersion={selected.rowVersion}
              />
            </div>
          </>
        ) : null}
      </DetailDrawer>
    </>
  );
}
