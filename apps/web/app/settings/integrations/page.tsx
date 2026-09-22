import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getIntegrationJobHealth, listIntegrationErrors } from "@/lib/integration-errors";
import { formatDateTimeVi } from "@/lib/money";
import { listIntegrationRecords } from "@/lib/tenant-admin";

export default async function SettingsIntegrationsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const [recordsResult, errorsResult, healthResult] = await Promise.all([
    listIntegrationRecords(),
    listIntegrationErrors({ recoveryStatus: "pending" }),
    getIntegrationJobHealth(),
  ]);

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Tích hợp API" },
          ]}
          title="Tích hợp API"
          lede="Bản ghi đồng bộ inbound và hàng đợi lỗi. Hệ thống vận hành vẫn là SoT nghiệp vụ (H-002); CMS ghi nhận và xử lý lỗi tài chính."
        />
        <SettingsHubNav active="integrations" />
        <p className="note">
          Xử lý dead-letter / thử lại tại{" "}
          <Link href="/integration-errors">Hàng đợi lỗi tích hợp</Link>. Chi tiết lỗi đã che secret.
        </p>

        <fieldset className="group-box">
          <legend>Tình trạng job</legend>
          {!healthResult.ok ? (
            <div className="alert alert-error" role="alert">
              {healthResult.message}
            </div>
          ) : (
            <>
              <p>
                Outbox chờ: <strong>{healthResult.data.outboxPending}</strong> · Lỗi chờ:{" "}
                <strong>{healthResult.data.integrationErrorsPending}</strong> · Dead-letter:{" "}
                <strong>{healthResult.data.integrationErrorsDeadLetter}</strong>
              </p>
              {healthResult.data.topActionableErrors.length > 0 ? (
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th scope="col">Mã</th>
                        <th scope="col">Nội dung</th>
                        <th scope="col">Hành động gợi ý</th>
                      </tr>
                    </thead>
                    <tbody>
                      {healthResult.data.topActionableErrors.map((e) => (
                        <tr key={e.id}>
                          <td className="mono-id">{e.errorCode}</td>
                          <td>{e.message}</td>
                          <td>{e.nextAction}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="empty-state" role="status">
                  Không có lỗi cần hành động.
                </div>
              )}
            </>
          )}
        </fieldset>

        <fieldset className="group-box" style={{ marginTop: "1.25rem" }}>
          <legend>Bản ghi đồng bộ gần đây</legend>
          {!recordsResult.ok ? (
            <div className="alert alert-error" role="alert">
              {recordsResult.message}
            </div>
          ) : recordsResult.data.length === 0 ? (
            <div className="empty-state" role="status">
              Chưa có bản ghi tích hợp.
            </div>
          ) : (
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Nguồn</th>
                    <th scope="col">Đối tượng ngoài</th>
                    <th scope="col">Trạng thái</th>
                    <th scope="col">Nhận lúc</th>
                    <th scope="col">Lỗi</th>
                  </tr>
                </thead>
                <tbody>
                  {recordsResult.data.map((r) => (
                    <tr key={r.id}>
                      <td>{r.sourceSystem}</td>
                      <td>
                        {r.externalObjectType} / {r.externalId}
                      </td>
                      <td>{r.status}</td>
                      <td>{formatDateTimeVi(r.receivedAt)}</td>
                      <td>{r.errorCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </fieldset>

        <fieldset className="group-box" style={{ marginTop: "1.25rem" }}>
          <legend>Lỗi đang chờ</legend>
          {!errorsResult.ok ? (
            <div className="alert alert-error" role="alert">
              {errorsResult.message}
            </div>
          ) : errorsResult.data.length === 0 ? (
            <div className="empty-state" role="status">
              Không có lỗi chờ xử lý.
            </div>
          ) : (
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Mã</th>
                    <th scope="col">Nội dung</th>
                    <th scope="col">Lần thử</th>
                    <th scope="col">Lúc</th>
                  </tr>
                </thead>
                <tbody>
                  {errorsResult.data.slice(0, 20).map((e) => (
                    <tr key={e.id}>
                      <td className="mono-id">{e.errorCode}</td>
                      <td>{e.message}</td>
                      <td>{e.attemptNo}</td>
                      <td>{formatDateTimeVi(e.occurredAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </fieldset>
      </section>
    </AppShell>
  );
}
