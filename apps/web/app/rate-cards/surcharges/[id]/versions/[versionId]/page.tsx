import { cookies } from "next/headers";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateOnlyVi, formatDateTimeVi } from "@/lib/money";
import { getSurchargeVersion } from "@/lib/rate-cards-server";

type Params = Promise<{ id: string; versionId: string }>;

function policyLabel(policy?: string) {
  switch ((policy ?? "").toUpperCase()) {
    case "MANDATORY_BY_POLICY":
      return "Bắt buộc theo chính sách";
    case "AUTO_CONDITIONAL":
      return "Tự động theo điều kiện dữ liệu";
    case "OPTIONAL_OPT_IN":
      return "Tùy chọn có xác nhận";
    case "UNCLASSIFIED_LEGACY":
      return "Chưa phân loại (legacy)";
    default:
      return policy || "—";
  }
}

export default async function SurchargeVersionDetailPage({ params }: { params: Params }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const { id, versionId } = await params;
  const terms = await fetchTerminology();
  const res = await getSurchargeVersion(id, versionId);
  if (!res.ok) {
    if (res.status === 404) notFound();
    return (
      <AppShell terms={terms} active="rate-cards">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">{res.message}</div>
        </section>
      </AppShell>
    );
  }

  const row = res.data;
  const version = row.version;
  const rule = version.rules[0];

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href={`/rate-cards/surcharges/${id}`}>← Quay lại phụ phí</Link>
        </p>
        <h1>{row.name} — phiên bản v{version.versionNo}</h1>
        <p className="muted">
          <code>{row.code}</code> · {row.direction === "sell" ? "Bán" : row.direction === "both" ? "Cả hai" : "Mua"}
          {version.publishStatus === "published" ? " · Chỉ đọc (đã phát hành)" : " · Bản nháp"}
        </p>
        {version.activationPolicy === "UNCLASSIFIED_LEGACY" ? (
          <div className="alert alert-warning" role="status">
            Chưa được phân loại chính sách áp dụng – Hệ thống không tự động cộng vào tính giá mới.
          </div>
        ) : null}
        <table className="data-table">
          <tbody>
            <tr><th>Trạng thái</th><td>{version.publishStatus === "published" ? "Đã phát hành" : "Nháp"}</td></tr>
            <tr><th>Hiệu lực từ</th><td>{formatDateOnlyVi(version.validFrom)}</td></tr>
            <tr><th>Hiệu lực đến</th><td>{formatDateOnlyVi(version.validTo)}</td></tr>
            <tr><th>Phát hành lúc</th><td>{version.publishedAt ? formatDateTimeVi(version.publishedAt) : "—"}</td></tr>
            <tr><th>Khoản mục kinh tế</th><td>{version.economicChargeTypeCode ? `${version.economicChargeTypeCode} — ${version.economicChargeTypeName}` : "Chưa gắn"}</td></tr>
            <tr><th>Chính sách kích hoạt</th><td>{policyLabel(version.activationPolicy)}</td></tr>
            {version.activationPolicy === "OPTIONAL_OPT_IN" || version.serviceCode ? (
              <>
                <tr>
                  <th>Dịch vụ kích hoạt</th>
                  <td>
                    {version.serviceCode ? (
                      <span><strong>{version.serviceCode}</strong>{version.serviceDisplayName ? ` — ${version.serviceDisplayName}` : ""}</span>
                    ) : (
                      <span className="muted">Chưa gắn</span>
                    )}
                  </td>
                </tr>
                <tr>
                  <th>Phạm vi lựa chọn</th>
                  <td>{version.selectionScope || "BILL"}</td>
                </tr>
              </>
            ) : null}
            <tr><th>Căn cứ phê duyệt</th><td>{version.approvalRef || "—"}</td></tr>
            <tr><th>Cấu hình kích hoạt</th><td><code>{version.activationConfigJson || "—"}</code></td></tr>
            <tr><th>Cách tính</th><td>{rule?.calculationMode || "—"}</td></tr>
            <tr><th>Cơ sở tính</th><td>{rule?.basis || "—"}</td></tr>
            <tr><th>Giá trị</th><td>{rule ? `${rule.rateAmountPercent} ${rule.currencyCode}` : "—"}</td></tr>
            <tr><th>Phương thức</th><td>{rule?.transportMode || "Không giới hạn"}</td></tr>
            <tr><th>Tuyến</th><td>{rule?.routeCode || "Không giới hạn"}</td></tr>
            <tr><th>VAT</th><td>{version.vatRate == null ? "Chưa khai báo" : `${version.vatRate}%`}</td></tr>
          </tbody>
        </table>
        {!row.readOnly ? (
          <p className="muted">
            <Link className="row-link" href={`/rate-cards/surcharges/${id}`}>Chỉnh sửa bản nháp trên trang phụ phí</Link>
          </p>
        ) : null}
      </section>
    </AppShell>
  );
}
