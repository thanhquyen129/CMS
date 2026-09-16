import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
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

  const result = await listReconciliations(
    status ? { status } : undefined
  );

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <h1>{reconLabel}</h1>
        <p className="lede">
          Phiên {reconLabel.toLowerCase()} thủ công. Chênh lệch ≠ ngoại lệ;
          hoàn tất phiên khi xong.
        </p>

        <div className="search-bar" role="tablist" aria-label="Lọc trạng thái">
          <Link
            className={!status ? "btn" : "btn btn-ghost"}
            href="/reconciliations"
            role="tab"
            aria-selected={!status}
          >
            Tất cả
          </Link>
          <Link
            className={status === "in_progress" ? "btn" : "btn btn-ghost"}
            href="/reconciliations?status=in_progress"
            role="tab"
            aria-selected={status === "in_progress"}
          >
            Đang đối soát
          </Link>
          <Link
            className={status === "completed" ? "btn" : "btn btn-ghost"}
            href="/reconciliations?status=completed"
            role="tab"
            aria-selected={status === "completed"}
          >
            Đã hoàn tất
          </Link>
          <Link className="btn" href="/reconciliations/new">
            Mở phiên mới
          </Link>
          <Link className="btn btn-ghost" href="/queues/reconciliations">
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
