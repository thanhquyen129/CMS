import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AddPricingRuleForm } from "@/components/AddPricingRuleForm";
import { CreateRateVersionForm } from "@/components/CreateRateVersionForm";
import { PublishRateVersionButton } from "@/components/PublishRateVersionButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import {
  calcMethodLabel,
  isDraftVersion,
  isPublishedVersion,
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
          {card.isActive ? "" : " · Ngưng dùng"}
          {card.description ? ` · ${card.description}` : ""}
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn btn-ghost" href="/bills">
            Tính giá trên {billLabel}
          </Link>
        </p>

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
                    {v.publishedAt
                      ? `Phát hành: ${formatDateTimeVi(v.publishedAt)}`
                      : "Chưa phát hành"}
                    {v.note ? ` · ${v.note}` : ""}
                  </p>

                  {ruleBag?.error ? (
                    <div className="alert alert-error" role="alert">
                      {ruleBag.error}
                    </div>
                  ) : rules.length === 0 ? (
                    <p className="note">
                      Chưa có quy tắc.{" "}
                      {draft
                        ? "Thêm ít nhất một quy tắc trước khi phát hành."
                        : null}
                    </p>
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
                          {rules.map((r) => (
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
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {draft ? (
                    <>
                      <AddPricingRuleForm
                        versionId={v.id}
                        defaultCurrency={card.currencyCode}
                      />
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
