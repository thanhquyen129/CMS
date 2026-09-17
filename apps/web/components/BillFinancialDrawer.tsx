"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { AuditTrailPanel } from "./AuditTrailPanel";
import { DetailDrawer } from "./DetailDrawer";
import { DrawerTabs } from "./list/DrawerTabs";
import {
  fetchBillFinancialView,
  patchBillContext,
  type BillFinancialView,
} from "@/lib/bill-financial-view";
import {
  billTypeLabel,
  operationalStatusLabel,
} from "@/lib/bills-shared";
import {
  directionLabel,
  documentTypeLabel,
  receiptStatusLabel,
} from "@/lib/documents-shared";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { maturityLabelKey, type CostListItem } from "@/lib/costs-revenues";
import { term, type TerminologyMap } from "@/lib/terminology";

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
  billId: string | null;
  open: boolean;
  onClose: () => void;
  labels: Labels;
  terms: TerminologyMap;
};

function money(amount: number | null | undefined, currency: string | null | undefined) {
  if (amount == null || !currency) return "—";
  return formatMoney(amount, currency);
}

function formatDateOnly(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    return formatDateTimeVi(iso).split(" ")[0] ?? formatDateTimeVi(iso);
  } catch {
    return iso;
  }
}

function maturityVi(terms: TerminologyMap, maturity: string, labels: Labels) {
  const fallback =
    maturity?.toLowerCase() === "expected"
      ? labels.expected
      : maturity?.toLowerCase() === "confirmed"
        ? labels.confirmed
        : maturity?.toLowerCase() === "actual"
          ? labels.actual
          : maturity;
  return term(terms, maturityLabelKey(maturity), fallback);
}

