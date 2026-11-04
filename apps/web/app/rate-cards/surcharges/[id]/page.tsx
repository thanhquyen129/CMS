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
                <td>
                  <span>{version.activationPolicy || "—"}</span>
                  {version.serviceCode ? (
                    <span className="muted" style={{ display: "block", fontSize: "0.85em" }}>
                      ({version.serviceCode} · {version.selectionScope || "BILL"})
                    </span>
                  ) : null}
                </td>
                <td>{formatDateOnlyVi(version.validFrom)}</td>
                <td>{formatDateOnlyVi(version.validTo)}</td>
                <td>{version.publishedAt ? formatDateTimeVi(version.publishedAt) : "—"}</td>
                <td>{version.rules.map((item) => (item.calculationMode === "fixed_rate" ? "fixed" : item.calculationMode)).join(", ") || "—"}</td>
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
        ) : latestPublished ? (
          <div className="card" style={{ marginTop: "1.5rem", padding: "1.25rem", border: "1px solid var(--border, #e2e8f0)", borderRadius: "8px", background: "var(--bg-subtle, #f8fafc)" }}>
            <h2 style={{ fontSize: "1.2rem", marginTop: 0, marginBottom: "1rem", color: "#0f172a" }}>
              Chi tiết phiên bản đã phát hành: v{latestPublished.versionNo} (Chỉ đọc)
            </h2>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
              <div>
                <span className="muted" style={{ display: "block", fontSize: "0.85rem" }}>Trạng thái phát hành</span>
                <strong>Đã phát hành (Read-only)</strong>
              </div>
              <div>
                <span className="muted" style={{ display: "block", fontSize: "0.85rem" }}>Chính sách kích hoạt</span>
                <strong>{latestPublished.activationPolicy || "—"}</strong>
              </div>
              <div>
                <span className="muted" style={{ display: "block", fontSize: "0.85rem" }}>Dịch vụ kích hoạt</span>
                <strong>{latestPublished.serviceCode ? `${latestPublished.serviceCode} — Đóng gói hàng hóa` : "Không yêu cầu"}</strong>
              </div>
              <div>
                <span className="muted" style={{ display: "block", fontSize: "0.85rem" }}>Phạm vi lựa chọn</span>
                <strong>{latestPublished.selectionScope || "BILL"}</strong>
              </div>
              <div>
                <span className="muted" style={{ display: "block", fontSize: "0.85rem" }}>Khoản mục kinh tế</span>
                <strong>{latestPublished.economicChargeTypeCode ? `${latestPublished.economicChargeTypeCode} — ${latestPublished.economicChargeTypeName}` : "Chưa gắn"}</strong>
              </div>
              <div>
                <span className="muted" style={{ display: "block", fontSize: "0.85rem" }}>Đơn giá / Tỷ lệ</span>
                <strong>{latestPublished.rules.map((item) => `${item.rateAmountPercent} ${item.currencyCode} (${item.calculationMode === "fixed_rate" ? "fixed" : item.calculationMode})`).join(", ") || "—"}</strong>
              </div>
            </div>
            <p className="muted" style={{ marginTop: "1rem", marginBottom: 0, fontStyle: "italic" }}>
              Phiên bản đã phát hành là bất biến (Read-only). Bấm &quot;Tạo phiên bản mới&quot; để thay đổi đơn giá hoặc điều kiện áp dụng.
            </p>
          </div>
        ) : (
          <p className="muted">Phiên bản đã phát hành giữ nguyên. Bấm tạo phiên bản mới để đổi đơn giá hoặc điều kiện.</p>
        )}
      </section>
    </AppShell>
  );
}
