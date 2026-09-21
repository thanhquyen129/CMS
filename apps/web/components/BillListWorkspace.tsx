"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { listenListSelected, replaceSearchShallow } from "@/lib/shallow-query";
import { BillFinancialDrawer } from "./BillFinancialDrawer";
import { DataTableShell, ExportCsvButton } from "@/components/list";
import {
  billTypeLabel,
  operationalStatusLabel,
  operationalStatusPillClass,
  transportModeLabel,
  type BillListItem,
} from "@/lib/bills-shared";
import { formatDateVi, formatMoney } from "@/lib/money";
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
  from?: string;
  to?: string;
  route?: string;
  customer?: string;
  page?: string;
  pageSize?: string;
};

type Props = {
  bills: BillListItem[];
  filteredCount: number;
  labels: Labels;
  terms: TerminologyMap;
  initialSelectedId?: string | null;
  listParams?: ListParams;
  pagination?: ReactNode;
  lower?: ReactNode;
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
  filteredCount,
  labels,
  terms,
  initialSelectedId = null,
  listParams = {},
  pagination,
  lower,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(initialSelectedId);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    setSelectedId((prev) => {
      if (prev && bills.some((b) => b.id === prev)) return prev;
      if (dismissed) return null;
      if (initialSelectedId && bills.some((b) => b.id === initialSelectedId)) {
        return initialSelectedId;
      }
      return bills[0]?.id ?? null;
    });
  }, [bills, initialSelectedId, dismissed]);

  useEffect(() => {
    return listenListSelected("/bills", (id) => {
      setDismissed(!id);
      setSelectedId(id);
    });
  }, []);

  const selected = useMemo(
    () => bills.find((b) => b.id === selectedId) ?? null,
    [bills, selectedId]
  );

  const syncSelected = useCallback(
    (id: string | null) => {
      setDismissed(!id);
      setSelectedId(id);
      const params = new URLSearchParams();
      if (listParams.q) params.set("q", listParams.q);
      if (listParams.status) params.set("status", listParams.status);
      if (listParams.from) params.set("from", listParams.from);
      if (listParams.to) params.set("to", listParams.to);
      if (listParams.route) params.set("route", listParams.route);
      if (listParams.customer) params.set("customer", listParams.customer);
      if (listParams.page && listParams.page !== "1") params.set("page", listParams.page);
      if (listParams.pageSize) params.set("pageSize", listParams.pageSize);
      if (id) params.set("selected", id);
      const qs = params.toString();
      replaceSearchShallow(qs ? `/bills?${qs}` : "/bills");
    },
    [listParams]
  );

  const close = useCallback(() => {
    setDismissed(true);
    setSelectedId(null);
  }, []);

  const csvRows = bills.map((b) => [
    b.billNo,
    b.customerName ?? "",
    b.routeCode ?? "",
    b.transportMode ? transportModeLabel(b.transportMode) : billTypeLabel(b.billType),
    formatDateVi(b.createdAt),
    operationalStatusLabel(b.operationalStatus),
    b.revenueBestAvailable ?? "",
    b.costBestAvailable ?? "",
    b.profitBestAvailable ?? "",
    b.summaryCurrencyCode ?? "",
  ]);

  return (
    <>
      <div className="list-workspace">
        <div className="list-workspace-main">
          <div className="list-table-card">
          <DataTableShell
            title={`Danh sách ${labels.bill}`}
            count={filteredCount}
            toolbar={
              <ExportCsvButton
                filename={`bills-${new Date().toISOString().slice(0, 10)}.csv`}
                headers={[
                  `Số ${labels.bill}`,
                  "Khách hàng",
                  "Tuyến",
                  "Loại",
                  "Ngày tạo",
                  "Trạng thái",
                  labels.revenue,
                  labels.cost,
                  labels.profit,
                  "Tiền tệ",
                ]}
                rows={csvRows}
              />
            }
          >
            <table className="data-table">
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
                    <span className="sr-only">Hồ sơ</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {bills.map((b) => {
                  const cur = b.summaryCurrencyCode;
                  const active = selectedId === b.id;
                  const profitNeg = b.profitBestAvailable != null && b.profitBestAvailable < 0;
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
                    >
                      <td>
                        <button
                          type="button"
                          className="row-link"
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
                      <td>
                        {b.transportMode
                          ? transportModeLabel(b.transportMode)
                          : billTypeLabel(b.billType)}
                      </td>
                      <td>{formatDateVi(b.createdAt)}</td>
                      <td>
                        <span className={operationalStatusPillClass(b.operationalStatus)}>
                          {operationalStatusLabel(b.operationalStatus)}
                        </span>
                      </td>
                      <td className="num">{money(b.revenueBestAvailable, cur)}</td>
                      <td className="num">{money(b.costBestAvailable, cur)}</td>
                      <td className={`num${profitNeg ? " neg" : " pos"}`}>
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
          </DataTableShell>
          {pagination}
          </div>
          {lower}
        </div>
        <BillFinancialDrawer
          billId={selected?.id ?? null}
          open={Boolean(selected)}
          onClose={close}
          labels={labels}
          terms={terms}
          inline
        />
      </div>
    </>
  );
}
