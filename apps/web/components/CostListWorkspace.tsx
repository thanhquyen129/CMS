"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import {
  isSharedCost,
  maturityLabelKey,
  type CostListItem,
} from "@/lib/costs-revenues";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  costs: CostListItem[];
  billLabel: string;
  costLabel: string;
  expectedLabel: string;
  confirmedLabel: string;
  actualLabel: string;
  directLabel: string;
  sharedLabel: string;
};

function maturityVi(terms: TerminologyMap, maturity: string, fallbacks: Record<string, string>) {
  return term(terms, maturityLabelKey(maturity), fallbacks[maturity?.toLowerCase()] ?? maturity);
}

export function CostListWorkspace({
  terms,
  costs,
  billLabel,
  costLabel,
  expectedLabel,
  confirmedLabel,
  actualLabel,
  directLabel,
  sharedLabel,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = useMemo(
    () => costs.find((c) => c.id === selectedId) ?? null,
    [costs, selectedId]
  );
  const close = useCallback(() => setSelectedId(null), []);

  const fallbacks = {
    expected: expectedLabel,
    confirmed: confirmedLabel,
    actual: actualLabel,
  };

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Mã</th>
              <th scope="col">{billLabel}</th>
              <th scope="col">Nguồn</th>
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
            {costs.map((c) => {
              const href = isSharedCost(c.attributionType)
                ? `/costs/shared/${c.id}`
                : `/costs/${c.id}`;
              const active = selectedId === c.id;
              return (
                <tr
                  key={c.id}
                  className={active ? "row-selected" : undefined}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
                  onClick={() => setSelectedId(c.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setSelectedId(c.id);
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
                        setSelectedId(c.id);
                      }}
                    >
                      {c.costTypeCode || c.id.slice(0, 8)}
                    </button>
                  </td>
                  <td>
                    {c.billId ? (
                      <Link
                        className="row-link"
                        href={`/bills/${c.billId}`}
                        onClick={(e) => e.stopPropagation()}
                      >
                        {c.billId.slice(0, 8)}…
                      </Link>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td>
                    {isSharedCost(c.attributionType) ? sharedLabel : directLabel}
                  </td>
                  <td>
                    <span
                      className={`maturity-pill maturity-${c.financialMaturity?.toLowerCase()}`}
                    >
                      {maturityVi(terms, c.financialMaturity, fallbacks)}
                    </span>
                  </td>
                  <td className="num">{formatMoney(c.amount, c.currencyCode)}</td>
                  <td>{formatDateTimeVi(c.effectiveDate)}</td>
                  <td>
                    <Link
                      className="btn btn-ghost btn-sm"
                      href={href}
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
            ? `${costLabel} ${selected.costTypeCode || selected.id.slice(0, 8)}`
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
              <Link
                className="btn btn-sm"
                href={
                  isSharedCost(selected.attributionType)
                    ? `/costs/shared/${selected.id}`
                    : `/costs/${selected.id}`
                }
              >
                Mở hồ sơ đầy đủ
              </Link>
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
          <dl className="metric-grid">
            <div>
              <dt>Nguồn</dt>
              <dd>
                {isSharedCost(selected.attributionType)
                  ? sharedLabel
                  : directLabel}
              </dd>
            </div>
            <div>
              <dt>Số tiền hiện hành</dt>
              <dd>{formatMoney(selected.amount, selected.currencyCode)}</dd>
            </div>
            <div>
              <dt>{expectedLabel}</dt>
              <dd>
                {formatMoney(
                  selected.expectedAmount ?? selected.amount,
                  selected.currencyCode
                )}
              </dd>
            </div>
            <div>
              <dt>{confirmedLabel}</dt>
              <dd>
                {selected.confirmedAmount != null
                  ? formatMoney(selected.confirmedAmount, selected.currencyCode)
                  : "—"}
              </dd>
            </div>
            <div>
              <dt>{actualLabel}</dt>
              <dd>
                {selected.actualAmount != null
                  ? formatMoney(selected.actualAmount, selected.currencyCode)
                  : "—"}
              </dd>
            </div>
            <div>
              <dt>Hiệu lực</dt>
              <dd>{formatDateTimeVi(selected.effectiveDate)}</dd>
            </div>
          </dl>
        ) : null}
      </DetailDrawer>
    </>
  );
}
