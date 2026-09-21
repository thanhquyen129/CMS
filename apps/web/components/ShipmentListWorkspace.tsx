"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { listenListSelected, replaceSearchShallow } from "@/lib/shallow-query";
import { DetailDrawer } from "./DetailDrawer";
import { DataTableShell, DrawerTabs, ExportCsvButton } from "@/components/list";
import {
  operationalStatusLabel,
  operationalStatusPillClass,
  transportModeLabel,
} from "@/lib/bills-shared";
import { formatDateVi } from "@/lib/money";
import { fetchShipmentClient, sourceSystemLabel } from "@/lib/operational-refs-client";
import type { ShipmentDetail, ShipmentListItem } from "@/lib/operational-refs";

type ListParams = {
  q?: string;
  status?: string;
  from?: string;
  to?: string;
  route?: string;
  page?: string;
  pageSize?: string;
};

type Props = {
  shipments: ShipmentListItem[];
  filteredCount: number;
  initialSelectedId?: string | null;
  listParams?: ListParams;
  pagination?: ReactNode;
  lower?: ReactNode;
};

export function ShipmentListWorkspace({
  shipments,
  filteredCount,
  initialSelectedId = null,
  listParams = {},
  pagination,
  lower,
}: Props) {
  const [selectedId, setSelectedId] = useState<string | null>(initialSelectedId);
  const [dismissed, setDismissed] = useState(false);
  const [tab, setTab] = useState("overview");
  const [detail, setDetail] = useState<ShipmentDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setSelectedId((prev) => {
      if (prev && shipments.some((s) => s.id === prev)) return prev;
      if (dismissed) return null;
      if (initialSelectedId && shipments.some((s) => s.id === initialSelectedId)) {
        return initialSelectedId;
      }
      return shipments[0]?.id ?? null;
    });
  }, [shipments, initialSelectedId, dismissed]);

  useEffect(() => {
    return listenListSelected("/shipments", (id) => {
      setDismissed(!id);
      setSelectedId(id);
    });
  }, []);

  const selected = useMemo(
    () => shipments.find((s) => s.id === selectedId) ?? null,
    [shipments, selectedId]
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
    void fetchShipmentClient(selectedId).then((res) => {
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
      if (listParams.page && listParams.page !== "1") params.set("page", listParams.page);
      if (listParams.pageSize) params.set("pageSize", listParams.pageSize);
      if (id) params.set("selected", id);
      const qs = params.toString();
      replaceSearchShallow(qs ? `/shipments?${qs}` : "/shipments");
    },
    [listParams]
  );

  const close = useCallback(() => {
    setDismissed(true);
    setSelectedId(null);
  }, []);
  const row = detail ?? selected;

  const csvRows = shipments.map((s) => [
    s.shipmentNo,
    s.routeCode ?? "",
    transportModeLabel(s.transportMode),
    sourceSystemLabel(s.sourceSystem),
    formatDateVi(s.createdAt),
    operationalStatusLabel(s.operationalStatus),
    s.relatedBillCount ?? 0,
    s.legCount ?? 0,
  ]);

  return (
    <div className="list-workspace">
      <div className="list-workspace-main">
        <div className="list-table-card">
          <DataTableShell
            title="Danh sách Shipment"
            count={filteredCount}
            toolbar={
              <ExportCsvButton
                filename={`shipments-${new Date().toISOString().slice(0, 10)}.csv`}
                headers={[
                  "Mã Shipment",
                  "Tuyến",
                  "Loại",
                  "Nguồn",
                  "Ngày tạo",
                  "Trạng thái",
                  "Bill",
                  "Chặng",
                ]}
                rows={csvRows}
              />
            }
          >
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Mã Shipment</th>
                  <th scope="col">Tuyến</th>
                  <th scope="col">Loại</th>
                  <th scope="col">Ngày tạo</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col" className="num">
                    Bill
                  </th>
                  <th scope="col" className="num">
                    Chặng
                  </th>
                  <th scope="col">
                    <span className="sr-only">Hồ sơ</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {shipments.map((s) => {
                  const active = selectedId === s.id;
                  return (
                    <tr
                      key={s.id}
                      className={active ? "row-selected" : undefined}
                      onClick={() => syncSelected(s.id)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          syncSelected(s.id);
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
                            syncSelected(s.id);
                          }}
                        >
                          {s.shipmentNo}
                        </button>
                      </td>
                      <td>{s.routeCode || "—"}</td>
                      <td>{transportModeLabel(s.transportMode)}</td>
                      <td>{formatDateVi(s.createdAt)}</td>
                      <td>
                        <span className={operationalStatusPillClass(s.operationalStatus)}>
                          {operationalStatusLabel(s.operationalStatus)}
                        </span>
                      </td>
                      <td className="num">{s.relatedBillCount ?? 0}</td>
                      <td className="num">{s.legCount ?? 0}</td>
                      <td>
                        <Link
                          className="btn btn-ghost btn-sm"
                          href={`/operations/shipments/${s.id}`}
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
        emptyHint="Chọn một Shipment trên danh sách để xem tham chiếu vận hành."
        title={
          row ? (
            <>
              {row.shipmentNo}{" "}
              <span className={operationalStatusPillClass(row.operationalStatus)}>
                {operationalStatusLabel(row.operationalStatus)}
              </span>
            </>
          ) : (
            "Shipment"
          )
        }
        subtitle={
          row
            ? `${row.routeCode || "—"}  |  ${transportModeLabel(row.transportMode)}  |  ${formatDateVi(row.createdAt)}`
            : null
        }
        footer={
          selected ? (
            <div className="toolbar-row" style={{ margin: 0 }}>
              <Link className="btn btn-sm" href={`/operations/shipments/${selected.id}`}>
                Mở hồ sơ đầy đủ
              </Link>
              <Link className="btn btn-sm btn-ghost" href="/bills/new">
                + Tạo Bill
              </Link>
            </div>
          ) : null
        }
      >
        {loading ? (
          <p className="muted" role="status">
            Đang tải Shipment…
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
                {
                  id: "legs",
                  label: "Chặng",
                  badge: detail?.legs.length ?? row.legCount ?? 0,
                },
              ]}
              activeId={tab}
              onChange={setTab}
            />
            {tab === "overview" ? (
              <dl className="info-grid">
                <div>
                  <dt>Mã Shipment</dt>
                  <dd>{row.shipmentNo}</dd>
                </div>
                <div>
                  <dt>Tuyến</dt>
                  <dd>{row.routeCode || "—"}</dd>
                </div>
                <div>
                  <dt>Điểm đi / đến</dt>
                  <dd>
                    {row.originCode || "—"} → {row.destinationCode || "—"}
                  </dd>
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
            {tab === "legs" ? (
              !detail || detail.legs.length === 0 ? (
                <p className="muted">Chưa có chặng trên Shipment này.</p>
              ) : (
                <ul className="stack-list">
                  {detail.legs.map((leg) => (
                    <li key={leg.id}>
                      <Link className="row-link" href={`/operations/legs/${leg.id}`}>
                        {leg.legNo}
                      </Link>
                      <div className="muted small">
                        {operationalStatusLabel(leg.operationalStatus)}
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
