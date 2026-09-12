import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { BillCostRevenuePanel } from "@/components/BillCostRevenuePanel";
import { BillDocumentsApArPanel } from "@/components/BillDocumentsApArPanel";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { term, type TerminologyMap } from "@/lib/terminology";
import {
  getBill,
  getFinancialProfile,
  getProfitability,
  type CurrencyFinancialBucket,
  type MaturityBreakdown,
} from "@/lib/bills";
import { listCostsByBill, listRevenuesByBill } from "@/lib/costs-revenues-server";
import { listAccountsPayable, listAccountsReceivable } from "@/lib/ap-ar";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

function MaturityRow({
  label,
  maturity,
  currency,
}: {
  label: string;
  maturity: MaturityBreakdown;
  currency: string;
}) {
  return (
    <tr>
      <th scope="row">{label}</th>
      <td className="num">{formatMoney(maturity.expectedTotal, currency)}</td>
      <td className="num">{formatMoney(maturity.confirmedTotal, currency)}</td>
      <td className="num">{formatMoney(maturity.actualTotal, currency)}</td>
    </tr>
  );
}

function CurrencyProfileCard({
  bucket,
  terms,
}: {
  bucket: CurrencyFinancialBucket;
  terms: TerminologyMap;
}) {
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");
  const revenue = term(terms, "REVENUE", "Doanh thu");
  const directCost = term(terms, "DIRECT_COST", "Chi phí trực tiếp");
  const cost = term(terms, "COST", "Chi phí");
  const profit = term(terms, "PROFIT", "Lợi nhuận");
  const best = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");
  const allocated = term(terms, "COST_ALLOCATION", "Phân bổ chi phí");
  const variance = term(terms, "VARIANCE_EXPECTED_VS_ACTUAL", "Chênh lệch Dự kiến vs Thực tế");

  return (
    <div className="currency-card">
      <h3 className="currency-title">{bucket.currencyCode}</h3>

      <div className="table-wrap">
        <table className="data-table maturity-table">
          <caption className="sr-only">
            Lớp trưởng thành {revenue} / {directCost} theo {bucket.currencyCode}
          </caption>
          <thead>
            <tr>
              <th scope="col">Chỉ tiêu</th>
              <th scope="col" className="num">
                {expected}
              </th>
              <th scope="col" className="num">
                {confirmed}
              </th>
              <th scope="col" className="num">
                {actual}
              </th>
            </tr>
          </thead>
          <tbody>
            <MaturityRow
              label={`${revenue} (${bucket.revenueLineCount} dòng)`}
              maturity={bucket.revenueMaturity}
              currency={bucket.currencyCode}
            />
            <MaturityRow
              label={`${directCost} (${bucket.directCostLineCount} dòng)`}
              maturity={bucket.directCostMaturity}
              currency={bucket.currencyCode}
            />
          </tbody>
        </table>
      </div>

      <dl className="metric-grid">
        <div>
          <dt>
            {revenue} — {best}
          </dt>
          <dd>{formatMoney(bucket.revenueBestAvailable, bucket.currencyCode)}</dd>
        </div>
        <div>
          <dt>
            {directCost} — {best}
          </dt>
          <dd>{formatMoney(bucket.directCostBestAvailable, bucket.currencyCode)}</dd>
        </div>
        <div>
          <dt>
            {allocated} ({bucket.allocatedCostLineCount} dòng)
          </dt>
          <dd>{formatMoney(bucket.allocatedCostAmount, bucket.currencyCode)}</dd>
        </div>
        <div>
          <dt>
            {cost} — {best}
          </dt>
          <dd>{formatMoney(bucket.costBestAvailable, bucket.currencyCode)}</dd>
        </div>
        <div>
          <dt>
            {profit} — {best}
          </dt>
          <dd className={bucket.profitBestAvailable < 0 ? "neg" : undefined}>
            {formatMoney(bucket.profitBestAvailable, bucket.currencyCode)}
          </dd>
        </div>
        <div>
          <dt>{variance} ({profit})</dt>
          <dd>{formatMoney(bucket.profitVarianceExpectedVsActual, bucket.currencyCode)}</dd>
        </div>
      </dl>
    </div>
  );
}

