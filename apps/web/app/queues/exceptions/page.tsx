import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ExceptionActionButtons } from "@/components/ExceptionActionButtons";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  billHrefFromException,
  exceptionStatusLabel,
  isOverdue,
  listExceptionQueue,
  objectTypeLabel,
  severityLabel,
} from "@/lib/control-desk";
import { formatDateTimeVi } from "@/lib/money";

type SearchParams = Promise<{ overdueOnly?: string }>;

export default async function ExceptionQueuePage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { overdueOnly } = await searchParams;
  const overdueFilter = overdueOnly === "1" || overdueOnly === "true";

  const terms = await fetchTerminology();
  const queueLabel = term(terms, "EXCEPTION_QUEUE", "Hàng đợi ngoại lệ");
  const exceptionLabel = term(terms, "EXCEPTION", "Ngoại lệ");
  const billLabel = term(terms, "BILL", "Bill");
  const slaLabel = term(terms, "EXCEPTION_SLA_DUE", "Hạn xử lý ngoại lệ (SLA)");
  const overdueTerm = term(terms, "EXCEPTION_OVERDUE", "Ngoại lệ quá hạn");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const result = await listExceptionQueue({ overdueOnly: overdueFilter });

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: dashboardLabel },
            { href: "/control", label: "Kiểm soát tài chính" },
            { label: queueLabel },
          ]}
          title={queueLabel}
          lede={`${exceptionLabel} đang mở / đang xử lý / leo thang cần controller xem xét. Xử lý · leo thang · đóng tại đây. Mở ${billLabel} khi có liên kết.`}
        />

        <div className="filter-tabs" role="group" aria-label="Bộ lọc hàng đợi">
          {overdueFilter ? (
            <Link href="/queues/exceptions">Hiện tất cả đang mở</Link>
          ) : (
            <Link
              className="active"
              href="/queues/exceptions?overdueOnly=1"
            >
              Chỉ {overdueTerm}
            </Link>
          )}
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            {overdueFilter
              ? `Không có ${overdueTerm} trong phạm vi của bạn.`
              : `Hàng đợi trống — không có ${exceptionLabel} đang mở cần xử lý.`}
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Tiêu đề</th>
                  <th scope="col">Mức độ</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Ngày tạo</th>
                  <th scope="col">{slaLabel}</th>
                  <th scope="col">Đối tượng</th>
                  <th scope="col">{billLabel}</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => {
                  const href = billHrefFromException(item);
                  const overdue = isOverdue(item.dueAt);
                  return (
                    <tr key={item.id}>
                      <td>
                        <div className="queue-title">{item.title}</div>
                        {item.ruleCode ? (
                          <span className="muted small block">
                            Quy tắc: {item.ruleCode}
                          </span>
                        ) : null}
                      </td>
                      <td>
                        <span
                          className={`severity-pill severity-${item.severity.toLowerCase()}`}
                        >
                          {severityLabel(item.severity)}
                        </span>
                      </td>
                      <td>{exceptionStatusLabel(terms, item.status)}</td>
                      <td>
                        {item.createdAt
                          ? formatDateTimeVi(item.createdAt)
                          : "—"}
                      </td>
                      <td>
                        {item.dueAt ? (
                          <span className={overdue ? "neg" : undefined}>
                            {formatDateTimeVi(item.dueAt)}
                            {overdue ? ` · ${overdueTerm}` : ""}
                          </span>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td>
                        {objectTypeLabel(terms, item.objectType)}
                        {item.objectId ? (
                          <span className="muted small block mono-id">
                            {item.objectId.slice(0, 8)}…
                          </span>
                        ) : null}
                      </td>
                      <td>
                        {href ? (
                          <Link className="row-link" href={href}>
                            Mở {billLabel}
                          </Link>
                        ) : (
                          <span className="muted">—</span>
                        )}
                      </td>
                      <td>
                        <ExceptionActionButtons
                          terms={terms}
                          exceptionId={item.id}
                          status={item.status}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}
