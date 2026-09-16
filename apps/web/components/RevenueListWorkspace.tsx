"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import { DrawerTabs } from "./list/DrawerTabs";
import {
  maturityLabelKey,
  type RevenueListItem,
} from "@/lib/costs-revenues";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  revenues: RevenueListItem[];
  billLabel: string;
  revenueLabel: string;
  expectedLabel: string;
  confirmedLabel: string;
  actualLabel: string;
};

function maturityVi(
  terms: TerminologyMap,
  maturity: string,
  fallbacks: Record<string, string>
) {
  return term(
    terms,
    maturityLabelKey(maturity),
    fallbacks[maturity?.toLowerCase()] ?? maturity
  );
}

export function RevenueListWorkspace({
  terms,
  revenues,
  billLabel,
  revenueLabel,
  expectedLabel,
  confirmedLabel,
  actualLabel,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [tab, setTab] = useState("overview");
  const selected = useMemo(
    () => revenues.find((r) => r.id === selectedId) ?? null,
    [revenues, selectedId]
  );
  const close = useCallback(() => {
    setSelectedId(null);
    setTab("overview");
  }, []);

  const fallbacks = {
    expected: expectedLabel,
    confirmed: confirmedLabel,
    actual: actualLabel,
  };

  const m = selected?.financialMaturity?.toLowerCase() ?? "";

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Loại</th>
              <th scope="col">{billLabel}</th>
              <th scope="col">Độ chín</th>
              <th scope="col" className="num">
                Số tiền
              </th>
              <th scope="col">Hiệu lực</th>
              <th scope="col">
                <span className="sr-only">Mở</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {revenues.map((r) => {
              const active = selectedId === r.id;
              return (
                <tr
                  key={r.id}
                  className={active ? "row-selected" : undefined}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
                  onClick={() => {
                    setSelectedId(r.id);
                    setTab("overview");
                  }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setSelectedId(r.id);
                      setTab("overview");
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
                        setSelectedId(r.id);
                        setTab("overview");
                      }}
                    >
                      {r.revenueTypeCode || r.id.slice(0, 8)}
                    </button>
                  </td>
                  <td>
                    <Link
                      className="row-link"
                      href={`/bills/${r.billId}`}
                      onClick={(e) => e.stopPropagation()}
                    >
                      {r.billId.slice(0, 8)}…
                    </Link>
                  </td>
                  <td>
                    <span
                      className={`maturity-pill maturity-${r.financialMaturity?.toLowerCase()}`}
                    >
                      {maturityVi(terms, r.financialMaturity, fallbacks)}
                    </span>
                  </td>
                  <td className="num">
                    {formatMoney(r.amount, r.currencyCode)}
                  </td>
                  <td>{formatDateTimeVi(r.effectiveDate)}</td>
                  <td>
                    <Link
                      className="btn btn-ghost btn-sm"
                      href={`/revenues/${r.id}`}
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
        title={
          selected
            ? `${revenueLabel} ${selected.revenueTypeCode || selected.id.slice(0, 8)}`
            : null
        }
        subtitle={
          selected ? (
            <span
              className={`maturity-pill maturity-${selected.financialMaturity?.toLowerCase()}`}
            >
              {maturityVi(terms, selected.financialMaturity, fallbacks)}
            </span>
          ) : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={`/revenues/${selected.id}`}>
                Mở hồ sơ đầy đủ
              </Link>
              <Link
                className="btn btn-sm btn-ghost"
                href={`/bills/${selected.billId}`}
              >
                {billLabel}
              </Link>
            </div>
          ) : null
        }
      >
        {selected ? (
          <>
            <DrawerTabs
              tabs={[
                { id: "overview", label: "Tổng quan" },
                { id: "maturity", label: "Độ chín" },
                { id: "related", label: "Liên quan" },
              ]}
              activeId={tab}
              onChange={setTab}
            />
            {tab === "overview" ? (
              <dl className="metric-grid">
                <div>
                  <dt>Loại</dt>
                  <dd>{selected.revenueTypeCode || "—"}</dd>
                </div>
                <div>
                  <dt>Số tiền hiện hành</dt>
                  <dd>{formatMoney(selected.amount, selected.currencyCode)}</dd>
                </div>
                <div>
                  <dt>Hiệu lực</dt>
                  <dd>{formatDateTimeVi(selected.effectiveDate)}</dd>
                </div>
                <div>
                  <dt>Trạng thái ghi</dt>
                  <dd>{selected.recordStatus}</dd>
                </div>
              </dl>
            ) : null}
            {tab === "maturity" ? (
              <>
                <ol className="maturity-stepper">
                  <li className={m === "expected" ? "is-current" : m !== "expected" ? "is-done" : undefined}>
                    {expectedLabel}
                  </li>
                  <li
                    className={
                      m === "confirmed"
                        ? "is-current"
                        : m === "actual"
                          ? "is-done"
                          : undefined
                    }
                  >
                    {confirmedLabel}
                  </li>
                  <li className={m === "actual" ? "is-current" : undefined}>
                    {actualLabel}
                  </li>
                </ol>
                <p className="muted">
                  {revenueLabel} ≠ hóa đơn / AR / thu tiền. Chuyển độ chín trên hồ sơ
                  đầy đủ.
                </p>
              </>
            ) : null}
            {tab === "related" ? (
              <ul className="stack-list">
                <li>
                  <Link className="row-link" href={`/bills/${selected.billId}`}>
                    {billLabel} {selected.billId.slice(0, 8)}…
                  </Link>
                </li>
                <li>
                  <Link
                    className="row-link"
                    href={`/bills/${selected.billId}?tab=revenues`}
                  >
                    Doanh thu trên {billLabel}
                  </Link>
                </li>
              </ul>
            ) : null}
          </>
        ) : null}
      </DetailDrawer>
    </>
  );
}
