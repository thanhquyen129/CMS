import { cookies } from "next/headers";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SurchargeForm } from "@/components/SurchargeForm";
import { SurchargeVersionActions } from "@/components/SurchargeVersionActions";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateOnlyVi, formatDateTimeVi } from "@/lib/money";
import { listEconomicChargeTypes, listRateCards, getSurcharge, listActivationServices } from "@/lib/rate-cards-server";
import { unwrapPaged } from "@/lib/paging";
import { listCatalog } from "@/lib/catalog";
import { catalogModeOptions } from "@/lib/create-workspace";

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
  const chargeTypes = await listEconomicChargeTypes();
  const activeTypes = chargeTypes.ok ? chargeTypes.data.filter((row) => row.isActive) : [];
  const modeCatalog = await listCatalog("transport_mode");
  const modeOptions = catalogModeOptions(modeCatalog.ok ? modeCatalog.data : []);
  const actRes = await listActivationServices(false);
  const activationServices = actRes.ok ? actRes.data : [];
  const row = detail.data;
  const draft = row.versions.find((v) => v.publishStatus === "draft");
  const latestPublished = row.versions.find((v) => v.publishStatus === "published");
  const actionVersion = draft ?? latestPublished;
  const rule = draft?.rules[0];
  const unclassified = row.versions.some((v) => v.activationPolicy === "UNCLASSIFIED_LEGACY");

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
        {unclassified ? (
          <div className="alert alert-warning" role="status">
            Có phiên bản chưa phân loại chính sách áp dụng – Hệ thống không tự động cộng vào tính giá mới.
          </div>
        ) : null}
        {actionVersion ? (
          <SurchargeVersionActions
            surchargeId={row.id}
            versionId={actionVersion.id}
            published={actionVersion.publishStatus === "published"}
          />
        ) : null}
        <table className="data-table">
          <thead>
            <tr>
              <th>Phiên bản</th>
              <th>Khoản mục kinh tế</th>
              <th>Trạng thái</th>
              <th>Chính sách kích hoạt</th>
              <th>Hiệu lực từ</th>
              <th>Hiệu lực đến</th>
              <th>Phát hành lúc</th>
              <th>Cách tính</th>
              <th>Giá trị</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {row.versions.map((version) => (
              <tr key={version.id}>
                <td>v{version.versionNo}</td>
                <td>{version.economicChargeTypeCode ? `${version.economicChargeTypeCode} — ${version.economicChargeTypeName}` : "Chưa gắn"}</td>
                <td>{version.publishStatus === "published" ? "Đã phát hành" : "Nháp"}</td>
                <td>{version.activationPolicy || "—"}</td>
                <td>{formatDateOnlyVi(version.validFrom)}</td>
                <td>{formatDateOnlyVi(version.validTo)}</td>
                <td>{version.publishedAt ? formatDateTimeVi(version.publishedAt) : "—"}</td>
                <td>{version.rules.map((item) => item.calculationMode).join(", ") || "—"}</td>
                <td>{version.rules.map((item) => `${item.rateAmountPercent} ${item.currencyCode}`).join(", ") || "—"}</td>
                <td>
                  {version.publishStatus === "published" ? (
                    <Link className="row-link" href={`/rate-cards/surcharges/${row.id}/versions/${version.id}`}>Xem chi tiết</Link>
                  ) : (
                    <Link className="row-link" href={`#draft-edit`}>Chỉnh sửa</Link>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {draft && rule ? (
          <>
            <h2 id="draft-edit">Sửa phiên bản nháp v{draft.versionNo}</h2>
            <SurchargeForm
              cards={options}
              chargeTypes={activeTypes}
              modes={modeOptions}
              activationServices={activationServices}
              lockCode
              surchargeId={row.id}
              versionId={draft.id}
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
                validFrom: draft.validFrom,
                validTo: draft.validTo,
                vatRate: draft.vatRate,
                economicChargeTypeId: draft.economicChargeTypeId,
                vendorPartyId: rule.vendorPartyId,
                customerPartyId: rule.customerPartyId,
                customerGroupCode: rule.customerGroupCode,
                activationPolicy: draft.activationPolicy,
                approvalRef: draft.approvalRef,
                serviceCode: draft.serviceCode ?? null,
                selectionScope: draft.selectionScope ?? "BILL",
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
