import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateTimeVi } from "@/lib/money";
import {
  auditActionLabel,
  auditObjectLabel,
  listAuditEvents,
} from "@/lib/tenant-admin";

type Search = {
  action?: string;
  objectType?: string;
  from?: string;
  to?: string;
  skip?: string;
};

const PAGE_SIZE = 50;

export default async function SettingsAuditPage({
  searchParams,
}: {
  searchParams: Promise<Search>;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const skip = Math.max(0, Number.parseInt(sp.skip ?? "0", 10) || 0);
  const qs = new URLSearchParams();
  if (sp.action) qs.set("action", sp.action);
  if (sp.objectType) qs.set("objectType", sp.objectType);
  if (sp.from) qs.set("from", new Date(`${sp.from}T00:00:00`).toISOString());
  if (sp.to) qs.set("to", new Date(`${sp.to}T23:59:59`).toISOString());
  qs.set("skip", String(skip));
  qs.set("take", String(PAGE_SIZE));

  const terms = await fetchTerminology();
  const result = await listAuditEvents(qs.toString());
  const rows = result.ok ? result.data : [];
  const nextSkip = skip + PAGE_SIZE;
  const prevSkip = Math.max(0, skip - PAGE_SIZE);

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Nhật ký hệ thống" },
          ]}
          title="Nhật ký hệ thống"
          lede="Audit bất biến theo thuê bao: ai, làm gì, trên đối tượng nào, trước/sau. Không sửa, không xóa."
        />
        <SettingsHubNav active="audit" />

        <form className="filter-bar" method="get">
          <div className="filter-field">
            <label htmlFor="auditAction">Hành động</label>
            <input
              id="auditAction"
              name="action"
              defaultValue={sp.action ?? ""}
              placeholder="vd. user.create"
            />
          </div>
          <div className="filter-field">
            <label htmlFor="auditObject">Loại đối tượng</label>
            <input
              id="auditObject"
              name="objectType"
              defaultValue={sp.objectType ?? ""}
              placeholder="vd. user, tenant, cost"
            />
          </div>
          <div className="filter-field">
            <label htmlFor="auditFrom">Từ ngày</label>
            <input id="auditFrom" name="from" type="date" defaultValue={sp.from ?? ""} />
          </div>
          <div className="filter-field">
            <label htmlFor="auditTo">Đến ngày</label>
            <input id="auditTo" name="to" type="date" defaultValue={sp.to ?? ""} />
          </div>
          <button type="submit" className="btn">
            Lọc
          </button>
        </form>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : rows.length === 0 ? (
          <div className="empty-state" role="status">
            Không có sự kiện trong bộ lọc này.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Thời điểm (UTC lưu, hiển thị local)</th>
                  <th scope="col">Người thực hiện</th>
                  <th scope="col">Hành động</th>
                  <th scope="col">Đối tượng</th>
                  <th scope="col">Lý do / tương quan</th>
                  <th scope="col">Trước / sau</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((e) => (
                  <tr key={e.id}>
                    <td>{formatDateTimeVi(e.occurredAt)}</td>
                    <td>{e.actorDisplayName || e.actorId || "Hệ thống"}</td>
                    <td>
                      {auditActionLabel(e.action)}
                      <div className="muted small">{e.action}</div>
                    </td>
                    <td>
                      {auditObjectLabel(e.objectType)}
                      <div className="mono-id muted">{e.objectId}</div>
                    </td>
                    <td>
                      {e.reason || "—"}
                      {e.correlationId ? (
                        <div className="mono-id muted">{e.correlationId}</div>
                      ) : null}
                    </td>
                    <td>
                      {e.beforeJson || e.afterJson ? (
                        <details>
                          <summary>Xem JSON</summary>
                          {e.beforeJson ? (
                            <pre className="mono-id">{e.beforeJson}</pre>
                          ) : null}
                          {e.afterJson ? (
                            <pre className="mono-id">{e.afterJson}</pre>
                          ) : null}
                        </details>
                      ) : (
                        "—"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="cta-row">
          {skip > 0 ? (
            <a
              className="btn btn-ghost"
              href={`/settings/audit?${new URLSearchParams({
                ...(sp.action ? { action: sp.action } : {}),
                ...(sp.objectType ? { objectType: sp.objectType } : {}),
                ...(sp.from ? { from: sp.from } : {}),
                ...(sp.to ? { to: sp.to } : {}),
                skip: String(prevSkip),
              }).toString()}`}
            >
              Trang trước
            </a>
          ) : null}
          {rows.length >= PAGE_SIZE ? (
            <a
              className="btn btn-ghost"
              href={`/settings/audit?${new URLSearchParams({
                ...(sp.action ? { action: sp.action } : {}),
                ...(sp.objectType ? { objectType: sp.objectType } : {}),
                ...(sp.from ? { from: sp.from } : {}),
                ...(sp.to ? { to: sp.to } : {}),
                skip: String(nextSkip),
              }).toString()}`}
            >
              Trang sau
            </a>
          ) : null}
        </div>
      </section>
    </AppShell>
  );
}
