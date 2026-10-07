import { cookies } from "next/headers";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SurchargeForm } from "@/components/SurchargeForm";
import { SurchargeVersionActions } from "@/components/SurchargeVersionActions";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateTimeVi, formatDateVi } from "@/lib/money";
import { listRateCards, getSurcharge } from "@/lib/rate-cards-server";
import { unwrapPaged } from "@/lib/paging";

type Params = Promise<{ id: string }>;

export default async function SurchargeDetailPage({ params }: { params: Params }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const { id } = await params;
  const terms = await fetchTerminology();
  const detail = await getSurcharge(id);
  if (!detail.ok) {
    if (detail.status === 404) notFound();
    return (
      <AppShell terms={terms} active="rate-cards">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">{detail.message}</div>
        </section>
      </AppShell>
    );
  }

  const cards = await listRateCards({ active: true, pageSize: 100 });
  const options = cards.ok
    ? unwrapPaged(cards.data).items.map((card) => ({ id: card.id, label: `${card.code} — ${card.name}` }))
    : [];
  const row = detail.data;
  const current = row.versions[0];
  const rule = current?.rules[0];
  const draft = current?.publishStatus === "draft";

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href="/rate-cards/surcharges">← Quản lý phụ phí</Link>
        </p>
        <h1>{row.name}</h1>
        <p className="muted">
          <code>{row.code}</code> · {row.direction === "sell" ? "Bán" : row.direction === "both" ? "Cả hai" : "Mua"}
          {row.sourceLegacyComponentId ? " · chuyển từ thành phần bảng giá nháp" : ""}
        </p>
        {current ? (
          <SurchargeVersionActions surchargeId={row.id} versionId={current.id} published={!draft} />
        ) : null}
        <table className="data-table">
          <thead>
            <tr>
              <th>Phiên bản</th>
              <th>Trạng thái</th>
              <th>Hiệu lực</th>
              <th>Cách tính</th>
              <th>Giá trị</th>
            </tr>
          </thead>
          <tbody>
            {row.versions.map((version) => (
              <tr key={version.id}>
                <td>v{version.versionNo}</td>
                <td>{version.publishStatus === "published" ? "Đã phát hành" : "Nháp"}</td>
                <td>
                  {formatDateVi(version.validFrom)} – {formatDateVi(version.validTo)}
                  {version.publishedAt ? ` · ${formatDateTimeVi(version.publishedAt)}` : ""}
                </td>
                <td>{version.rules.map((item) => item.calculationMode).join(", ") || "—"}</td>
                <td>{version.rules.map((item) => `${item.rateAmountPercent} ${item.currencyCode}`).join(", ") || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {draft && current && rule ? (
          <>
            <h2>Sửa phiên bản nháp</h2>
            <SurchargeForm
              cards={options}
              lockCode
              surchargeId={row.id}
              versionId={current.id}
              initial={{
                code: row.code,
                name: row.name,
                direction: row.direction,
                calculationMode: rule.calculationMode,
                basis: rule.basis,
                currencyCode: rule.currencyCode,
                rateAmountPercent: rule.rateAmountPercent,
                transportMode: rule.transportMode,
                routeCode: rule.routeCode,
                dangerousGoods: rule.dangerousGoods,
                rateCardId: rule.rateCardId,
                validFrom: current.validFrom,
                validTo: current.validTo,
              }}
            />
          </>
        ) : (
          <p className="muted">Phiên bản đã phát hành giữ nguyên. Bấm tạo phiên bản mới để đổi đơn giá hoặc điều kiện.</p>
        )}
      </section>
    </AppShell>
  );
}
