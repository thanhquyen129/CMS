import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdjustCostRevenueButton } from "@/components/AdjustCostRevenueButton";
import {
  AdjustmentHistoryTable,
  LineDetailBackLink,
} from "@/components/AdjustmentHistoryTable";
import { AuditTrailPanel } from "@/components/AuditTrailPanel";
import { MaturityTransitionButton } from "@/components/MaturityTransitionButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getCost } from "@/lib/costs-revenues-server";
import {
  canActualize,
  canConfirm,
  isSharedCost,
  maturityLabelKey,
} from "@/lib/costs-revenues";
import { formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function CostDetailPage({ params }: { params: Params }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");
  const adjLabel = term(terms, "ADJUSTMENT", "Điều chỉnh");

  const costRes = await getCost(id);
  if (!costRes.ok && costRes.status === 404) {
    return (
      <AppShell terms={terms} active="costs">
        <section className="panel">
          <h1>Không tìm thấy {costLabel}</h1>
          <p className="lede">{costRes.message}</p>
          <Link className="btn" href="/costs">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  if (!costRes.ok) {
    return (
      <AppShell terms={terms} active="costs">
        <section className="panel">
          <LineDetailBackLink href="/bills" label={`Danh sách ${billLabel}`} />
          <div className="alert alert-error" role="alert">
            {costRes.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const cost = costRes.data;
  if (isSharedCost(cost.attributionType)) {
    redirect(`/costs/shared/${cost.id}`);
  }

  const maturityKey = maturityLabelKey(cost.financialMaturity);
  const maturityVi =
    maturityKey === "EXPECTED"
      ? expected
      : maturityKey === "CONFIRMED"
        ? confirmed
        : maturityKey === "ACTUAL"
          ? actual
          : cost.financialMaturity;

  const backHref = cost.billId ? `/bills/${cost.billId}` : "/bills";
  const adjustments = cost.adjustments ?? [];

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel panel-wide">
        <LineDetailBackLink
          href={backHref}
          label={cost.billId ? `Hồ sơ ${billLabel}` : `Danh sách ${billLabel}`}
        />
        <h1>
          {cost.costTypeCode || `${costLabel} ${cost.id.slice(0, 8)}`}
        </h1>
        <p className="lede">
          Trực tiếp · {maturityVi} ·{" "}
          <strong>{formatMoney(cost.amount, cost.currencyCode)}</strong>
          {cost.recordStatus !== "active" ? ` · ${cost.recordStatus}` : ""}
        </p>

        <dl className="metric-grid">
          <div>
            <dt>{expected}</dt>
            <dd>{formatMoney(cost.expectedAmount, cost.currencyCode)}</dd>
          </div>
          <div>
            <dt>{confirmed}</dt>
            <dd>
              {cost.confirmedAmount != null
                ? formatMoney(cost.confirmedAmount, cost.currencyCode)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>{actual}</dt>
            <dd>
              {cost.actualAmount != null
                ? formatMoney(cost.actualAmount, cost.currencyCode)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Ngày hiệu lực</dt>
            <dd>{cost.effectiveDate}</dd>
          </div>
        </dl>

        {cost.recordStatus === "active" ? (
          <p className="cta-row">
            {canConfirm(cost.financialMaturity, cost.recordStatus) ? (
              <MaturityTransitionButton
                terms={terms}
                kind="cost"
                action="confirm"
                lineId={cost.id}
                currentAmount={cost.amount}
                currencyCode={cost.currencyCode}
                rowVersion={cost.rowVersion}
              />
            ) : null}
            {canActualize(cost.financialMaturity, cost.recordStatus) ? (
              <MaturityTransitionButton
                terms={terms}
                kind="cost"
                action="actualize"
                lineId={cost.id}
                currentAmount={cost.amount}
                currencyCode={cost.currencyCode}
                rowVersion={cost.rowVersion}
              />
            ) : null}
            <AdjustCostRevenueButton
              terms={terms}
              kind="cost"
              lineId={cost.id}
              currentAmount={cost.amount}
              currencyCode={cost.currencyCode}
              financialMaturity={cost.financialMaturity}
              rowVersion={cost.rowVersion}
              buttonClassName="btn btn-sm"
            />
            {cost.billId ? (
              <Link className="btn btn-ghost btn-sm" href={backHref}>
                Về {billLabel}
              </Link>
            ) : null}
          </p>
        ) : null}

        <h2>{adjLabel} — lịch sử</h2>
        <p className="note">
          Mỗi lần đổi số lớp hiện tại tạo một dòng lịch sử (C-009). Không silent
          overwrite.
        </p>
        <AdjustmentHistoryTable
          terms={terms}
          adjustments={adjustments}
          currencyCode={cost.currencyCode}
        />

        <section className="panel" style={{ marginTop: "24px" }} aria-labelledby="cost-trace-heading">
          <h2 id="cost-trace-heading" className="section-title">
            Nguồn tính giá &amp; Truy vết (Rating Traceability)
          </h2>
          {cost.ratingTrace && cost.ratingTrace.sourceOrigin !== "manual" ? (
            <div className="stack">
              <dl className="kv-list">
                <div>
                  <dt>Nguồn phát sinh</dt>
                  <dd>
                    <span className="badge badge-success">
                      {cost.ratingTrace.isSurcharge ? "Phụ phí từ Rating" : "Cước chính từ Rating"}
                    </span>
                  </dd>
                </div>
                {cost.ratingTrace.ratingId ? (
                  <div>
                    <dt>Lần tính giá (Rating)</dt>
                    <dd>
                      {cost.billId ? (
                        <Link className="row-link" href={`/bills/${cost.billId}?tab=rating#bill-rating`}>
                          Rating #{cost.ratingTrace.ratingId.slice(0, 8)}
                        </Link>
                      ) : (
                        <span>#{cost.ratingTrace.ratingId.slice(0, 8)}</span>
                      )}
                    </dd>
                  </div>
                ) : null}
                {cost.ratingTrace.isSurcharge ? (
                  <>
                    <div>
                      <dt>Biểu phụ phí (Surcharge)</dt>
                      <dd>
                        {cost.ratingTrace.surchargeId ? (
                          <Link className="row-link" href={`/rate-cards/surcharges/${cost.ratingTrace.surchargeId}`}>
                            {cost.ratingTrace.surchargeCode} — {cost.ratingTrace.surchargeName ?? "Phụ phí độc lập"}
                          </Link>
                        ) : (
                          <span>{cost.ratingTrace.surchargeCode ?? "Phụ phí"}</span>
                        )}
                      </dd>
                    </div>
                    {cost.ratingTrace.surchargeVersionId ? (
                      <div>
                        <dt>Phiên bản phụ phí</dt>
                        <dd><code>#{cost.ratingTrace.surchargeVersionId.slice(0, 8)}</code> (Snapshot độc lập)</dd>
                      </div>
                    ) : null}
                  </>
                ) : (
                  <>
                    <div>
                      <dt>Bảng giá &amp; Phiên bản</dt>
                      <dd>
                        {cost.ratingTrace.rateCardId ? (
                          <Link className="row-link" href={`/rate-cards/${cost.ratingTrace.rateCardId}`}>
                            {cost.ratingTrace.rateCardCode} — {cost.ratingTrace.rateCardName} (v{cost.ratingTrace.rateVersionNo ?? 1})
                          </Link>
                        ) : (
                          <span>v{cost.ratingTrace.rateVersionNo}</span>
                        )}
                      </dd>
                    </div>
                    <div>
                      <dt>Quy tắc tính giá</dt>
                      <dd>
                        <code>{cost.ratingTrace.ruleCode ?? "QT0001"}</code>
                        {cost.ratingTrace.pricingRuleId ? (
                          <span className="muted small" style={{ marginLeft: "8px" }}>
                            (ID: #{cost.ratingTrace.pricingRuleId.slice(0, 8)})
                          </span>
                        ) : null}
                      </dd>
                    </div>
                  </>
                )}
                <div>
                  <dt>Khoản mục (Charge Code)</dt>
                  <dd>
                    <strong>{cost.ratingTrace.chargeCode ?? cost.costTypeCode}</strong>
                  </dd>
                </div>
                <div>
                  <dt>Trọng lượng tính cước (CW)</dt>
                  <dd>
                    {cost.ratingTrace.chargeableWeightKg != null ? `${cost.ratingTrace.chargeableWeightKg} kg` : "—"}
                  </dd>
                </div>
                <div>
                  <dt>Đơn giá / Nấc khớp</dt>
                  <dd>
                    {cost.ratingTrace.unitRate != null
                      ? `${formatMoney(cost.ratingTrace.unitRate, cost.currencyCode)} / kg`
                      : cost.ratingTrace.formulaText ?? "Theo bảng giá"}
                  </dd>
                </div>
                <div>
                  <dt>Công thức snapshot</dt>
                  <dd>
                    <code>{cost.ratingTrace.formulaText ?? "—"}</code>
                  </dd>
                </div>
                <div>
                  <dt>Ngày áp dụng giá (Rate Date)</dt>
                  <dd>
                    {cost.ratingTrace.rateDateFormatted ?? "—"} ({cost.ratingTrace.rateDateSourceLabel ?? "Theo Bill"})
                  </dd>
                </div>
                {cost.ratingTrace.vendorPartyId ? (
                  <div>
                    <dt>Nhà cung cấp (Vendor)</dt>
                    <dd>
                      <Link className="row-link" href="/admin/parties">
                        {cost.ratingTrace.vendorName || `#${cost.ratingTrace.vendorPartyId.slice(0, 8)}`}
                      </Link>
                    </dd>
                  </div>
                ) : null}
              </dl>

              <h3 className="section-title sm" style={{ marginTop: "16px" }}>Bóc tách Thuế &amp; Tiền (VAT Disclosure)</h3>
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Tiền trước thuế (Net / Pre-tax)</th>
                      <th scope="col">Thuế suất VAT</th>
                      <th scope="col" className="num">Tiền thuế VAT</th>
                      <th scope="col" className="num">Tổng sau thuế (Gross)</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td><strong>{formatMoney(cost.ratingTrace.netAmount ?? cost.expectedAmount, cost.currencyCode)}</strong></td>
                      <td>{cost.ratingTrace.vatRate != null ? `${cost.ratingTrace.vatRate}%` : "Chưa khai báo"}</td>
                      <td className="num">{formatMoney(cost.ratingTrace.vatAmount ?? 0, cost.currencyCode)}</td>
                      <td className="num"><strong>{formatMoney(cost.ratingTrace.grossAmount ?? cost.expectedAmount, cost.currencyCode)}</strong></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            <div className="alert alert-info" role="status">
              <strong>Nguồn: Nhập thủ công</strong> — Khoản chi phí này được tạo thủ công hoặc chưa có snapshot liên kết từ Rating Engine.
            </div>
          )}
        </section>

        <AuditTrailPanel
          terms={terms}
          objectType="cost"
          objectId={cost.id}
        />
      </section>
    </AppShell>
  );
}
