import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AuditTrailPanel } from "@/components/AuditTrailPanel";
import { BillCostRevenuePanel } from "@/components/BillCostRevenuePanel";
import { BillDocumentsApArPanel } from "@/components/BillDocumentsApArPanel";
import { BillRatingPanel } from "@/components/BillRatingPanel";
import { FieldOwnershipPanel } from "@/components/FieldOwnershipPanel";
import { OperationalContextGrid } from "@/components/OperationalContextGrid";
import { PartySnapshotPanel } from "@/components/PartySnapshotPanel";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { term, type TerminologyMap } from "@/lib/terminology";
import {
  billTypeLabel,
  getBill,
  getFinancialProfile,
  getProfitability,
  operationalStatusLabel,
  type CurrencyFinancialBucket,
  type MaturityBreakdown,
} from "@/lib/bills";
import { listCostsByBill, listRevenuesByBill } from "@/lib/costs-revenues-server";
import { listAccountsPayable, listAccountsReceivable } from "@/lib/ap-ar";
import { listFinancialDocuments } from "@/lib/documents";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;
type SearchParams = Promise<{ tab?: string }>;

type BillTab =
  | "overview"
  | "costs"
  | "revenues"
  | "documents"
  | "rating"
  | "history";

function parseTab(raw: string | undefined): BillTab {
  switch (raw) {
    case "costs":
    case "revenues":
    case "documents":
    case "rating":
    case "history":
      return raw;
    default:
      return "overview";
  }
}

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
            Chỉ số tài chính {revenue} / {directCost} theo {bucket.currencyCode}
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
          <dt>
            {variance} ({profit})
          </dt>
          <dd>{formatMoney(bucket.profitVarianceExpectedVsActual, bucket.currencyCode)}</dd>
        </div>
      </dl>
    </div>
  );
}

