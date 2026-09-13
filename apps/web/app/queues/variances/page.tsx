import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { EscalateVarianceButton } from "@/components/EscalateVarianceButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listVariances,
  objectTypeLabel,
  severityLabel,
  varianceStatusLabel,
  varianceTypeLabel,
} from "@/lib/control-desk";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ status?: string }>;

const STATUS_FILTERS = [
  { id: "open", labelKey: "open" as const },
  { id: "accepted", labelKey: "accepted" as const },
  { id: "cleared", labelKey: "cleared" as const },
  { id: "written_off", labelKey: "written_off" as const },
] as const;

export default async function VarianceQueuePage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { status: statusRaw } = await searchParams;
  const status = (statusRaw?.trim() || "open").toLowerCase();

  const terms = await fetchTerminology();
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");
  const queueTitle = `Hàng đợi ${varianceLabel.toLowerCase()}`;
  const exceptionLabel = term(terms, "EXCEPTION", "Ngoại lệ");
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const result = await listVariances({ status });

  return (
    <AppShell terms={terms} active="variances">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          {queueTitle}
        </p>
        <h1>{queueTitle}</h1>
        <p className="lede">
          {varianceLabel} là sự kiện kiểm soát số liệu — không tự mở{" "}
          {exceptionLabel.toLowerCase()}. CTA mở ngoại lệ thủ công khi cần
          controller xử lý (giữ tách lớp).
        </p>

        <div className="search-bar" role="group" aria-label="Bộ lọc trạng thái">
          {STATUS_FILTERS.map((f) => (
            <Link
              key={f.id}
              className={
                status === f.id ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={
                f.id === "open"
                  ? "/queues/variances"
                  : `/queues/variances?status=${f.id}`
              }
            >
              {varianceStatusLabel(terms, f.id)}
            </Link>
          ))}
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Không có {varianceLabel.toLowerCase()} trạng thái «
            {varianceStatusLabel(terms, status)}».
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Số tiền</th>
                  <th scope="col">Mức độ</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Loại</th>
                  <th scope="col">Nguồn</th>
                  <th scope="col">{reconLabel}</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <div className="queue-title">
                        {formatMoney(item.amount, item.currencyCode)}
                      </div>
                      {item.explanation ? (
                        <span className="muted small block">
                          {item.explanation}
                        </span>
                      ) : (
                        <span className="muted small block mono-id">
                          {item.id.slice(0, 8)}…
                        </span>
                      )}
                    </td>
                    <td>
                      <span
                        className={`severity-pill severity-${item.severity.toLowerCase()}`}
                      >
                        {severityLabel(item.severity)}
                      </span>
                    </td>
                    <td>{varianceStatusLabel(terms, item.status)}</td>
                    <td>{varianceTypeLabel(item.varianceType)}</td>
                    <td>
                      {objectTypeLabel(terms, item.sourceType)}
                      <span className="muted small block mono-id">
                        {item.sourceId.slice(0, 8)}…
                      </span>
                    </td>
                    <td>
                      {item.reconciliationId ? (
                        <Link
                          className="row-link"
                          href={`/reconciliations/${item.reconciliationId}`}
                        >
                          Mở {reconLabel.toLowerCase()}
                        </Link>
                      ) : (
                        <span className="muted">—</span>
                      )}
                    </td>
                    <td>
                      <EscalateVarianceButton terms={terms} variance={item} />
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
