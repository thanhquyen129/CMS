"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import {
  billTypeLabel,
  operationalStatusLabel,
  type BillListItem,
} from "@/lib/bills-shared";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Labels = {
  bill: string;
  revenue: string;
  cost: string;
  profit: string;
  expected: string;
  confirmed: string;
  actual: string;
};

type Props = {
  bills: BillListItem[];
  labels: Labels;
  initialSelectedId?: string | null;
};

function money(
  amount: number | null | undefined,
  currency: string | null | undefined
): string {
  if (amount == null || !currency) return "—";
  return formatMoney(amount, currency);
}

export function BillListWorkspace({
  bills,
  labels,
  initialSelectedId = null,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(initialSelectedId);

  const selected = useMemo(
    () => bills.find((b) => b.id === selectedId) ?? null,
    [bills, selectedId]
  );

  const close = useCallback(() => setSelectedId(null), []);

  return (
    <>
      <div className="table-wrap" style={{ marginTop: "1rem" }}>
        <table className="data-table">
          <caption className="sr-only">
            Danh sách {labels.bill} ({bills.length})
          </caption>
          <thead>
            <tr>
              <th scope="col">Số {labels.bill}</th>
              <th scope="col">Loại</th>
              <th scope="col">Trạng thái</th>
              <th scope="col">Ngày tạo</th>
              <th scope="col" className="num">
                {labels.revenue}
              </th>
              <th scope="col" className="num">
                {labels.cost}
              </th>
              <th scope="col" className="num">
                {labels.profit}
              </th>
              <th scope="col">
                <span className="sr-only">Mở</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {bills.map((b) => {
              const cur = b.summaryCurrencyCode;
              const active = selectedId === b.id;
              return (
                <tr
                  key={b.id}
                  className={active ? "row-selected" : undefined}
                  onClick={() => setSelectedId(b.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setSelectedId(b.id);
                    }
                  }}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
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
                        setSelectedId(b.id);
                      }}
                    >
                      {b.billNo}
                    </button>
                  </td>
                  <td>{billTypeLabel(b.billType)}</td>
                  <td>
                    <span className="status-pill">
                      {operationalStatusLabel(b.operationalStatus)}
                    </span>
                  </td>
                  <td>{formatDateTimeVi(b.createdAt)}</td>
                  <td className="num">
                    {money(b.revenueBestAvailable, cur)}
                  </td>
                  <td className="num">{money(b.costBestAvailable, cur)}</td>
                  <td
                    className={`num${
                      b.profitBestAvailable != null && b.profitBestAvailable < 0
                        ? " neg"
                        : ""
                    }`}
                  >
                    {money(b.profitBestAvailable, cur)}
                  </td>
                  <td>
                    <Link
                      className="btn btn-ghost btn-sm"
                      href={`/bills/${b.id}`}
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
          selected ? (
            <>
              {labels.bill} {selected.billNo}
            </>
          ) : null
        }
        subtitle={
          selected ? (
            <>
              <span className="status-pill">
                {operationalStatusLabel(selected.operationalStatus)}
              </span>
              {" · "}
              {billTypeLabel(selected.billType)}
            </>
          ) : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={`/bills/${selected.id}`}>
                Mở hồ sơ đầy đủ
              </Link>
              <Link
                className="btn btn-sm btn-ghost"
                href={`/bills/${selected.id}?tab=costs`}
              >
                {labels.cost}
              </Link>
              <Link
                className="btn btn-sm btn-ghost"
                href={`/bills/${selected.id}?tab=revenues`}
              >
                {labels.revenue}
              </Link>
              <Link
                className="btn btn-sm btn-ghost"
                href={`/documents?billId=${selected.id}`}
              >
                Chứng từ
              </Link>
            </div>
          ) : null
        }
      >
        {selected ? (
          <div className="stack">
            <h3 className="section-title sm">Chỉ số tài chính</h3>
            <p className="muted small">
              Giá trị tốt nhất hiện có (projection) — không phải sổ cái. Độ chín{" "}
              {labels.expected} / {labels.confirmed} / {labels.actual} tách riêng.
            </p>
            <dl className="metric-grid">
              <div>
                <dt>
                  {labels.revenue} — {labels.expected}
                </dt>
                <dd>
                  {money(
                    selected.revenueExpectedTotal,
                    selected.summaryCurrencyCode
                  )}
                </dd>
              </div>
              <div>
                <dt>
                  {labels.revenue} — {labels.confirmed}
                </dt>
                <dd>
                  {money(
                    selected.revenueConfirmedTotal,
                    selected.summaryCurrencyCode
                  )}
                </dd>
              </div>
              <div>
                <dt>
                  {labels.revenue} — {labels.actual}
                </dt>
                <dd>
                  {money(
                    selected.revenueActualTotal,
                    selected.summaryCurrencyCode
                  )}
                </dd>
              </div>
              <div>
                <dt>
                  {labels.revenue} (tốt nhất)
                </dt>
                <dd>
                  {money(
                    selected.revenueBestAvailable,
                    selected.summaryCurrencyCode
                  )}
                </dd>
              </div>
              <div>
                <dt>
                  {labels.cost} (tốt nhất)
                </dt>
                <dd>
                  {money(selected.costBestAvailable, selected.summaryCurrencyCode)}
                </dd>
              </div>
              <div>
                <dt>
                  {labels.profit} (tốt nhất)
                </dt>
                <dd
                  className={
                    selected.profitBestAvailable != null &&
                    selected.profitBestAvailable < 0
                      ? "neg"
                      : undefined
                  }
                >
                  {money(
                    selected.profitBestAvailable,
                    selected.summaryCurrencyCode
                  )}
                </dd>
              </div>
            </dl>
            <p className="muted small">
              Tạo: {formatDateTimeVi(selected.createdAt)}
              {selected.summaryCurrencyCode
                ? ` · Tiền tệ: ${selected.summaryCurrencyCode}`
                : ""}
            </p>
          </div>
        ) : null}
      </DetailDrawer>
    </>
  );
}