export default async function BillDetailPage({
  params,
  searchParams,
}: {
  params: Params;
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const { tab: tabRaw } = await searchParams;
  const tab = parseTab(tabRaw);

  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const profileLabel = term(terms, "BILL_FINANCIAL_PROFILE", "Hồ sơ tài chính Bill");
  const profitLabel = term(terms, "BILL_PROFITABILITY", "Lợi nhuận theo Bill");
  const settlementLabel = term(terms, "SETTLEMENT_OUTSTANDING", "Số dư tất toán còn lại");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const best = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ");

  const [billRes, profileRes, profitRes, costsRes, revenuesRes, apRes, arRes, docsRes] =
    await Promise.all([
      getBill(id),
      getFinancialProfile(id),
      getProfitability(id, "best"),
      listCostsByBill(id),
      listRevenuesByBill(id),
      listAccountsPayable(),
      listAccountsReceivable(),
      listFinancialDocuments({ billId: id }),
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
  const costCount = costsRes.ok ? costsRes.data.length : 0;
  const revenueCount = revenuesRes.ok ? revenuesRes.data.length : 0;
  const docCount = docsRes.ok ? docsRes.data.items.length : 0;
  const tabHref = (t: BillTab) =>
    t === "overview" ? `/bills/${id}` : `/bills/${id}?tab=${t}`;

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
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          <Link href="/bills">Đơn hàng vận chuyển</Link>
          {" / "}
          <span>
            {billLabel} {bill.billNo}
          </span>
        </p>

        <div className="page-header-row">
          <div>
            <h1>
              {billLabel} {bill.billNo}
            </h1>
            <p className="lede meta-line">
              <span className="status-pill">
                {operationalStatusLabel(bill.operationalStatus)}
              </span>
              {" · "}
              {billTypeLabel(bill.billType)}
              {bill.sourceSystem ? ` · Nguồn: ${bill.sourceSystem}` : ""}
              {bill.externalId ? ` · Mã ngoài: ${bill.externalId}` : ""}
            </p>
          </div>
        </div>

        <div className="filter-tabs" role="tablist" aria-label="Tab hồ sơ Bill">
          <Link
            className={tab === "overview" ? "active" : undefined}
            href={tabHref("overview")}
            role="tab"
            aria-selected={tab === "overview"}
          >
            Tổng quan
          </Link>
          <Link
            className={tab === "costs" ? "active" : undefined}
            href={tabHref("costs")}
            role="tab"
            aria-selected={tab === "costs"}
          >
            {costLabel} ({costCount})
          </Link>
          <Link
            className={tab === "revenues" ? "active" : undefined}
            href={tabHref("revenues")}
            role="tab"
            aria-selected={tab === "revenues"}
          >
            {revenueLabel} ({revenueCount})
          </Link>
          <Link
            className={tab === "documents" ? "active" : undefined}
            href={tabHref("documents")}
            role="tab"
            aria-selected={tab === "documents"}
          >
            {docLabel} ({docCount})
          </Link>
          <Link
            className={tab === "rating" ? "active" : undefined}
            href={tabHref("rating")}
            role="tab"
            aria-selected={tab === "rating"}
          >
            Tính giá
          </Link>
          <Link
            className={tab === "history" ? "active" : undefined}
            href={tabHref("history")}
            role="tab"
            aria-selected={tab === "history"}
          >
            Lịch sử
          </Link>
        </div>

        {tab === "overview" ? (
          <>
            <div className="toolbar-row" role="group" aria-label="Hành động nhanh">
              <Link className="btn btn-sm" href={`/bills/${id}/costs/new`}>
                + Thêm {costLabel.toLowerCase()}
              </Link>
              <Link className="btn btn-sm" href={`/bills/${id}/revenues/new`}>
                + Thêm {revenueLabel.toLowerCase()}
              </Link>
              <Link className="btn btn-sm btn-ghost" href={`/documents?billId=${id}`}>
                Chứng từ
              </Link>
              <Link className="btn btn-sm btn-ghost" href={tabHref("rating")}>
                Tính giá
              </Link>
            </div>

            <h2 className="section-title">Ngữ cảnh vận hành</h2>
            <OperationalContextGrid row={bill} />
            <FieldOwnershipPanel objectType="bill" objectId={bill.id} />
            <h2 className="section-title">Snapshot đối tác</h2>
            <PartySnapshotPanel billId={bill.id} />

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
                  <div className="alert alert-info">
                    {profileRes.data.asOfLimitationNote}
                  </div>
                ) : null}

                {profileRes.data.byCurrency.length === 0 ? (
                  <div className="empty-state" role="status">
                    Chưa có dòng {costLabel.toLowerCase()} /{" "}
                    {revenueLabel.toLowerCase()} trên {billLabel} này.
                  </div>
                ) : (
                  <div className="stack">
                    {profileRes.data.byCurrency.map((b) => (
                      <CurrencyProfileCard
                        key={b.currencyCode}
                        bucket={b}
                        terms={terms}
                      />
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
                                {formatMoney(
                                  s.accountsPayableOutstanding,
                                  s.currencyCode
                                )}
                              </td>
                              <td className="num">
                                {formatMoney(
                                  s.accountsReceivableOutstanding,
                                  s.currencyCode
                                )}
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
                  {profitRes.data.view}) ·{" "}
                  {formatDateTimeVi(profitRes.data.asOfTimestamp)}
                </p>
                {profitRes.data.note ? (
                  <p className="note">{profitRes.data.note}</p>
                ) : null}
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th scope="col">Tiền tệ</th>
                        <th scope="col" className="num">
                          {revenueLabel}
                        </th>
                        <th scope="col" className="num">
                          {term(terms, "DIRECT_COST", "Chi phí trực tiếp")}
                        </th>
                        <th scope="col" className="num">
                          {term(terms, "COST_ALLOCATION", "Phân bổ")}
                        </th>
                        <th scope="col" className="num">
                          {costLabel}
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
                          <td className="num">
                            {formatMoney(p.revenueAmount, p.currencyCode)}
                          </td>
                          <td className="num">
                            {formatMoney(p.directCostAmount, p.currencyCode)}
                          </td>
                          <td className="num">
                            {formatMoney(p.allocatedCostAmount, p.currencyCode)}
                          </td>
                          <td className="num">
                            {formatMoney(p.costAmount, p.currencyCode)}
                          </td>
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
          </>
        ) : null}

        {tab === "costs" || tab === "revenues" ? (
          <BillCostRevenuePanel
            terms={terms}
            billId={id}
            costs={costsRes.ok ? costsRes.data : null}
            costsError={costsRes.ok ? null : costsRes.message}
            revenues={revenuesRes.ok ? revenuesRes.data : null}
            revenuesError={revenuesRes.ok ? null : revenuesRes.message}
            focus={tab === "costs" ? "costs" : "revenues"}
          />
        ) : null}

        {tab === "documents" ? (
          <BillDocumentsApArPanel
            terms={terms}
            billId={id}
            documents={docsRes.ok ? docsRes.data.items : null}
            documentsError={docsRes.ok ? null : docsRes.message}
            payables={apRes.ok ? apRes.data : null}
            payablesError={apRes.ok ? null : apRes.message}
            receivables={arRes.ok ? arRes.data : null}
            receivablesError={arRes.ok ? null : arRes.message}
          />
        ) : null}

        {tab === "rating" ? <BillRatingPanel terms={terms} billId={id} /> : null}

        {tab === "history" ? (
          <AuditTrailPanel
            terms={terms}
            objectType="Bill"
            objectId={id}
            title="Lịch sử / audit"
          />
        ) : null}
      </section>
    </AppShell>
  );
}
