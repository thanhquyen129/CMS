import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { IntegrationErrorActions } from "@/components/IntegrationErrorActions";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  integrationRecoveryStatusLabel,
  listIntegrationErrors,
} from "@/lib/integration-errors";
import { formatDateTimeVi } from "@/lib/money";

type SearchParams = Promise<{ recoveryStatus?: string }>;

const FILTERS = [
  { value: "pending", label: "Chờ xử lý" },
  { value: "retried", label: "Đã thử lại" },
  { value: "dead_letter", label: "Dead letter" },
] as const;

export default async function IntegrationErrorsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { recoveryStatus: rawStatus } = await searchParams;
  const recoveryStatus = rawStatus?.trim() || "pending";

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const result = await listIntegrationErrors(
    recoveryStatus === "all" ? undefined : { recoveryStatus }
  );

  return (
    <AppShell terms={terms} active="integration-errors">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          Lỗi tích hợp
        </p>
        <h1>Lỗi tích hợp</h1>
        <p className="lede">
          Theo dõi lỗi đồng bộ tích hợp và phục hồi thủ công: đánh dấu thử lại
          hoặc chuyển dead letter. Mặc định hiển thị lỗi đang chờ xử lý.
        </p>

        <div className="search-bar" role="tablist" aria-label="Lọc trạng thái phục hồi">
          {FILTERS.map((f) => (
            <Link
              key={f.value}
              className={
                recoveryStatus === f.value ? "btn" : "btn btn-ghost"
              }
              href={`/integration-errors?recoveryStatus=${f.value}`}
              role="tab"
              aria-selected={recoveryStatus === f.value}
            >
              {f.label}
            </Link>
          ))}
          <Link
            className={recoveryStatus === "all" ? "btn" : "btn btn-ghost"}
            href="/integration-errors?recoveryStatus=all"
            role="tab"
            aria-selected={recoveryStatus === "all"}
          >
            Tất cả
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            {recoveryStatus === "pending"
              ? "Không có lỗi tích hợp đang chờ xử lý."
              : "Không có lỗi tích hợp trong bộ lọc này."}
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Thời điểm</th>
                  <th scope="col">Mã lỗi</th>
                  <th scope="col">Thông báo</th>
                  <th scope="col">Lần thử</th>
                  <th scope="col">Trạng thái PH</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => (
                  <tr key={item.id}>
                    <td>{formatDateTimeVi(item.occurredAt)}</td>
                    <td>
                      <span className="mono-id">{item.errorCode}</span>
                    </td>
                    <td>
                      <div>{item.message}</div>
                      {item.detail ? (
                        <span className="muted small block">{item.detail}</span>
                      ) : null}
                    </td>
                    <td>{item.attemptNo}</td>
                    <td>
                      {integrationRecoveryStatusLabel(item.recoveryStatus)}
                      {item.recoveryNote ? (
                        <span className="muted small block">
                          {item.recoveryNote}
                        </span>
                      ) : null}
                    </td>
                    <td>
                      <IntegrationErrorActions
                        errorId={item.id}
                        recoveryStatus={item.recoveryStatus}
                      />
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
