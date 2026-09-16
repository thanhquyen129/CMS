import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid } from "@/components/list/StatCardGrid";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listReconciliations,
  reconciliationStatusLabel,
  reconciliationTypeLabel,
} from "@/lib/reconciliations";
import { formatDateTimeVi } from "@/lib/money";

type SearchParams = Promise<{ status?: string }>;

export default async function ReconciliationsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { status } = await searchParams;
  const terms = await fetchTerminology();
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  const billLabel = term(terms, "BILL", "Bill");
  const queueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");

  const [result, allRes] = await Promise.all([
    listReconciliations(status ? { status } : undefined),
    status ? listReconciliations() : Promise.resolve(null),
  ]);
  const kpiSource =
    allRes && allRes.ok ? allRes.data : result.ok ? result.data : [];
  const inProgress = kpiSource.filter(
    (r) => r.status?.toLowerCase() === "in_progress"
  ).length;
  const completed = kpiSource.filter(
    (r) => r.status?.toLowerCase() === "completed"
  ).length;

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/control", label: "Kiểm soát tài chính" },
            { label: reconLabel },
          ]}
          title={reconLabel}
          lede={`Phiên ${reconLabel.toLowerCase()} thủ công. Chênh lệch ≠ ngoại lệ; hoàn tất phiên khi xong.`}
          action={
            <Link className="btn" href="/reconciliations/new">
              + Mở phiên mới
            </Link>
          }
        />

        <StatCardGrid
          cards={[
            {
              key: "total",
              label: `Tổng phiên`,
              value: kpiSource.length,
            },
            {
              key: "progress",
              label: "Đang đối soát",
              value: inProgress,
              tone: inProgress > 0 ? "warning" : "default",
              href: "/reconciliations?status=in_progress",
            },
            {
              key: "done",
              label: "Đã hoàn tất",
              value: completed,
              tone: "success",
              href: "/reconciliations?status=completed",
            },
            {
              key: "queue",
              label: queueLabel,
              value: "→",
              href: "/queues/reconciliations",
            },
          ]}
        />

        <div className="filter-tabs" role="tablist" aria-label="Lọc trạng thái">
          <Link
            className={!status ? "active" : undefined}
            href="/reconciliations"
            role="tab"
            aria-selected={!status}
          >
            Tất cả
          </Link>
          <Link
            className={status === "in_progress" ? "active" : undefined}
            href="/reconciliations?status=in_progress"
            role="tab"
            aria-selected={status === "in_progress"}
          >
            Đang đối soát
          </Link>
          <Link
            className={status === "completed" ? "active" : undefined}
            href="/reconciliations?status=completed"
            role="tab"
            aria-selected={status === "completed"}
          >
            Đã hoàn tất
          </Link>
          <Link className="btn btn-ghost btn-sm" href="/queues/reconciliations">
            {queueLabel}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có phiên {reconLabel.toLowerCase()}.{" "}
            <Link href="/reconciliations/new">Mở phiên mới</Link>.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Loại</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Dòng</th>
                  <th scope="col">Bắt đầu</th>
                  <th scope="col">{billLabel}</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => (
                  <tr key={item.id}>
                    <td>
                      {reconciliationTypeLabel(terms, item.reconciliationType)}
                      <span className="muted small block">v{item.versionNo}</span>
                    </td>
                    <td>{reconciliationStatusLabel(terms, item.status)}</td>
                    <td>{item.details?.length ?? 0}</td>
                    <td>
                      {item.startedAt
                        ? formatDateTimeVi(item.startedAt)
                        : "—"}
                    </td>
                    <td>
                      {item.billId ? (
                        <Link
                          className="row-link"
                          href={`/bills/${item.billId}`}
                        >
                          Mở {billLabel}
                        </Link>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>
                      <Link
                        className="row-link"
                        href={`/reconciliations/${item.id}`}
                      >
                        Chi tiết
                      </Link>
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
