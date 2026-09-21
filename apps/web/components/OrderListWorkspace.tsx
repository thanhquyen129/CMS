"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState, useTransition, type ReactNode } from "react";
import { DetailDrawer } from "./DetailDrawer";
import { DataTableShell, DrawerTabs, ExportCsvButton } from "@/components/list";
import {
  operationalStatusLabel,
  operationalStatusPillClass,
  transportModeLabel,
} from "@/lib/bills-shared";
import { formatDateVi } from "@/lib/money";
import { fetchOrderClient } from "@/lib/operational-refs-client";
import {
  sourceSystemLabel,
  type OrderDetail,
  type OrderListItem,
} from "@/lib/operational-refs";

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
  orders: OrderListItem[];
  filteredCount: number;
  initialSelectedId?: string | null;
  listParams?: ListParams;
  pagination?: ReactNode;
  lower?: ReactNode;
};

export function OrderListWorkspace({
  orders,
  filteredCount,
  initialSelectedId = null,
  listParams = {},
  pagination,
  lower,
}: Props) {
  const router = useRouter();
  const [selectedId, setSelectedId] = useState<string | null>(initialSelectedId);
  const [dismissed, setDismissed] = useState(false);
  const [, startTransition] = useTransition();
  const [tab, setTab] = useState("overview");
  const [detail, setDetail] = useState<OrderDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setSelectedId((prev) => {
      if (prev && orders.some((o) => o.id === prev)) return prev;
      if (dismissed) return null;
      if (initialSelectedId && orders.some((o) => o.id === initialSelectedId)) {
        return initialSelectedId;
      }
      return orders[0]?.id ?? null;
    });
  }, [orders, initialSelectedId, dismissed]);

  const selected = useMemo(
    () => orders.find((o) => o.id === selectedId) ?? null,
    [orders, selectedId]
  );

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      setError(null);
      setTab("overview");
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    setTab("overview");
    void fetchOrderClient(selectedId).then((res) => {
      if (cancelled) return;
      setLoading(false);
      if (!res.ok) {
        setDetail(null);
        setError(res.message);
        return;
      }
      setDetail(res.data);
    });
    return () => {
      cancelled = true;
    };
  }, [selectedId]);

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
      startTransition(() => {
        router.replace(qs ? `/orders?${qs}` : "/orders", { scroll: false });
      });
    },
    [listParams, router]
  );

  const close = useCallback(() => {
    setDismissed(true);
    setSelectedId(null);
  }, []);
  const row = detail ?? selected;

  const csvRows = orders.map((o) => [
    o.orderNo,
    o.customerName ?? "",
    o.routeCode ?? "",
    transportModeLabel(o.transportMode),
    sourceSystemLabel(o.sourceSystem),
    formatDateVi(o.createdAt),
    operationalStatusLabel(o.operationalStatus),
    o.relatedBillCount ?? 0,
  ]);

  return (
    <div className="list-workspace">
        <div className="list-workspace-main">
          <div className="list-table-card">
          <DataTableShell
          title="Danh sách đơn hàng"
          count={filteredCount}
          toolbar={
            <ExportCsvButton
              filename={`orders-${new Date().toISOString().slice(0, 10)}.csv`}
              headers={[
                "Mã đơn",
                "Khách hàng",
                "Tuyến",
                "Phương thức",
                "Nguồn",
                "Ngày tạo",
                "Trạng thái",
                "Bill",
              ]}
              rows={csvRows}
            />
          }
        >
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Mã đơn</th>
                <th scope="col">Khách hàng</th>
                <th scope="col">Tuyến</th>
                <th scope="col">Loại</th>
                <th scope="col">Ngày tạo</th>
                <th scope="col">Trạng thái</th>
                <th scope="col" className="num">
                  Bill
                </th>
                <th scope="col">
                  <span className="sr-only">Hồ sơ</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {orders.map((o) => {
                const active = selectedId === o.id;
                return (
                  <tr
                    key={o.id}
                    className={active ? "row-selected" : undefined}
                    onClick={() => syncSelected(o.id)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        syncSelected(o.id);
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
                          syncSelected(o.id);
                        }}
                      >
                        {o.orderNo}
                      </button>
                    </td>
                    <td>{o.customerName || "—"}</td>
                    <td>{o.routeCode || "—"}</td>
                    <td>{transportModeLabel(o.transportMode)}</td>
                    <td>{formatDateVi(o.createdAt)}</td>
                    <td>
                      <span className={operationalStatusPillClass(o.operationalStatus)}>
                        {operationalStatusLabel(o.operationalStatus)}
                      </span>
                    </td>
                    <td className="num">{o.relatedBillCount ?? 0}</td>
                    <td>
                      <Link
                        className="btn btn-ghost btn-sm"
                        href={`/operations/orders/${o.id}`}
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

      <DetailDrawer
        inline
        open={Boolean(selected)}
        onClose={close}
        emptyHint="Chọn một đơn hàng trên danh sách để xem tham chiếu vận hành."
        title={
          row ? (
            <>
              {row.orderNo}{" "}
              <span className={operationalStatusPillClass(row.operationalStatus)}>
                {operationalStatusLabel(row.operationalStatus)}
              </span>
            </>
          ) : (
            "Đơn hàng"
          )
        }
        subtitle={
          row
            ? `${row.customerName || "—"}  |  ${row.routeCode || "—"}  |  ${transportModeLabel(row.transportMode)}  |  ${formatDateVi(row.createdAt)}`
            : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={`/operations/orders/${selected.id}`}>
                Mở hồ sơ đầy đủ
              </Link>
              <Link className="btn btn-sm btn-ghost" href={`/bills/new`}>
                + Tạo Bill
              </Link>
            </div>
          ) : null
        }
      >
        {loading ? (
          <p className="muted" role="status">
            Đang tải đơn hàng…
          </p>
        ) : null}
        {error ? (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        ) : null}
        {row && !loading ? (
          <>
            <DrawerTabs
              tabs={[
                { id: "overview", label: "Tổng quan" },
                {
                  id: "bills",
                  label: "Bill liên kết",
                  badge: detail?.relatedBills.length ?? row.relatedBillCount ?? 0,
                },
              ]}
              activeId={tab}
              onChange={setTab}
            />
            {tab === "overview" ? (
              <dl className="info-grid">
                <div>
                  <dt>Mã đơn</dt>
                  <dd>{row.orderNo}</dd>
                </div>
                <div>
                  <dt>Khách hàng</dt>
                  <dd>{row.customerName || "—"}</dd>
                </div>
                <div>
                  <dt>Tuyến</dt>
                  <dd>{row.routeCode || "—"}</dd>
                </div>
                <div>
                  <dt>Phương thức</dt>
                  <dd>{transportModeLabel(row.transportMode)}</dd>
                </div>
                <div>
                  <dt>Nguồn</dt>
                  <dd>{sourceSystemLabel(row.sourceSystem)}</dd>
                </div>
                <div>
                  <dt>ETD / ETA</dt>
                  <dd>
                    {formatDateVi(row.etdAt)} / {formatDateVi(row.etaAt)}
                  </dd>
                </div>
                <div>
                  <dt>Reference khách</dt>
                  <dd>{row.customerReference || "—"}</dd>
                </div>
                <div>
                  <dt>Nhân viên phụ trách</dt>
                  <dd>{detail?.assignedUserName || "—"}</dd>
                </div>
                <div className="info-grid-span">
                  <dt>Mô tả</dt>
                  <dd>{row.description?.trim() || "—"}</dd>
                </div>
              </dl>
            ) : null}
            {tab === "bills" ? (
              !detail || detail.relatedBills.length === 0 ? (
                <p className="muted">
                  Chưa gắn Bill.{" "}
                  <Link className="row-link" href="/bills/new">
                    Tạo Bill
                  </Link>
                  .
                </p>
              ) : (
                <ul className="stack-list">
                  {detail.relatedBills.map((b) => (
                    <li key={b.id}>
                      <Link className="row-link" href={`/bills/${b.id}`}>
                        {b.billNo}
                      </Link>
                      <div className="muted small">
                        {operationalStatusLabel(b.operationalStatus)}
                      </div>
                    </li>
                  ))}
                </ul>
              )
            ) : null}
          </>
        ) : null}
      </DetailDrawer>
    </div>
  );
}
