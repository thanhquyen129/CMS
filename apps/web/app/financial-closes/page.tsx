import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid, type StatCardModel } from "@/components/list/StatCardGrid";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  closeStatusLabel,
  listFinancialCloses,
  scopeTypeLabel,
} from "@/lib/financial-closes";
import { formatDateTimeVi } from "@/lib/money";

type SearchParams = Promise<{ status?: string; scopeType?: string }>;

export default async function FinancialClosesPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const snapshotLabel = term(
    terms,
    "FINANCIAL_CLOSE_SNAPSHOT",
    "Bản chốt tài chính"
  );
  const billLabel = term(terms, "BILL", "Bill");

  const result = await listFinancialCloses({
    status: sp.status,
    scopeType: sp.scopeType,
  });

  const items = result.ok ? result.data : [];
  const openCount = items.filter(
    (r) => r.status?.toLowerCase() === "open"
  ).length;
  const lockedCount = items.filter(
    (r) => r.status?.toLowerCase() === "locked"
  ).length;
  const reopenedCount = items.filter(
    (r) => r.status?.toLowerCase() === "reopened"
  ).length;
  const statCards: StatCardModel[] = [
    { key: "total", label: `Tổng ${closeLabel.toLowerCase()}`, value: items.length },
    { key: "open", label: "Đang mở", value: openCount },
    { key: "locked", label: "Đã khóa", value: lockedCount },
    { key: "reopened", label: "Đã mở lại", value: reopenedCount },
  ];

  return (
    <AppShell terms={terms} active="financial-closes">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: closeLabel },
          ]}
          title={closeLabel}
          lede={`Mở → Close Review → Closed → Reopened → Reclosed. Tạo ${snapshotLabel.toLowerCase()} bất biến; không sửa ngầm kỳ đã chốt. P&L sau chốt đọc từ snapshot — không ghi đè ${billLabel}.`}
          action={
            <Link className="btn" href="/financial-closes/new">
              + Mở {closeLabel.toLowerCase()}
            </Link>
          }
        />

        {result.ok ? <StatCardGrid cards={statCards} /> : null}

        <div className="filter-tabs" role="group" aria-label="Bộ lọc chốt">
          <Link
            className={!sp.status && !sp.scopeType ? "active" : undefined}
            href="/financial-closes"
          >
            Tất cả
          </Link>
          <Link
            className={sp.status === "open" ? "active" : undefined}
            href="/financial-closes?status=open"
          >
            Đang mở
          </Link>
          <Link
            className={sp.status === "locked" ? "active" : undefined}
            href="/financial-closes?status=locked"
          >
            Đã khóa
          </Link>
          <Link
            className={sp.scopeType === "bill" ? "active" : undefined}
            href="/financial-closes?scopeType=bill"
          >
            Theo {billLabel}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : items.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có lần {closeLabel.toLowerCase()}.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Phạm vi</th>
                  <th scope="col">Kỳ / {billLabel}</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Snapshot</th>
                  <th scope="col">Bắt đầu</th>
                  <th scope="col">Checklist</th>
                </tr>
              </thead>
              <tbody>
                {items.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <Link
                        className="row-link"
                        href={`/financial-closes/${row.id}`}
                      >
                        {scopeTypeLabel(terms, row.scopeType)} · v
                        {row.versionNo}
                      </Link>
                    </td>
                    <td>
                      {row.scopeType === "bill" && row.scopeId ? (
                        <Link
                          className="row-link"
                          href={`/bills/${row.scopeId}`}
                        >
                          {billLabel}
                        </Link>
                      ) : row.periodFrom || row.periodTo ? (
                        `${row.periodFrom ?? "…"} → ${row.periodTo ?? "…"}`
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>{closeStatusLabel(terms, row.status)}</td>
                    <td>{row.snapshots?.length ?? 0}</td>
                    <td>
                      {row.startedAt
                        ? formatDateTimeVi(row.startedAt)
                        : "—"}
                    </td>
                    <td>
                      <ol className="maturity-stepper compact">
                        <li className={row.startedAt ? "is-done" : undefined}>
                          Bắt đầu
                        </li>
                        <li
                          className={
                            (row.snapshots?.length ?? 0) > 0
                              ? "is-done"
                              : undefined
                          }
                        >
                          Snapshot
                        </li>
                        <li className={row.lockedAt ? "is-done" : undefined}>
                          Khóa
                        </li>
                      </ol>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}
