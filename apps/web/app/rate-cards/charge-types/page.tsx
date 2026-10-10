import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { EconomicChargeTypeForm } from "@/components/EconomicChargeTypeForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getChargeVatReadiness, listEconomicChargeTypes } from "@/lib/rate-cards-server";
import type { ApiResult } from "@/lib/bills";

type ChargeType = { id: string; code: string; name: string; isActive: boolean };

export default async function ChargeTypesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const list = await listEconomicChargeTypes();
  const readiness = await getChargeVatReadiness();

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href="/rate-cards">← Bảng giá</Link>
        </p>
        <h1>Khoản mục kinh tế</h1>
        <p className="lede">Dùng để ghép chi phí mua và doanh thu bán cùng loại, ví dụ phí đóng gói.</p>
        <EconomicChargeTypeForm />
        <ChargeTable list={list} />
        {readiness.ok ? (
          <div className="note">
            <p>
              VAT chưa khai báo: bảng giá {readiness.data.rateVersionsMissingVat}, phụ phí {readiness.data.surchargeVersionsMissingVat}, tính giá {readiness.data.ratingDetailsMissingVat}, chi phí {readiness.data.costsMissingVat}, doanh thu {readiness.data.revenuesMissingVat}.
            </p>
            <p>
              Chưa gắn khoản mục: phụ phí {readiness.data.surchargeVersionsUnmapped}, chi phí {readiness.data.costsUnmapped}, doanh thu {readiness.data.revenuesUnmapped}, tính giá {readiness.data.ratingDetailsUnmapped}.
            </p>
            <p className="muted small">{readiness.data.note}</p>
          </div>
        ) : (
          <div className="alert alert-error" role="alert">{readiness.message}</div>
        )}
      </section>
    </AppShell>
  );
}

function ChargeTable({ list }: { list: ApiResult<ChargeType[]> }) {
  if (!list.ok) {
    return <div className="alert alert-error" role="alert">{list.message}</div>;
  }
  if (list.data.length === 0) {
    return <div className="empty-state" role="status">Chưa có khoản mục. Thêm mã trước khi đối chiếu lãi lỗ.</div>;
  }
  return (
    <table className="data-table">
      <thead>
        <tr>
          <th scope="col">Mã</th>
          <th scope="col">Tên</th>
        </tr>
      </thead>
      <tbody>
        {list.data.map((row) => (
          <tr key={row.id}>
            <td>{row.code}</td>
            <td>{row.name}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