export default async function BillDetailPage({ params }: { params: Params }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const profileLabel = term(terms, "BILL_FINANCIAL_PROFILE", "Hồ sơ tài chính Bill");
  const profitLabel = term(terms, "BILL_PROFITABILITY", "Lợi nhuận theo Bill");
  const settlementLabel = term(terms, "SETTLEMENT_OUTSTANDING", "Số dư tất toán còn lại");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const best = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");

  const [billRes, profileRes, profitRes, costsRes, revenuesRes, apRes, arRes] =
    await Promise.all([
      getBill(id),
      getFinancialProfile(id),
      getProfitability(id, "best"),
      listCostsByBill(id),
      listRevenuesByBill(id),
      listAccountsPayable(),
      listAccountsReceivable(),
    ]);

  if (!billRes.ok && billRes.status === 404) {
    return (
      <AppShell terms={terms} active="bills">
        <section className="panel">
          <h1>Không tìm thấy {billLabel}</h1>
          <p className="lede">{billRes.message}</p>
          <Link className="btn" href="/bills">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  if (!billRes.ok) {
    return (
      <AppShell terms={terms} active="bills">
        <section className="panel">
          <h1>{billLabel}</h1>
          <div className="alert alert-error" role="alert">
            {billRes.message}
          </div>
          <Link className="btn btn-ghost" href="/bills">
            Quay lại danh sách
          </Link>
        </section>
      </AppShell>
    );
  }

  const bill = billRes.data;

  return (
    <AppShell
      terms={terms}
      active="bills"
      topbarRight={
        <Link className="btn btn-ghost btn-sm" href="/bills">
          ← Danh sách
        </Link>
      }
    >
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/bills">{billLabel}</Link>
          <span aria-hidden="true"> / </span>
          <span>{bill.billNo}</span>
        </p>
        <h1>
          {billLabel} {bill.billNo}
        </h1>
        <p className="lede meta-line">
          Loại: {bill.billType} · Trạng thái: {bill.operationalStatus}
          {bill.sourceSystem ? ` · Nguồn: ${bill.sourceSystem}` : ""}
          {bill.externalId ? ` · Mã ngoài: ${bill.externalId}` : ""}
        </p>

        <h2 className="section-title">{profileLabel}</h2>
        {!profileRes.ok ? (
          <div className="alert alert-error" role="alert">
            {profileRes.message}
          </div>
        ) : (
          <>
            <p className="muted small">
              Cập nhật: {formatDateTimeVi(profileRes.data.asOfTimestamp)}
              {profileRes.data.hasMixedCurrencies
                ? " · Nhiều loại tiền — không cộng gộp chéo."
                : ""}
            </p>
            {profileRes.data.note ? (
              <p className="note">{profileRes.data.note}</p>
            ) : null}
            {profileRes.data.asOfLimitationNote ? (
              <div className="alert alert-info">{profileRes.data.asOfLimitationNote}</div>
            ) : null}

            {profileRes.data.byCurrency.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có dòng {term(terms, "COST", "chi phí")} /{" "}
                {term(terms, "REVENUE", "doanh thu")} trên {billLabel} này.
              </div>
            ) : (
              <div className="stack">
                {profileRes.data.byCurrency.map((b) => (
                  <CurrencyProfileCard key={b.currencyCode} bucket={b} terms={terms} />
                ))}
              </div>
            )}

            {profileRes.data.settlementOutstanding.length > 0 ? (
              <div className="settlement-block">
                <h3 className="section-title sm">{settlementLabel}</h3>
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th scope="col">Tiền tệ</th>
                        <th scope="col" className="num">
                          {apLabel}
                        </th>
                        <th scope="col" className="num">
                          {arLabel}
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {profileRes.data.settlementOutstanding.map((s) => (
                        <tr key={s.currencyCode}>
                          <td>{s.currencyCode}</td>
                          <td className="num">
                            {formatMoney(s.accountsPayableOutstanding, s.currencyCode)}
                          </td>
                          <td className="num">
                            {formatMoney(s.accountsReceivableOutstanding, s.currencyCode)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : null}
          </>
        )}

        <BillCostRevenuePanel
          terms={terms}
          billId={id}
          costs={costsRes.ok ? costsRes.data : null}
          costsError={costsRes.ok ? null : costsRes.message}
          revenues={revenuesRes.ok ? revenuesRes.data : null}
          revenuesError={revenuesRes.ok ? null : revenuesRes.message}
        />

        <BillDocumentsApArPanel
          terms={terms}
          billId={id}
          payables={apRes.ok ? apRes.data : null}
          payablesError={apRes.ok ? null : apRes.message}
          receivables={arRes.ok ? arRes.data : null}
          receivablesError={arRes.ok ? null : arRes.message}
        />

        <h2 className="section-title">{profitLabel}</h2>
        {!profitRes.ok ? (
          <div className="alert alert-error" role="alert">
            {profitRes.message}
          </div>
        ) : profitRes.data.byCurrency.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có số liệu {profitLabel.toLowerCase()} để hiển thị.
          </div>
        ) : (
          <>
            <p className="muted small">
              {term(terms, "PROFITABILITY_VIEW", "Góc nhìn lợi nhuận")}: {best} (
              {profitRes.data.view}) · {formatDateTimeVi(profitRes.data.asOfTimestamp)}
            </p>
            {profitRes.data.note ? <p className="note">{profitRes.data.note}</p> : null}
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Tiền tệ</th>
                    <th scope="col" className="num">
                      {term(terms, "REVENUE", "Doanh thu")}
                    </th>
                    <th scope="col" className="num">
                      {term(terms, "DIRECT_COST", "Chi phí trực tiếp")}
                    </th>
                    <th scope="col" className="num">
                      {term(terms, "COST_ALLOCATION", "Phân bổ")}
                    </th>
                    <th scope="col" className="num">
                      {term(terms, "COST", "Chi phí")}
                    </th>
                    <th scope="col" className="num">
                      {term(terms, "PROFIT", "Lợi nhuận")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {profitRes.data.byCurrency.map((p) => (
                    <tr key={p.currencyCode}>
                      <td>{p.currencyCode}</td>
                      <td className="num">{formatMoney(p.revenueAmount, p.currencyCode)}</td>
                      <td className="num">{formatMoney(p.directCostAmount, p.currencyCode)}</td>
                      <td className="num">
                        {formatMoney(p.allocatedCostAmount, p.currencyCode)}
                      </td>
                      <td className="num">{formatMoney(p.costAmount, p.currencyCode)}</td>
                      <td className={`num ${p.profitAmount < 0 ? "neg" : ""}`}>
                        {formatMoney(p.profitAmount, p.currencyCode)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </section>
    </AppShell>
  );
}
