import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listReconciliationQueue,
  reconciliationStatusLabel,
  reconciliationTypeLabel,
} from "@/lib/reconciliations";
import { formatDateTimeVi } from "@/lib/money";

export default async function ReconciliationQueuePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const queueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  const billLabel = term(terms, "BILL", "Bill");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const result = await listReconciliationQueue();

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          {queueLabel}
        </p>
        <h1>{queueLabel}</h1>
        <p className="lede">
          Phiên {reconLabel.toLowerCase()} đang mở (nháp / đang đối soát). Mở
          phiên để thêm dòng hoặc hoàn tất.
        </p>

        <div className="cta-row">
          <Link className="btn" href="/reconciliations/new">
            Mở phiên mới
          </Link>
          <Link className="btn btn-ghost" href="/reconciliations">
            Tất cả phiên
          </Link>
          <Link className="btn btn-ghost" href="/bank-feed">
            {term(terms, "BANK_FEED", "Sao kê ngân hàng")}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Không có phiên {reconLabel.toLowerCase()} đang mở.{" "}
            <Link href="/reconciliations/new">Mở phiên mới</Link>.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Loại</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Phiên bản</th>
                  <th scope="col">Bắt đầu</th>
                  <th scope="col">{billLabel}</th>
                  <th scope="col">Ghi chú</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => (
                  <tr key={item.id}>
                    <td>{reconciliationTypeLabel(terms, item.reconciliationType)}</td>
                    <td>{reconciliationStatusLabel(terms, item.status)}</td>
                    <td>v{item.versionNo}</td>
                    <td>
                      {item.startedAt
                        ? formatDateTimeVi(item.startedAt)
                        : "—"}
                    </td>
                    <td>
                      {item.billId ? (
                        <Link className="row-link" href={`/bills/${item.billId}`}>
                          Mở {billLabel}
                        </Link>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>{item.notes?.trim() || "—"}</td>
                    <td>
                      <Link
                        className="row-link"
                        href={`/reconciliations/${item.id}`}
                      >
                        Mở phiên
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
