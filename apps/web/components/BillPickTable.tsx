import Link from "next/link";
import { DataTableShell } from "@/components/list";
import {
  billTypeLabel,
  operationalStatusLabel,
  operationalStatusPillClass,
  transportModeLabel,
  type BillListItem,
} from "@/lib/bills-shared";
import { formatDateVi, formatMoney } from "@/lib/money";

type Props = {
  bills: BillListItem[];
  totalCount: number;
  billLabel: string;
  revenueLabel: string;
  costLabel: string;
  profitLabel: string;
  selectedId?: string | null;
  /** Base path for selection links, e.g. /revenues/new */
  pickHrefBase: string;
  emptyMessage?: string;
};

function money(
  amount: number | null | undefined,
  currency: string | null | undefined
): string {
  if (amount == null || !currency) return "—";
  return formatMoney(amount, currency);
}

/** Compact Bill grid for picking a Bill before create cost/revenue (UI-02 columns). */
export function BillPickTable({
  bills,
  totalCount,
  billLabel,
  revenueLabel,
  costLabel,
  profitLabel,
  selectedId = null,
  pickHrefBase,
  emptyMessage,
}: Props) {
  return (
    <div className="list-table-card">
      <DataTableShell
        title={`Chọn ${billLabel}`}
        count={totalCount}
        toolbar={
          <Link className="btn btn-sm btn-ghost" href="/bills">
            Mở danh sách {billLabel}
          </Link>
        }
      >
        {bills.length === 0 ? (
          <div className="empty-state" role="status">
            {emptyMessage ?? `Chưa có ${billLabel} để gắn.`}
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Số {billLabel}</th>
                  <th scope="col">Khách hàng</th>
                  <th scope="col">Tuyến</th>
                  <th scope="col">Loại</th>
                  <th scope="col">Ngày tạo</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col" className="num">
                    {revenueLabel}
                  </th>
                  <th scope="col" className="num">
                    {costLabel}
                  </th>
                  <th scope="col" className="num">
                    {profitLabel}
                  </th>
                  <th scope="col">
                    <span className="sr-only">Chọn</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {bills.map((b) => {
                  const selected = b.id === selectedId;
                  const href = `${pickHrefBase}?billId=${encodeURIComponent(b.id)}`;
                  const currency = b.summaryCurrencyCode;
                  return (
                    <tr key={b.id} className={selected ? "row-selected" : undefined}>
                      <td>
                        <Link className="row-link" href={href}>
                          {b.billNo}
                        </Link>
                      </td>
                      <td>{b.customerName ?? "—"}</td>
                      <td>{b.routeCode ?? "—"}</td>
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
                      <td className="num">{money(b.revenueBestAvailable, currency)}</td>
                      <td className="num">{money(b.costBestAvailable, currency)}</td>
                      <td className="num">{money(b.profitBestAvailable, currency)}</td>
                      <td>
                        <Link
                          className={selected ? "btn btn-sm btn-primary" : "btn btn-sm"}
                          href={href}
                          aria-current={selected ? "true" : undefined}
                        >
                          {selected ? "Đã chọn" : "Chọn"}
                        </Link>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </DataTableShell>
    </div>
  );
}
