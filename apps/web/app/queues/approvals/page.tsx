import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { DecideApprovalButton } from "@/components/DecideApprovalButton";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  approvalStatusLabel,
  isPendingApproval,
  listApprovalQueue,
  objectHrefFromApproval,
  objectTypeLabel,
} from "@/lib/control-desk";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

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
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const result = await listApprovalQueue();

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
          lede={`${approvalLabel} đang chờ quyết định. Phê duyệt / từ chối tại đây — ${approvalLabel} ≠ quyền hệ thống. Mở ${billLabel} / ${docLabel} khi có màn chi tiết.`}
        />

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
                  <th scope="col">Số tiền</th>
                  <th scope="col">Người yêu cầu</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">{levelLabel}</th>
                  <th scope="col">Yêu cầu lúc</th>
                  <th scope="col">Lý do</th>
                  <th scope="col">Liên kết</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((item) => {
                  const href = item.detailPath || objectHrefFromApproval(item);
                  const t = item.objectType.toLowerCase();
                  const linkLabel =
                    t === "bill"
                      ? `Mở ${billLabel}`
                      : t === "financial_document" || t === "document"
                        ? `Mở ${docLabel}`
                        : t === "cost_allocation"
                      ? "Mở phân bổ"
                    : t === "payment"
                          ? "Mở thanh toán"
                          : t === "collection"
                            ? "Mở thu tiền"
                            : "Mở";
                  return (
                    <tr key={item.id}>
                      <td>
                        <div className="queue-title">
                          {objectTypeLabel(terms, item.objectType)}
                          {item.businessCode ? ` · ${item.businessCode}` : ""}
                        </div>
                      </td>
                      <td>
                        {item.amount != null && item.currencyCode
                          ? formatMoney(item.amount, item.currencyCode)
                          : "—"}
                      </td>
                      <td>{item.requestedByName || "—"}</td>
                      <td>{approvalStatusLabel(terms, item.status)}</td>
                      <td>
                        {item.currentLevel}/{item.requiredLevel}
                      </td>
                      <td>{formatDateTimeVi(item.requestedAt)}</td>
                      <td>{item.requestReason?.trim() || "—"}</td>
                      <td>
                        {href ? (
                          <Link className="row-link" href={href}>
                            {linkLabel}
                          </Link>
                        ) : (
                          <span
                            className="muted"
                            title="Chưa có màn chi tiết cho loại này"
                          >
                            —
                          </span>
                        )}
                      </td>
                      <td>
                        {isPendingApproval(item.status) ? (
                          <DecideApprovalButton
                            terms={terms}
                            approvalId={item.id}
                            currentLevel={item.currentLevel}
                            requiredLevel={item.requiredLevel}
                          />
                        ) : (
                          "—"
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