export function BillFinancialDrawer({
  billId,
  open,
  onClose,
  labels,
  terms,
}: Props) {
  const [tab, setTab] = useState("overview");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [view, setView] = useState<BillFinancialView | null>(null);
  const [noteDraft, setNoteDraft] = useState("");
  const [noteSaving, setNoteSaving] = useState(false);
  const [noteMsg, setNoteMsg] = useState<string | null>(null);

  const load = useCallback(async (id: string) => {
    setLoading(true);
    setError(null);
    const res = await fetchBillFinancialView(id);
    if (!res.ok) {
      setView(null);
      setError(res.message);
      setLoading(false);
      return;
    }
    setView(res.data);
    setNoteDraft(res.data.bill.internalNote ?? "");
    setNoteMsg(null);
    setLoading(false);
  }, []);

  useEffect(() => {
    if (!open || !billId) {
      setView(null);
      setTab("overview");
      setError(null);
      return;
    }
    setTab("overview");
    void load(billId);
  }, [open, billId, load]);

  const saveNote = async () => {
    if (!billId || !view) return;
    setNoteSaving(true);
    setNoteMsg(null);
    const res = await patchBillContext(billId, {
      customerPartyId: view.bill.customerPartyId ?? null,
      routeCode: view.bill.routeCode ?? null,
      etdAt: view.bill.etdAt ?? null,
      etaAt: view.bill.etaAt ?? null,
      assignedUserId: view.bill.assignedUserId ?? null,
      description: view.bill.description ?? null,
      internalNote: noteDraft.trim() || null,
    });
    setNoteSaving(false);
    if (!res.ok) {
      setNoteMsg(res.message);
      return;
    }
    setNoteMsg("Đã lưu ghi chú.");
    setView({
      ...view,
      bill: { ...view.bill, internalNote: noteDraft.trim() || null },
    });
  };

  const bill = view?.bill;
  const bucket = view?.profile?.byCurrency?.[0];
  const currency = bucket?.currencyCode ?? bill?.summaryCurrencyCode ?? "VND";
  const primaryOrder = view?.graph?.orders?.[0];
  const primaryShipment = view?.graph?.shipments?.[0];
  const profitMargin =
    bucket && bucket.revenueBestAvailable !== 0
      ? (bucket.profitBestAvailable / bucket.revenueBestAvailable) * 100
      : null;

  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ");

  return (
    <DetailDrawer
      open={open}
      wide
      onClose={onClose}
      title={
        bill ? (
          <>
            {labels.bill} {bill.billNo}
          </>
        ) : billId ? (
          labels.bill
        ) : null
      }
      subtitle={
        bill ? (
          <>
            <span className="status-pill">
              {operationalStatusLabel(bill.operationalStatus)}
            </span>
            {bill.customerName ? ` · ${bill.customerName}` : ""}
            {bill.routeCode ? ` · ${bill.routeCode}` : ""}
            {` · ${billTypeLabel(bill.billType)}`}
            {` · ${formatDateOnly(bill.createdAt)}`}
          </>
        ) : null
      }
      footer={
        bill ? (
          <div className="toolbar-row" style={{ margin: 0 }}>
            <Link className="btn btn-sm" href={`/bills/${bill.id}`}>
              Mở hồ sơ đầy đủ
            </Link>
            <Link
              className="btn btn-sm btn-ghost"
              href={`/bills/${bill.id}/costs/new`}
            >
              + {labels.cost}
            </Link>
            <Link
              className="btn btn-sm btn-ghost"
              href={`/bills/${bill.id}/revenues/new`}
            >
              + {labels.revenue}
            </Link>
            <Link
              className="btn btn-sm btn-ghost"
              href={`/documents?billId=${bill.id}`}
            >
              {docLabel}
            </Link>
          </div>
        ) : null
      }
    >
      {loading ? (
        <p className="muted" role="status">
          Đang tải hồ sơ tài chính…
        </p>
      ) : null}
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {bill && view && !loading ? (
        <>
          <DrawerTabs
            tabs={[
              { id: "overview", label: "Tổng quan" },
              {
                id: "costs",
                label: labels.cost,
                badge: view.costCount,
              },
              {
                id: "revenues",
                label: labels.revenue,
                badge: view.revenueCount,
              },
              {
                id: "documents",
                label: docLabel,
                badge: view.documentCount,
              },
              { id: "history", label: "Lịch sử" },
              { id: "related", label: "Liên quan" },
            ]}
            activeId={tab}
            onChange={setTab}
          />

          {tab === "overview" ? (
            <div className="stack bill-fv-overview">
              <h3 className="section-title sm">Thông tin chung</h3>
              <dl className="info-grid">
                <div>
                  <dt>Số {labels.bill}</dt>
                  <dd>{bill.billNo}</dd>
                </div>
                <div>
                  <dt>Số đơn hàng</dt>
                  <dd>{primaryOrder?.orderNo ?? "—"}</dd>
                </div>
                <div>
                  <dt>Khách hàng</dt>
                  <dd>{bill.customerName ?? "—"}</dd>
                </div>
                <div>
                  <dt>Tuyến</dt>
                  <dd>{bill.routeCode ?? "—"}</dd>
                </div>
                <div>
                  <dt>Loại vận chuyển</dt>
                  <dd>{billTypeLabel(bill.billType)}</dd>
                </div>
                <div>
                  <dt>ETD / ETA</dt>
                  <dd>
                    {formatDateOnly(bill.etdAt)} / {formatDateOnly(bill.etaAt)}
                  </dd>
                </div>
                <div>
                  <dt>Trạng thái</dt>
                  <dd>
                    <span className="status-pill">
                      {operationalStatusLabel(bill.operationalStatus)}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt>Nhân viên phụ trách</dt>
                  <dd>{bill.assignedUserName ?? "—"}</dd>
                </div>
                <div>
                  <dt>Shipment</dt>
                  <dd>{primaryShipment?.shipmentNo ?? "—"}</dd>
                </div>
                <div className="info-grid-span">
                  <dt>Mô tả</dt>
                  <dd>{bill.description?.trim() || "—"}</dd>
                </div>
              </dl>

              <h3 className="section-title sm">
                Chỉ số tài chính ({currency})
              </h3>
              <p className="muted small">
                Projection theo độ chín {labels.expected} / {labels.confirmed} /{" "}
                {labels.actual} — không phải sổ cái.
              </p>
              <div className="bill-fv-finance">
                <div>
                  <h4 className="bill-fv-finance-label">{labels.revenue}</h4>
                  <dl className="metric-grid compact">
                    <div>
                      <dt>{labels.expected}</dt>
                      <dd>
                        {money(bucket?.revenueMaturity.expectedTotal, currency)}
                      </dd>
                    </div>
                    <div>
                      <dt>{labels.confirmed}</dt>
                      <dd>
                        {money(bucket?.revenueMaturity.confirmedTotal, currency)}
                      </dd>
                    </div>
                    <div>
                      <dt>{labels.actual}</dt>
                      <dd>
                        {money(bucket?.revenueMaturity.actualTotal, currency)}
                      </dd>
                    </div>
                  </dl>
                </div>
                <div>
                  <h4 className="bill-fv-finance-label">{labels.cost}</h4>
                  <dl className="metric-grid compact">
                    <div>
                      <dt>{labels.expected}</dt>
                      <dd>
                        {money(
                          bucket?.directCostMaturity.expectedTotal,
                          currency
                        )}
                      </dd>
                    </div>
                    <div>
                      <dt>{labels.confirmed}</dt>
                      <dd>
                        {money(
                          bucket?.directCostMaturity.confirmedTotal,
                          currency
                        )}
                      </dd>
                    </div>
                    <div>
                      <dt>{labels.actual}</dt>
                      <dd>
                        {money(bucket?.directCostMaturity.actualTotal, currency)}
                      </dd>
                    </div>
                  </dl>
                </div>
                <div>
                  <h4 className="bill-fv-finance-label">{labels.profit}</h4>
                  <dl className="metric-grid compact">
                    <div>
                      <dt>Dự kiến (tốt nhất)</dt>
                      <dd
                        className={
                          bucket && bucket.profitBestAvailable < 0
                            ? "neg"
                            : undefined
                        }
                      >
                        {money(bucket?.profitBestAvailable, currency)}
                      </dd>
                    </div>
                    <div>
                      <dt>Biên LN</dt>
                      <dd>
                        {profitMargin == null
                          ? "—"
                          : `${profitMargin.toFixed(1)}%`}
                      </dd>
                    </div>
                  </dl>
                </div>
              </div>

              <h3 className="section-title sm">Tiến độ xử lý</h3>
              <ol className="maturity-stepper bill-progress-stepper">
                {view.progress.map((s) => (
                  <li
                    key={s.id}
                    className={
                      s.state === "done"
                        ? "is-done"
                        : s.state === "current"
                          ? "is-current"
                          : undefined
                    }
                  >
                    {s.labelVi}
                  </li>
                ))}
              </ol>

              <h3 className="section-title sm">Hành động nhanh</h3>
              <div className="toolbar-row">
                <Link
                  className="btn btn-sm"
                  href={`/bills/${bill.id}/costs/new`}
                >
                  + Thêm {labels.cost.toLowerCase()}
                </Link>
                <Link
                  className="btn btn-sm"
                  href={`/bills/${bill.id}/revenues/new`}
                >
                  + Thêm {labels.revenue.toLowerCase()}
                </Link>
                <Link
                  className="btn btn-sm btn-ghost"
                  href={`/documents/receive?billId=${bill.id}`}
                >
                  Upload {docLabel.toLowerCase()}
                </Link>
                <Link
                  className="btn btn-sm btn-ghost"
                  href={`/bills/${bill.id}`}
                >
                  Thêm…
                </Link>
              </div>

              <h3 className="section-title sm">Ghi chú</h3>
              <label className="sr-only" htmlFor="bill-internal-note">
                Ghi chú nội bộ
              </label>
              <textarea
                id="bill-internal-note"
                className="input textarea"
                rows={3}
                value={noteDraft}
                onChange={(e) => setNoteDraft(e.target.value)}
                placeholder="Ghi chú nội bộ cho điều vận / kế toán…"
              />
              <div className="toolbar-row">
                <button
                  type="button"
                  className="btn btn-sm"
                  disabled={noteSaving}
                  onClick={() => void saveNote()}
                >
                  {noteSaving ? "Đang lưu…" : "Lưu ghi chú"}
                </button>
                {noteMsg ? (
                  <span className="muted small" role="status">
                    {noteMsg}
                  </span>
                ) : null}
              </div>
            </div>
          ) : null}

          {tab === "costs" ? (
            <CostRevenueMiniList
              kind="cost"
              labels={labels}
              terms={terms}
              billId={bill.id}
              items={view.costs}
              empty={`Chưa có ${labels.cost.toLowerCase()} gắn Bill này.`}
            />
          ) : null}

          {tab === "revenues" ? (
            <CostRevenueMiniList
              kind="revenue"
              labels={labels}
              terms={terms}
              billId={bill.id}
              items={view.revenues}
              empty={`Chưa có ${labels.revenue.toLowerCase()} gắn Bill này.`}
            />
          ) : null}

          {tab === "documents" ? (
            view.documents.length === 0 ? (
              <p className="muted">
                Chưa có {docLabel.toLowerCase()}.{" "}
                <Link className="row-link" href={`/documents?billId=${bill.id}`}>
                  Mở danh sách chứng từ
                </Link>
              </p>
            ) : (
              <ul className="stack-list">
                {view.documents.map((d) => (
                  <li key={d.id}>
                    <Link className="row-link" href={`/documents/${d.id}`}>
                      {documentTypeLabel(d.documentType)} {d.documentNo}
                    </Link>
                    <div className="muted small">
                      {directionLabel(terms, d.direction)} ·{" "}
                      {receiptStatusLabel(terms, d.receiptStatus)} ·{" "}
                      {formatMoney(d.totalAmount, d.currencyCode)}
                    </div>
                  </li>
                ))}
              </ul>
            )
          ) : null}

          {tab === "history" ? (
            <AuditTrailPanel
              terms={terms}
              objectType="Bill"
              objectId={bill.id}
              title="Lịch sử Bill"
            />
          ) : null}

          {tab === "related" ? (
            <div className="stack">
              <h3 className="section-title sm">Đơn hàng (Order)</h3>
              {view.graph.orders.length === 0 ? (
                <p className="muted small">Chưa liên kết Order.</p>
              ) : (
                <ul className="stack-list">
                  {view.graph.orders.map((o) => (
                    <li key={o.id}>
                      <strong>{o.orderNo}</strong>
                      <div className="muted small">
                        {operationalStatusLabel(o.operationalStatus)}
                        {o.sourceSystem ? ` · ${o.sourceSystem}` : ""}
                        {o.externalId ? ` · ${o.externalId}` : ""}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              <h3 className="section-title sm">Shipment</h3>
              {view.graph.shipments.length === 0 ? (
                <p className="muted small">Chưa liên kết Shipment.</p>
              ) : (
                <ul className="stack-list">
                  {view.graph.shipments.map((s) => (
                    <li key={s.id}>
                      <strong>{s.shipmentNo}</strong>
                      <div className="muted small">
                        {operationalStatusLabel(s.operationalStatus)}
                        {s.sourceSystem ? ` · ${s.sourceSystem}` : ""}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              <p className="muted small">
                CMS là lớp kiểm soát tài chính — Order/Shipment là tham chiếu vận
                hành (H-002), không thay SoT TMS.
              </p>
            </div>
          ) : null}
        </>
      ) : null}
    </DetailDrawer>
  );
}

function CostRevenueMiniList({
  kind,
  labels,
  terms,
  billId,
  items,
  empty,
}: {
  kind: "cost" | "revenue";
  labels: Labels;
  terms: TerminologyMap;
  billId: string;
  items: CostListItem[] | { id: string; financialMaturity: string; amount: number; currencyCode: string; revenueTypeCode?: string | null }[];
  empty: string;
}) {
  if (items.length === 0) {
    return (
      <p className="muted">
        {empty}{" "}
        <Link
          className="row-link"
          href={
            kind === "cost"
              ? `/bills/${billId}/costs/new`
              : `/bills/${billId}/revenues/new`
          }
        >
          Thêm mới
        </Link>
      </p>
    );
  }

  return (
    <ul className="stack-list">
      {items.map((item) => {
        const code =
          kind === "cost"
            ? (item as CostListItem).costTypeCode
            : (item as { revenueTypeCode?: string | null }).revenueTypeCode;
        const href =
          kind === "cost"
            ? (item as CostListItem).attributionType?.toLowerCase() === "shared"
              ? `/costs/shared/${item.id}`
              : `/costs/${item.id}`
            : `/revenues/${item.id}`;
        return (
          <li key={item.id}>
            <Link className="row-link" href={href}>
              {code || item.id.slice(0, 8)}
            </Link>
            <div className="muted small">
              <span
                className={`maturity-pill maturity-${item.financialMaturity?.toLowerCase()}`}
              >
                {maturityVi(terms, item.financialMaturity, labels)}
              </span>
              {" · "}
              {formatMoney(item.amount, item.currencyCode)}
            </div>
          </li>
        );
      })}
      <li>
        <Link
          className="row-link"
          href={
            kind === "cost"
              ? `/bills/${billId}?tab=costs`
              : `/bills/${billId}?tab=revenues`
          }
        >
          Xem đầy đủ trên hồ sơ →
        </Link>
      </li>
    </ul>
  );
}
