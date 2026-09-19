import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { UpsertFxRateForm } from "@/components/UpsertFxRateForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listFxRates } from "@/lib/catalog";

export default async function AdminFxRatesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const result = await listFxRates();

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/admin", label: "Danh mục" },
            { href: "/admin/currencies", label: "Tiền tệ" },
            { label: "Tỷ giá" },
          ]}
          title="Tỷ giá ngoại tệ"
          lede="Tỷ giá theo ngày và phiên bản. Không tính lại lịch sử đã ghi trên chứng từ."
        />

        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">
                {result.message}
              </div>
            ) : result.data.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có tỷ giá. Ghi cặp thủ công khi không tích hợp nguồn FX.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Cặp</th>
                      <th scope="col">Ngày</th>
                      <th scope="col" className="num">
                        Tỷ giá
                      </th>
                      <th scope="col">Nguồn</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.data.map((r) => (
                      <tr key={r.id}>
                        <td className="mono-id">
                          {r.fromCurrencyCode}/{r.toCurrencyCode}
                        </td>
                        <td>{r.rateDate}</td>
                        <td className="num">{r.rate}</td>
                        <td>
                          {r.source}
                          {r.note ? (
                            <div className="muted small">{r.note}</div>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </fieldset>
          <fieldset className="group-box">
            <legend>Ghi tỷ giá</legend>
            <UpsertFxRateForm />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
