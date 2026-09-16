"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { DetailDrawer } from "./DetailDrawer";
import { DocumentStatusTriad } from "./DocumentStatusTriad";
import {
  directionLabel,
  documentTypeLabel,
  type FinancialDocumentListItem,
} from "@/lib/documents";
import { formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  documents: FinancialDocumentListItem[];
  billLabel: string;
  docLabel: string;
};

export function DocumentListWorkspace({
  terms,
  documents,
  billLabel,
  docLabel,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = useMemo(
    () => documents.find((d) => d.id === selectedId) ?? null,
    [documents, selectedId]
  );
  const close = useCallback(() => setSelectedId(null), []);

  return (
    <>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Số chứng từ</th>
              <th scope="col">Loại</th>
              <th scope="col">Chiều</th>
              <th scope="col" className="num">
                Số tiền
              </th>
              <th scope="col">{billLabel}</th>
              <th scope="col">Trạng thái</th>
              <th scope="col">
                <span className="sr-only">Mở</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {documents.map((d) => {
              const active = selectedId === d.id;
              return (
                <tr
                  key={d.id}
                  className={active ? "row-selected" : undefined}
                  tabIndex={0}
                  style={{ cursor: "pointer" }}
                  onClick={() => setSelectedId(d.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      setSelectedId(d.id);
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
                        setSelectedId(d.id);
                      }}
                    >
                      {d.documentNo}
                    </button>
                  </td>
                  <td>{documentTypeLabel(d.documentType)}</td>
                  <td>{directionLabel(terms, d.direction)}</td>
                  <td className="num">
                    {formatMoney(d.totalAmount, d.currencyCode)}
                  </td>
                  <td>
                    {d.billId ? (
                      <Link
                        className="row-link"
                        href={`/bills/${d.billId}`}
                        onClick={(e) => e.stopPropagation()}
                      >
                        {d.billId.slice(0, 8)}…
                      </Link>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td>
                    <DocumentStatusTriad
                      terms={terms}
                      receiptStatus={d.receiptStatus}
                      acceptanceStatus={d.acceptanceStatus}
                      matchingStatus={d.matchingStatus}
                      compact
                    />
                  </td>
                  <td>
                    <Link
                      className="btn btn-ghost btn-sm"
                      href={`/documents/${d.id}`}
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
        title={selected ? `${docLabel} ${selected.documentNo}` : null}
        subtitle={
          selected ? (
            <>
              {documentTypeLabel(selected.documentType)}
              {" · "}
              {directionLabel(terms, selected.direction)}
            </>
          ) : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={`/documents/${selected.id}`}>
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
              <Link
                className="btn btn-sm btn-ghost"
                href={`/documents/${selected.id}/match`}
              >
                Khớp chứng từ
              </Link>
            </div>
          ) : null
        }
      >
        {selected ? (
          <div className="stack">
            <p className="muted small">
              Received ≠ Accepted ≠ Matched — ba chiều độc lập, không gộp một status.
            </p>
            <DocumentStatusTriad
              terms={terms}
              receiptStatus={selected.receiptStatus}
              acceptanceStatus={selected.acceptanceStatus}
              matchingStatus={selected.matchingStatus}
            />
            <dl className="metric-grid" style={{ marginTop: "1rem" }}>
              <div>
                <dt>Số tiền</dt>
                <dd>
                  {formatMoney(selected.totalAmount, selected.currencyCode)}
                </dd>
              </div>
              <div>
                <dt>Ngày chứng từ</dt>
                <dd>{selected.documentDate}</dd>
              </div>
              <div>
                <dt>{billLabel}</dt>
                <dd>
                  {selected.billId ? (
                    <Link className="row-link" href={`/bills/${selected.billId}`}>
                      {selected.billId.slice(0, 8)}…
                    </Link>
                  ) : (
                    "—"
                  )}
                </dd>
              </div>
            </dl>
          </div>
        ) : null}
      </DetailDrawer>
    </>
  );
}
