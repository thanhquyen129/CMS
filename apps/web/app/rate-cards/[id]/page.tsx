import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { PublishRateVersionButton } from "@/components/PublishRateVersionButton";
import { SetRateCardActiveButton } from "@/components/SetRateCardActiveButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { TariffMatrix } from "@/components/TariffMatrix";
import { formatDateOnlyVi, formatDateTimeVi, formatMoney } from "@/lib/money";
import {
  amountFieldLabel,
  weightBandLabel,
  calcMethodLabel,
  declarationModeLabel,
  cardStatusLabel,
  isContainerRateMethod,
  isDraftVersion,
  isPublishedVersion,
  isWeightBreakMethod,
  partyTypeLabel,
  rateTransportLabel,
  publicationLabel,
  publicationListHint,
  versionStatusLabel,
  type PricingRule,
  type RateVersion,
} from "@/lib/rate-cards";
import {
  getRateCard,
  listPricingRules,
  listRateRuleWarnings,
  listRateVersions,
  type RateRuleWarning,
} from "@/lib/rate-cards-server";

type Params = Promise<{ id: string }>;

async function loadRulesForVersions(versions: RateVersion[]) {
  const entries = await Promise.all(
    versions.map(async (v) => {
      const [rulesRes, warningsRes] = await Promise.all([listPricingRules(v.id), listRateRuleWarnings(v.id)]);
      return {
        versionId: v.id,
        rules: rulesRes.ok ? rulesRes.data : ([] as PricingRule[]),
        warnings: warningsRes.ok ? warningsRes.data : ([] as RateRuleWarning[]),
        error: rulesRes.ok ? null : rulesRes.message,
      };
    })
  );
  return new Map(entries.map((e) => [e.versionId, e]));
}

