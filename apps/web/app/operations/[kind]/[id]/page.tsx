import { cookies } from "next/headers";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { LinkBillToRefForm } from "@/components/LinkBillToRefForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { OperationalContextGrid } from "@/components/OperationalContextGrid";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { operationalStatusLabel } from "@/lib/bills-shared";
import {
  getLeg,
  getMovement,
  getOrder,
  getShipment,
  operationalRefCode,
  sourceSystemLabel,
} from "@/lib/operational-refs";

type Params = Promise<{ kind: string; id: string }>;

const KINDS = new Set(["orders", "shipments", "legs", "movements"]);

export default async function OperationalDetailPage({
  params,
}: {
  params: Params;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { kind, id } = await params;
  if (!KINDS.has(kind)) {
    notFound();
  }

  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");

  const loaded =
    kind === "orders"
      ? await getOrder(id)
      : kind === "shipments"
        ? await getShipment(id)
        : kind === "legs"
          ? await getLeg(id)
          : await getMovement(id);

  const title =
    kind === "orders"
      ? "Đơn hàng"
      : kind === "shipments"
        ? "Lô hàng"
        : kind === "legs"
          ? "Chặng"
          : "Chuyến";

  const tab =
    kind === "orders"
      ? "orders"
      : kind === "shipments"
        ? "shipments"
        : kind === "legs"
          ? "legs"
          : "movements";

  if (!loaded.ok) {
    return (
      <AppShell terms={terms} active="bills" navChild="operations">
        <section className="panel">
          <p className="breadcrumb">
            <Link href={`/operations?tab=${tab}`}>Tham chiếu vận hành</Link>
          </p>
          <div className="alert alert-error" role="alert">
            {loaded.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const row = loaded.data;
  const code = operationalRefCode(row);

  const linkEndpoint =
    kind === "orders"
      ? `/bff/orders/${id}/bills`
      : kind === "shipments"
        ? `/bff/shipments/${id}/bills`
        : kind === "legs"
          ? `/bff/transport-legs/${id}/bills`
          : `/bff/transport-movements/${id}/bills`;

  return (
    <AppShell terms={terms} active="bills" navChild="operations">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/bills", label: billLabel },
            { href: `/operations?tab=${tab}`, label: "Tham chiếu vận hành" },
            { label: code },
          ]}
          title={`${title} ${code}`}
          lede={`${sourceSystemLabel(row.sourceSystem)} · ${operationalStatusLabel(row.operationalStatus)}`}
        />

        <dl className="kv-grid">
          <div>
            <dt>Mã ngoài</dt>
            <dd className="mono-id">{row.externalId}</dd>
          </div>
          <div>
            <dt>Nguồn</dt>
            <dd>{sourceSystemLabel(row.sourceSystem)}</dd>
          </div>
          {"shipmentId" in row && row.shipmentNo ? (
            <div>
              <dt>Lô hàng</dt>
              <dd>
                <Link href={`/operations/shipments/${row.shipmentId}`}>
                  {row.shipmentNo}
                </Link>
              </dd>
            </div>
          ) : null}
        </dl>

        {"transportMode" in row || "customerName" in row || "context" in row ? (
          <>
            <h2 className="section-title">Ngữ cảnh vận hành</h2>
            <OperationalContextGrid row={row} />
          </>
        ) : null}

        <h2 className="section-title">{billLabel} liên kết</h2>
        {row.relatedBills.length === 0 ? (
          <p className="muted">Chưa gắn {billLabel}.</p>
        ) : (
          <ul className="stack-list">
            {row.relatedBills.map((b) => (
              <li key={b.id}>
                <Link href={`/bills/${b.id}`}>{b.billNo}</Link>
                <span className="muted small">
                  {" "}
                  · {operationalStatusLabel(b.operationalStatus)}
                </span>
              </li>
            ))}
          </ul>
        )}
        <LinkBillToRefForm endpoint={linkEndpoint} billLabel={billLabel} />

        {"legs" in row && Array.isArray(row.legs) && row.legs.length > 0 ? (
          <>
            <h2 className="section-title">Chặng</h2>
            <ul className="stack-list">
              {row.legs.map((l: { id: string; legNo: string }) => (
                <li key={l.id}>
                  <Link href={`/operations/legs/${l.id}`}>{l.legNo}</Link>
                </li>
              ))}
            </ul>
          </>
        ) : null}

        {"movements" in row &&
        Array.isArray(row.movements) &&
        row.movements.length > 0 ? (
          <>
            <h2 className="section-title">Chuyến</h2>
            <ul className="stack-list">
              {row.movements.map((m: { id: string; movementNo: string }) => (
                <li key={m.id}>
                  <Link href={`/operations/movements/${m.id}`}>
                    {m.movementNo}
                  </Link>
                </li>
              ))}
            </ul>
          </>
        ) : null}
      </section>
    </AppShell>
  );
}
