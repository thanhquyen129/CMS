import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AddPricingRuleForm } from "@/components/AddPricingRuleForm";
import { AddPricingRuleComponentForm } from "@/components/AddPricingRuleComponentForm";
import { PricingRuleComponentActions } from "@/components/PricingRuleComponentActions";
import { CreateRateVersionForm } from "@/components/CreateRateVersionForm";
import { PublishRateVersionButton } from "@/components/PublishRateVersionButton";
import { RetireRateCardButton } from "@/components/RetireRateCardButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { ComposeTariffForm } from "@/components/ComposeTariffForm";
import { TariffMatrix } from "@/components/TariffMatrix";
import { formatDateTimeVi, formatDateVi, formatMoney } from "@/lib/money";
import {
  calcMethodLabel,
  isContainerRateMethod,
  isDraftVersion,
  isPublishedVersion,
  isWeightBreakMethod,
  partyTypeLabel,
  versionStatusLabel,
  type PricingRule,
  type RateVersion,
} from "@/lib/rate-cards";
import {
  getRateCard,
  listPricingRules,
  listRateVersions,
} from "@/lib/rate-cards-server";

type Params = Promise<{ id: string }>;

async function loadRulesForVersions(versions: RateVersion[]) {
  const entries = await Promise.all(
    versions.map(async (v) => {
      const rulesRes = await listPricingRules(v.id);
      return {
        versionId: v.id,
        rules: rulesRes.ok ? rulesRes.data : ([] as PricingRule[]),
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
          {card.carrierName ? ` · ${card.carrierName}` : ""}
          {card.transportMode ? ` · ${card.transportMode}` : ""}
          {card.routeCode ? ` · ${card.routeCode}` : ""}
          {card.isActive ? "" : " · Ngưng dùng"}
          {card.description ? ` · ${card.description}` : ""}
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn btn-ghost" href="/bills">
            Tính giá trên {billLabel}
          </Link>
        </p>
        {versionsRes.ok && !versions.some((v) => isPublishedVersion(v.status)) ? (
          <RetireRateCardButton rateCardId={card.id} code={card.code} />
        ) : null}
        {versions.some((v) => isPublishedVersion(v.status)) ? (
          <p className="note">
            Đã có phiên bản phát hành — không ngừng bảng giá. Lập phiên bản mới.
          </p>
        ) : null}

        <h2 className="section-title">Phiên bản</h2>
        {!versionsRes.ok ? (
          <div className="alert alert-error" role="alert">
            {versionsRes.message}
          </div>
        ) : versions.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có phiên bản. Tạo nháp bên dưới.
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
                    v{v.versionNo} — {versionStatusLabel(v.status)}
                  </h3>
                  <p className="muted small">
                    {v.effectiveFrom
                      ? `Hiệu lực: ${formatDateVi(v.effectiveFrom)}`
                      : "Chưa ghi ngày hiệu lực"}
                    {v.publishedAt
                      ? ` · Phát hành: ${formatDateTimeVi(v.publishedAt)}`
                      : " · Chưa phát hành"}
                  </p>
                  {v.note ? <p className="note">{v.note}</p> : null}

                  {ruleBag?.error ? (
                    <div className="alert alert-error" role="alert">
                      {ruleBag.error}
                    </div>
                  ) : rules.some(
                      (r) =>
                        isWeightBreakMethod(r.calcMethod) &&
                        (r.breaks?.length ?? 0) > 0
                    ) ? (
                    <TariffMatrix
                      rules={rules}
                      currencyCode={card.currencyCode}
                      transportMode={card.transportMode}
                    />
                  ) : rules.length === 0 && draft ? (
                    <ComposeTariffForm
                      versionId={v.id}
                      currencyCode={card.currencyCode}
                      transportMode={card.transportMode}
                    />
                  ) : rules.length === 0 ? (
                    <p className="note">Chưa có quy tắc.</p>
                  ) : (
                    <div className="table-wrap">
                      <table className="data-table">
                        <thead>
                          <tr>
                            <th scope="col">Mã</th>
                            <th scope="col">Tên</th>
                            <th scope="col">Cách tính</th>
                            <th scope="col" className="num">
                              Đơn giá
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
                                <td>
                                  <code>{r.code}</code>
                                </td>
                                <td>{r.name}</td>
                                <td>{calcMethodLabel(r.calcMethod)}</td>
                                <td className="num">
                                  {formatMoney(r.unitAmount, r.currencyCode)}
                                </td>
                                <td className="muted small">
                                  {[
                                    r.serviceTypeCode,
                                    r.partyTypeCode,
                                    r.routeCode,
                                  ]
                                    .filter(Boolean)
                                    .join(" · ") || "—"}
                                  {(r.components ?? []).length > 0 ? (
                                    <div>
                                      {(r.components ?? []).map((c) => (
                                        <div key={c.id}>
                                          {c.code} ·{" "}
                                          {c.financialNature === "revenue"
                                            ? "Doanh thu"
                                            : "Chi phí"}{" "}
                                          ·{" "}
                                          {formatMoney(
                                            c.amount,
                                            c.currencyCode
                                          )}
                                          {draft ? (
                                            <PricingRuleComponentActions
                                              componentId={c.id}
                                              name={c.name}
                                              amount={c.amount}
                                              currencyCode={c.currencyCode}
                                              financialNature={c.financialNature}
                                              costTypeCode={c.costTypeCode}
                                              revenueTypeCode={c.revenueTypeCode}
                                            />
                                          ) : null}
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

                  {draft && rules.length > 0 ? (
                    <>
                      <AddPricingRuleForm
                        versionId={v.id}
                        defaultCurrency={card.currencyCode}
                      />
                      {rules.map((r) => (
                        <AddPricingRuleComponentForm
                          key={r.id}
                          ruleId={r.id}
                          defaultCurrency={r.currencyCode || card.currencyCode}
                        />
                      ))}
                      {rules.length > 0 ? (
                        <PublishRateVersionButton
                          versionId={v.id}
                          versionNo={v.versionNo}
                        />
                      ) : null}
                    </>
                  ) : null}

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

        <CreateRateVersionForm rateCardId={card.id} />
      </section>
    </AppShell>
  );
}
