import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { ManualReferenceForm } from "@/components/ManualReferenceForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { operationalStatusLabel } from "@/lib/bills-shared";
import { formatDateTimeVi } from "@/lib/money";
import {
  listLegs,
  listMovements,
  listOrders,
  listShipments,
  sourceSystemLabel,
} from "@/lib/operational-refs";

type SearchParams = Promise<{ tab?: string; q?: string }>;

const TABS = [
  { id: "orders", label: "Đơn hàng" },
  { id: "shipments", label: "Lô hàng" },
  { id: "legs", label: "Chặng" },
  { id: "movements", label: "Chuyến" },
] as const;

export default async function OperationsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { tab: tabRaw, q } = await searchParams;
  const tab = TABS.some((t) => t.id === tabRaw) ? tabRaw! : "orders";
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");

  const [ordersRes, shipmentsRes, legsRes, movementsRes] = await Promise.all([
    listOrders(q),
    listShipments(q),
    listLegs(q),
    listMovements(q),
  ]);

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/bills", label: billLabel },
            { label: "Tham chiếu vận hành" },
          ]}
          title="Tham chiếu vận hành"
          lede="Order / Lô hàng / Chặng / Chuyến phục vụ neo tài chính trên Bill. Không phải TMS — không điều phối, định vị hay e-POD."
        />

        <div className="hub-module-tabs" role="tablist" aria-label="Loại tham chiếu">
          {TABS.map((t) => (
            <Link
              key={t.id}
              href={`/operations?tab=${t.id}${q ? `&q=${encodeURIComponent(q)}` : ""}`}
              className={`hub-module-tab${tab === t.id ? " is-active" : ""}`}
            >
              <strong>{t.label}</strong>
            </Link>
          ))}
        </div>

        <form className="filter-bar" method="get" style={{ marginTop: "1rem" }}>
          <input type="hidden" name="tab" value={tab} />
          <label className="sr-only" htmlFor="ops-q">
            Tìm
          </label>
          <input
            id="ops-q"
            name="q"
            type="search"
            defaultValue={q ?? ""}
            placeholder="Số tham chiếu hoặc mã ngoài"
          />
          <button className="btn btn-ghost" type="submit">
            Tìm
          </button>
        </form>

        {tab === "orders" ? (
          <OpsTable
            error={ordersRes.ok ? null : ordersRes.message}
            empty="Chưa có đơn hàng tham chiếu."
            rows={
              ordersRes.ok
                ? ordersRes.data.map((o) => ({
                    id: o.id,
                    href: `/operations/orders/${o.id}`,
                    code: o.orderNo,
                    source: o.sourceSystem,
                    status: o.operationalStatus,
                    createdAt: o.createdAt,
                  }))
                : []
            }
          />
        ) : null}
        {tab === "shipments" ? (
          <OpsTable
            error={shipmentsRes.ok ? null : shipmentsRes.message}
            empty="Chưa có lô hàng tham chiếu."
            rows={
              shipmentsRes.ok
                ? shipmentsRes.data.map((s) => ({
                    id: s.id,
                    href: `/operations/shipments/${s.id}`,
                    code: s.shipmentNo,
                    source: s.sourceSystem,
                    status: s.operationalStatus,
                    createdAt: s.createdAt,
                  }))
                : []
            }
          />
        ) : null}
        {tab === "legs" ? (
          <OpsTable
            error={legsRes.ok ? null : legsRes.message}
            empty="Chưa có chặng tham chiếu."
            rows={
              legsRes.ok
                ? legsRes.data.map((l) => ({
                    id: l.id,
                    href: `/operations/legs/${l.id}`,
                    code: l.legNo,
                    source: l.sourceSystem,
                    status: l.operationalStatus,
                    createdAt: l.createdAt,
                  }))
                : []
            }
          />
        ) : null}
        {tab === "movements" ? (
          <OpsTable
            error={movementsRes.ok ? null : movementsRes.message}
            empty="Chưa có chuyến tham chiếu."
            rows={
              movementsRes.ok
                ? movementsRes.data.map((m) => ({
                    id: m.id,
                    href: `/operations/movements/${m.id}`,
                    code: m.movementNo,
                    source: m.sourceSystem,
                    status: m.operationalStatus,
                    createdAt: m.createdAt,
                  }))
                : []
            }
          />
        ) : null}

        <div className="layout-cols-2" style={{ marginTop: "1.5rem" }}>
          {tab === "orders" ? <ManualReferenceForm kind="order" /> : null}
          {tab === "shipments" ? <ManualReferenceForm kind="shipment" /> : null}
          {tab === "legs" ? (
            <ManualReferenceForm
              kind="leg"
              shipments={
                shipmentsRes.ok
                  ? shipmentsRes.data.map((s) => ({
                      id: s.id,
                      shipmentNo: s.shipmentNo,
                    }))
                  : []
              }
            />
          ) : null}
          {tab === "movements" ? <ManualReferenceForm kind="movement" /> : null}
        </div>
      </section>
    </AppShell>
  );
}

function OpsTable({
  error,
  empty,
  rows,
}: {
  error: string | null;
  empty: string;
  rows: {
    id: string;
    href: string;
    code: string;
    source: string;
    status: string;
    createdAt: string;
  }[];
}) {
  if (error) {
    return (
      <div className="alert alert-error" role="alert">
        {error}
      </div>
    );
  }
  if (rows.length === 0) {
    return (
      <div className="empty-state" role="status">
        {empty}
      </div>
    );
  }
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">Số</th>
            <th scope="col">Nguồn</th>
            <th scope="col">Trạng thái</th>
            <th scope="col">Tạo lúc</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id}>
              <td>
                <Link className="row-link" href={r.href}>
                  {r.code}
                </Link>
              </td>
              <td>{sourceSystemLabel(r.source)}</td>
              <td>{operationalStatusLabel(r.status)}</td>
              <td className="muted small">{formatDateTimeVi(r.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