export default async function RateCardDetailPage({
  params,
}: {
  params: Params;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");

  const [cardRes, versionsRes] = await Promise.all([
    getRateCard(id),
    listRateVersions(id),
  ]);

  if (!cardRes.ok && cardRes.status === 404) {
    return (
      <AppShell terms={terms} active="rate-cards">
        <section className="panel">
          <h1>Không tìm thấy bảng giá</h1>
          <p className="lede">{cardRes.message}</p>
          <Link className="btn" href="/rate-cards">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  if (!cardRes.ok) {
    return (
      <AppShell terms={terms} active="rate-cards">
        <section className="panel">
          <h1>Bảng giá</h1>
          <div className="alert alert-error" role="alert">
            {cardRes.message}
          </div>
          <Link className="btn btn-ghost" href="/rate-cards">
            Quay lại
          </Link>
        </section>
      </AppShell>
    );
  }

  const card = cardRes.data;
  const versions = versionsRes.ok ? versionsRes.data : [];
  const rulesByVersion = await loadRulesForVersions(versions);
  const draftVersion = versions
    .filter((v) => isDraftVersion(v.status))
    .sort((a, b) => b.versionNo - a.versionNo)[0];
  const draftRules = draftVersion ? (rulesByVersion.get(draftVersion.id)?.rules ?? []) : [];
  const publishedLocked = versions.some((v) => isPublishedVersion(v.status)) && !draftVersion;

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/rate-cards">Bảng giá</Link>
          <span aria-hidden="true"> / </span>
          <span>{card.code}</span>
        </p>
        <h1>{card.name}</h1>
        <p className="lede meta-line">
          Mã: <code>{card.code}</code> · {partyTypeLabel(card.partyType)} ·{" "}
          {card.currencyCode}
          {card.transportMode ? ` · ${rateTransportLabel(card.transportMode)}` : ""}
          {` · ${declarationModeLabel(card.declarationMode)}`}
          {` · ${cardStatusLabel(card.isActive)}`}
          {` · ${publicationLabel(versions.some((v) => isPublishedVersion(v.status)))}`}
          {card.description ? ` · ${card.description}` : ""}
        </p>

        {card.routeIsActive === false ? (
          <p className="alert alert-warning" role="status">
            Tuyến đã lưu ngừng hiệu lực. Giữ nguyên trên bản này. Khi sửa nháp, chọn tuyến đang hoạt động nếu đổi tuyến.
          </p>
        ) : null}
        <div className="cta-row rate-detail-toolbar">
          {draftVersion ? (
            <Link className="btn" href={`/rate-cards/${card.id}/edit`}>
              Chỉnh sửa bảng giá
            </Link>
          ) : null}
          <Link className="btn btn-ghost" href="/bills">
            Tính giá trên {billLabel}
          </Link>
          {draftVersion && draftRules.length > 0 ? (
            <PublishRateVersionButton versionId={draftVersion.id} versionNo={draftVersion.versionNo} label="Phát hành phiên bản" />
          ) : null}
          <SetRateCardActiveButton card={card} inline />
          {publishedLocked ? (
            <span className="status-pill status-closed">Đã phát hành</span>
          ) : draftVersion ? (
            <span className="status-pill status-draft">Nháp</span>
          ) : null}
        </div>
        <p className="note" title={publicationListHint}>
          {draftVersion
            ? "Màn chi tiết chỉ để xem. Sửa bản nháp bằng Chỉnh sửa bảng giá."
            : "Phiên bản đã phát hành không sửa được. Màn này chỉ để xem."}
        </p>

        <h2 className="section-title">Phiên bản</h2>
        {!versionsRes.ok ? (
          <div className="alert alert-error" role="alert">
            {versionsRes.message}
          </div>
        ) : versions.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có phiên bản. Tạo bảng giá mới để khai báo bản nháp.
          </div>
        ) : (
          <div className="stack">
            {versions.map((v) => {
              const ruleBag = rulesByVersion.get(v.id);
              const rules = ruleBag?.rules ?? [];
              const draft = isDraftVersion(v.status);
              const published = isPublishedVersion(v.status);

              return (
                <div key={v.id} className="panel" style={{ margin: 0 }}>
                  <h3 className="section-title sm">
                    v{v.versionNo} · {versionStatusLabel(v.status)}
                  </h3>
                  <div className="table-wrap">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>Hiệu lực từ</th>
                          <th>Hiệu lực đến</th>
                          <th>Phát hành lúc</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          <td>{formatDateOnlyVi(v.effectiveFrom)}</td>
                          <td>{formatDateOnlyVi(v.effectiveTo)}</td>
                          <td>{v.publishedAt ? formatDateTimeVi(v.publishedAt) : "—"}</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                  <p className="muted small">
                    Quy tắc ở đây là cước chính. Phụ phí dùng chung tạo ở Quản lý phụ phí và không bắt buộc gắn vào bảng giá.
                  </p>
                  <p className="muted small">
                    Thuế suất VAT: {v.vatRate == null ? "chưa khai báo" : `${v.vatRate}%`}.
                    {published ? " Phiên bản đã phát hành không sửa thuế suất." : " Sửa thuế suất trên màn khai báo bảng giá."}
                  </p>
                  {v.note ? <p className="note">{v.note}</p> : null}
                  {(ruleBag?.warnings ?? []).map((w) => (
                    <div key={w.code} className="alert alert-warning" role="status">
                      {w.message} Quy tắc: {w.ruleCodes.join(", ")}.
                    </div>
                  ))}

                  {ruleBag?.error ? (
                    <div className="alert alert-error" role="alert">
                      {ruleBag.error}
                    </div>
                  ) : rules.some(
                      (r) =>
                        isWeightBreakMethod(r.calcMethod) &&
                        (r.breaks?.length ?? 0) > 0
                    ) ? (
                    <>
                      {rules.some((r) => !isWeightBreakMethod(r.calcMethod)) ? (
                        <div className="alert alert-warning" role="status">
                          Phiên bản này đang có cả quy tắc giá và ma trận. Dữ liệu không bị xóa. Cần quyết định xử lý trước khi ghi đè.
                        </div>
                      ) : null}
                      <TariffMatrix
                        rules={rules}
                        currencyCode={card.currencyCode}
                        transportMode={card.transportMode}
                      />
                    </>
                  ) : rules.length === 0 && draft ? (
                    <p className="note">Chưa có giá trên bản nháp. Dùng Chỉnh sửa bảng giá để khai báo.</p>
                  ) : rules.length === 0 ? (
                    <p className="note">Chưa có quy tắc.</p>
                  ) : (
                    <div className="table-wrap">
                      <table className="data-table">
                        <thead>
                          <tr>
                            <th scope="col">Tên quy tắc</th>
                            <th scope="col">Mã quy tắc</th>
                            <th scope="col">Cách tính</th>
                            <th scope="col" className="num">
                              Số tiền / Đơn giá
                            </th>
                            <th scope="col">Lọc</th>
                          </tr>
                        </thead>
                        <tbody>
                          {rules.map((r) => {
                            const breaks = r.breaks ?? [];
                            const containers = r.containerRates ?? [];
                            const showBreaks =
                              isWeightBreakMethod(r.calcMethod) ||
                              breaks.length > 0;
                            const showContainers =
                              isContainerRateMethod(r.calcMethod) ||
                              containers.length > 0;

                            return (
                              <tr key={r.id}>
                                <td>{r.name}</td>
                                <td>{r.code}</td>
                                <td>{calcMethodLabel(r.calcMethod)}</td>
                                <td className="num">
                                  {amountFieldLabel(r.calcMethod, r.applicability)}:{" "}
                                  {formatMoney(r.unitAmount, r.currencyCode)}
                                </td>
                                <td className="muted small">
                                  {[
                                    r.appliesAnyRoute ? "Tất cả tuyến" : r.routeCode,
                                    r.carrierName,
                                    r.weightBasis === "gross"
                                      ? "Trọng lượng thực"
                                      : r.weightBasis === "chargeable"
                                        ? "Trọng lượng tính cước"
                                        : null,
                                    r.weightMin != null || r.weightMax != null
                                      ? weightBandLabel(r.weightMin, r.weightMax)
                                      : null,
                                    r.serviceTypeCode,
                                    r.partyTypeCode ? partyTypeLabel(r.partyTypeCode) : null,
                                  ]
                                    .filter(Boolean)
                                    .join(" · ") || "—"}
                                  {(r.components ?? []).length > 0 ? (
                                    <div>
                                      {(r.components ?? []).map((c) => (
                                        <div key={c.id}>
                                          {c.code}
                                          {c.economicChargeTypeCode ? ` · ${c.economicChargeTypeCode}` : " · chưa gắn khoản mục"}
                                          {" · "}
                                          {c.financialNature === "revenue"
                                            ? "Doanh thu"
                                            : "Chi phí"}{" "}
                                          ·{" "}
                                          {formatMoney(
                                            c.amount,
                                            c.currencyCode
                                          )}
                                        </div>
                                      ))}
                                    </div>
                                  ) : null}
                                  {showBreaks ? (
                                    <div style={{ marginTop: "0.5rem" }}>
                                      <strong>Bậc trọng lượng</strong>
                                      {breaks.length === 0 ? (
                                        <div>Chưa có bậc trọng lượng.</div>
                                      ) : (
                                        <div>
                                          {breaks.map((b) => (
                                            <div key={b.id}>
                                              #{b.sequenceNo}:{" "}
                                              {b.minQuantity}
                                              {b.maxQuantity != null
                                                ? ` – ${b.maxQuantity}`
                                                : "+"}{" "}
                                              ·{" "}
                                              {formatMoney(
                                                b.unitAmount,
                                                r.currencyCode
                                              )}
                                            </div>
                                          ))}
                                        </div>
                                      )}
                                    </div>
                                  ) : null}
                                  {showContainers ? (
                                    <div style={{ marginTop: "0.5rem" }}>
                                      <strong>Đơn giá container</strong>
                                      {containers.length === 0 ? (
                                        <div>Chưa có đơn giá container.</div>
                                      ) : (
                                        <div>
                                          {containers.map((c) => (
                                            <div key={c.id}>
                                              {c.containerType} ·{" "}
                                              {formatMoney(
                                                c.unitAmount,
                                                r.currencyCode
                                              )}
                                            </div>
                                          ))}
                                        </div>
                                      )}
                                    </div>
                                  ) : null}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {published ? (
                    <p className="note">
                      Phiên bản đã phát hành — bất biến. Dùng trên {billLabel} để
                      tính giá.
                    </p>
                  ) : null}
                </div>
              );
            })}
          </div>
        )}
      </section>
    </AppShell>
  );
}
