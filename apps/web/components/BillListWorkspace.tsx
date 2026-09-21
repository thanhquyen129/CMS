"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useMemo, useState, useTransition } from "react";
import { BillFinancialDrawer } from "./BillFinancialDrawer";
import {
  billTypeLabel,
  operationalStatusLabel,
  transportModeLabel,
  type BillListItem,
} from "@/lib/bills-shared";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";

type Labels = {
  bill: string;
  revenue: string;
  cost: string;
  profit: string;
  expected: string;
  confirmed: string;
  actual: string;
};

type ListParams = {
  q?: string;
  status?: string;
  page?: string;
  pageSize?: string;
};

type Props = {
  bills: BillListItem[];
  labels: Labels;
  terms: TerminologyMap;
  initialSelectedId?: string | null;
  listParams?: ListParams;
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
  terms,
  initialSelectedId = null,
  listParams = {},
}: Props) {
  const router = useRouter();
  const [selectedId, setSelectedId] = useState<string | null>(initialSelectedId);
  const [, startTransition] = useTransition();

  const selected = useMemo(
    () => bills.find((b) => b.id === selectedId) ?? null,
    [bills, selectedId]
  );

  const syncSelected = useCallback(
    (id: string | null) => {
      setSelectedId(id);
      const params = new URLSearchParams();
      if (listParams.q) params.set("q", listParams.q);
      if (listParams.status) params.set("status", listParams.status);
      if (listParams.page && listParams.page !== "1") {
        params.set("page", listParams.page);
      }
      if (listParams.pageSize) params.set("pageSize", listParams.pageSize);
      if (id) params.set("selected", id);
      const qs = params.toString();
      startTransition(() => {
        router.replace(qs ? `/bills?${qs}` : "/bills", { scroll: false });
      });
    },
    [listParams, router]
  );

  const close = useCallback(() => syncSelected(null), [syncSelected]);

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
              <th scope="col">Khách hàng</th>
              <th scope="col">Tuyến</th>
              <th scope="col">Loại</th>
              <th scope="col">Ngày tạo</th>
              <th scope="col">Trạng thái</th>
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
                  onClick={() => syncSelected(b.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      syncSelected(b.id);
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
                        syncSelected(b.id);
                      }}
                    >
                      {b.billNo}
                    </button>
                  </td>
                  <td>{b.customerName || "—"}</td>
                  <td>{b.routeCode || "—"}</td>
                  <td>{b.transportMode ? transportModeLabel(b.transportMode) : billTypeLabel(b.billType)}</td>
                  <td>{formatDateTimeVi(b.createdAt)}</td>
                  <td>
                    <span className="status-pill">
                      {operationalStatusLabel(b.operationalStatus)}
                    </span>
                  </td>
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

      <BillFinancialDrawer
        billId={selected?.id ?? null}
        open={Boolean(selected)}
        onClose={close}
        labels={labels}
        terms={terms}
      />
    </>
  );
}
