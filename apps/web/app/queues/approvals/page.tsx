import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  approvalStatusLabel,
  billHrefFromApproval,
  listApprovalQueue,
  objectTypeLabel,
} from "@/lib/control-desk";
import { formatDateTimeVi } from "@/lib/money";

export default async function ApprovalQueuePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const queueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");
  const approvalLabel = term(terms, "APPROVAL", "Phê duyệt");
  const levelLabel = term(terms, "APPROVAL_LEVEL", "Cấp phê duyệt");
  const billLabel = term(terms, "BILL", "Bill");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const result = await listApprovalQueue();

  return (
    <AppShell terms={terms} active="approvals">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          {queueLabel}
        </p>
        <h1>{queueLabel}</h1>
        <p className="lede">
          {approvalLabel} đang chờ quyết định. U3 chỉ đọc hàng đợi — quyết định approve/reject
          trên UI sẽ ở sprint sau. Mở {billLabel} khi đối tượng là Bill.
        </p>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Không có {approvalLabel} đang chờ trong phạm vi của bạn.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Đối tượng</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">{levelLabel}</th>
                  <th scope="col">Yêu cầu lúc</th>
                  <th scope="col">Lý do</th>
                  <th scope="col">Liên kết</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => {
                  const href = billHrefFromApproval(item);
                  return (
                    <tr key={item.id}>
                      <td>
                        <div className="queue-title">
                          {objectTypeLabel(terms, item.objectType)}
                        </div>
                        <span className="muted small block mono-id">
                          {item.objectId}
                        </span>
                      </td>
                      <td>{approvalStatusLabel(terms, item.status)}</td>
                      <td>
                        {item.currentLevel}/{item.requiredLevel}
                      </td>
                      <td>{formatDateTimeVi(item.requestedAt)}</td>
                      <td>{item.requestReason?.trim() || "—"}</td>
                      <td>
                        {href ? (
                          <Link className="row-link" href={href}>
                            Mở {billLabel}
                          </Link>
                        ) : (
                          <span className="muted" title="Chưa có màn chi tiết cho loại này">
                            —
                          </span>
                        )}
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
