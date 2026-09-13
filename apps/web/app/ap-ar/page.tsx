import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ReverseRecognizeButton } from "@/components/ReverseRecognizeButton";
import { WriteOffButton } from "@/components/WriteOffButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  agingBucketLabel,
  exposureStatusLabel,
  filterByApArStatus,
  listAccountsPayable,
  listAccountsReceivable,
  listPayableExposures,
  listReceivableExposures,
  parseApArStatusFilter,
  settlementStatusLabel,
  type ApArStatusFilter,
  type AccountsPayableItem,
  type AccountsReceivableItem,
} from "@/lib/ap-ar";
import { formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";

type SearchParams = Promise<{ tab?: string; status?: string }>;

function apArHref(opts: {
  tab?: string;
  status?: ApArStatusFilter;
}): string {
  const p = new URLSearchParams();
  if (opts.tab && opts.tab !== "ap") p.set("tab", opts.tab);
  if (opts.status && opts.status !== "outstanding") {
    p.set("status", opts.status);
  }
  const qs = p.toString();
  return qs ? `/ap-ar?${qs}` : "/ap-ar";
}

function statusFilterLabel(
  terms: TerminologyMap,
  filter: ApArStatusFilter
): string {
  if (filter === "settled") {
    return term(terms, "SETTLEMENT_SETTLED", "Đã tất toán");
  }
  if (filter === "all") return "Tất cả";
  return term(terms, "OUTSTANDING", "Còn dư");
}

function ApArTable({
  terms,
  items,
  kind,
  billLabel,
  outstandingLabel,
  agingLabel,
  showSettledAmount,
}: {
  terms: TerminologyMap;
  items: (AccountsPayableItem | AccountsReceivableItem)[];
  kind: "payable" | "receivable";
  billLabel: string;
  outstandingLabel: string;
  agingLabel: string;
  showSettledAmount: boolean;
}) {
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">{billLabel}</th>
            <th scope="col" className="num">
              Đã ghi nhận
            </th>
            {showSettledAmount ? (
              <th scope="col" className="num">
                Đã tất toán
              </th>
            ) : null}
            <th scope="col" className="num">
              {outstandingLabel}
            </th>
            <th scope="col">Trạng thái tất toán</th>
            <th scope="col">Hạn</th>
            <th scope="col">{agingLabel}</th>
            <th scope="col">Thao tác</th>
          </tr>
        </thead>
        <tbody>
          {items.map((row) => (
            <tr key={row.id}>
              <td>
                {row.billId ? (
                  <Link className="row-link" href={`/bills/${row.billId}`}>
                    Mở {billLabel}
                  </Link>
                ) : (
                  <span className="muted">—</span>
                )}
              </td>
              <td className="num">
                {formatMoney(row.recognizedAmount, row.currencyCode)}
              </td>
              {showSettledAmount ? (
                <td className="num">
                  {formatMoney(row.finalizedSettledAmount, row.currencyCode)}
                </td>
              ) : null}
              <td className="num">
                {formatMoney(row.outstanding, row.currencyCode)}
              </td>
              <td>{settlementStatusLabel(terms, row.settlementStatus)}</td>
              <td>{row.dueDate ?? "—"}</td>
              <td>
                {agingBucketLabel(row.agingBucket)}
                {row.daysPastDue != null && row.daysPastDue > 0
                  ? ` · ${row.daysPastDue} ngày`
                  : ""}
              </td>
              <td>
                <div className="cta-row" style={{ gap: "0.35rem", flexWrap: "wrap" }}>
                  {row.outstanding > 0 ? (
                    <WriteOffButton
                      terms={terms}
                      kind={kind}
                      accountsId={row.id}
                      outstanding={row.outstanding}
                      currencyCode={row.currencyCode}
                    />
                  ) : null}
                  <ReverseRecognizeButton
                    terms={terms}
                    kind={kind}
                    accountsId={row.id}
                    outstanding={row.outstanding}
                    currencyCode={row.currencyCode}
                    settledAmount={row.finalizedSettledAmount}
                    recordStatus={row.recordStatus}
                  />
                  {row.outstanding <= 0 &&
                  row.recordStatus?.toLowerCase() !== "active" ? (
                    <span className="muted small">—</span>
                  ) : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default async function ApArPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { tab, status: statusRaw } = await searchParams;
  const activeTab =
    tab === "ar" || tab === "exposure" ? tab : "ap";
  const statusFilter = parseApArStatusFilter(statusRaw);

  const terms = await fetchTerminology();
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const outstandingLabel = term(terms, "OUTSTANDING", "Số dư còn lại");
  const settledLabel = term(terms, "SETTLEMENT_SETTLED", "Đã tất toán");
  const agingLabel = term(terms, "AGING", "Tuổi nợ");
  const payableExposureLabel = term(
    terms,
    "PAYABLE_EXPOSURE",
    "Nghĩa vụ phải trả (exposure)"
  );
  const receivableExposureLabel = term(
    terms,
    "RECEIVABLE_EXPOSURE",
    "Quyền thu dự kiến (exposure)"
  );
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  const [apRes, arRes, peRes, reRes] = await Promise.all([
    listAccountsPayable(),
    listAccountsReceivable(),
    listPayableExposures(),
    listReceivableExposures(),
  ]);

  const apItems = apRes.ok
    ? filterByApArStatus(apRes.data, statusFilter)
    : [];
  const arItems = arRes.ok
    ? filterByApArStatus(arRes.data, statusFilter)
    : [];
  const apSettledCount = apRes.ok
    ? apRes.data.filter((r) => r.settlementStatus?.toLowerCase() === "settled")
        .length
    : 0;
  const arSettledCount = arRes.ok
    ? arRes.data.filter((r) => r.settlementStatus?.toLowerCase() === "settled")
        .length
    : 0;
  const peItems = peRes.ok
    ? peRes.data.filter((e) => e.openAmount > 0 || e.status !== "recognized")
    : [];
  const reItems = reRes.ok
    ? reRes.data.filter((e) => e.openAmount > 0 || e.status !== "recognized")
    : [];

  const showSettledAmount =
    statusFilter === "settled" || statusFilter === "all";

  return (
    <AppShell terms={terms} active="ap-ar">
      <section className="panel panel-wide">
        <h1>
          {apLabel} / {arLabel}
        </h1>
        <p className="lede">
          Đọc {outstandingLabel} đã ghi nhận và lịch sử {settledLabel.toLowerCase()}.{" "}
          {apLabel} ≠ {costLabel}; {arLabel} ≠ {revenueLabel}. Tất toán qua{" "}
          <Link className="row-link" href="/settlements">
            {paymentLabel} / thu tiền
          </Link>
          . Xóa nợ phần dư nhỏ = điều chỉnh (không phải {paymentLabel}).
        </p>

        <div className="toolbar-row" role="group" aria-label="Thao tác AP/AR">
          <Link className="btn btn-sm" href="/ap-ar/aging">
            Tóm tắt tuổi nợ
          </Link>
          <Link className="btn btn-sm" href="/ap-ar/exposures/new?kind=payable">
            Tạo exposure phải trả
          </Link>
          <Link
            className="btn btn-sm"
            href="/ap-ar/exposures/new?kind=receivable"
          >
            Tạo exposure phải thu
          </Link>
        </div>

        <div className="search-bar" role="tablist" aria-label="Chọn sổ">
          <Link
            className={activeTab === "ap" ? "btn" : "btn btn-ghost"}
            href={apArHref({ tab: "ap", status: statusFilter })}
            role="tab"
            aria-selected={activeTab === "ap"}
          >
            {apLabel}
          </Link>
          <Link
            className={activeTab === "ar" ? "btn" : "btn btn-ghost"}
            href={apArHref({ tab: "ar", status: statusFilter })}
            role="tab"
            aria-selected={activeTab === "ar"}
          >
            {arLabel}
          </Link>
          <Link
            className={activeTab === "exposure" ? "btn" : "btn btn-ghost"}
            href={apArHref({ tab: "exposure" })}
            role="tab"
            aria-selected={activeTab === "exposure"}
          >
            Exposure
          </Link>
        </div>

        {activeTab === "ap" || activeTab === "ar" ? (
          <div
            className="search-bar"
            role="group"
            aria-label="Lọc trạng thái tất toán"
          >
            <Link
              className={
                statusFilter === "outstanding" ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={apArHref({ tab: activeTab, status: "outstanding" })}
            >
              Còn dư
            </Link>
            <Link
              className={
                statusFilter === "settled" ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={apArHref({ tab: activeTab, status: "settled" })}
            >
              {settledLabel}
            </Link>
            <Link
              className={
                statusFilter === "all" ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={apArHref({ tab: activeTab, status: "all" })}
            >
              Tất cả
            </Link>
          </div>
        ) : null}

        {activeTab === "ap" ? (
          <>
            <h2 className="section-title sm">
              {apLabel} — {statusFilterLabel(terms, statusFilter)}
            </h2>
            {!apRes.ok ? (
              <div className="alert alert-error" role="alert">
                {apRes.message}
              </div>
            ) : apItems.length === 0 ? (
              <div className="empty-state" role="status">
                {statusFilter === "outstanding" ? (
                  <>
                    Không có {apLabel.toLowerCase()} còn dư.
                    {apSettledCount > 0 ? (
                      <>
                        {" "}
                        Có {apSettledCount} khoản{" "}
                        <Link
                          className="row-link"
                          href={apArHref({ tab: "ap", status: "settled" })}
                        >
                          {settledLabel.toLowerCase()}
                        </Link>
                        .
                      </>
                    ) : (
                      <> Chưa ghi nhận từ exposure hoặc chưa tất toán.</>
                    )}
                  </>
                ) : statusFilter === "settled" ? (
                  <>Không có {apLabel.toLowerCase()} đã tất toán.</>
                ) : (
                  <>Chưa có {apLabel.toLowerCase()} nào.</>
                )}
              </div>
            ) : (
              <ApArTable
                terms={terms}
                items={apItems}
                kind="payable"
                billLabel={billLabel}
                outstandingLabel={outstandingLabel}
                agingLabel={agingLabel}
                showSettledAmount={showSettledAmount}
              />
            )}
          </>
        ) : null}

        {activeTab === "ar" ? (
          <>
            <h2 className="section-title sm">
              {arLabel} — {statusFilterLabel(terms, statusFilter)}
            </h2>
            {!arRes.ok ? (
              <div className="alert alert-error" role="alert">
                {arRes.message}
              </div>
            ) : arItems.length === 0 ? (
              <div className="empty-state" role="status">
                {statusFilter === "outstanding" ? (
                  <>
                    Không có {arLabel.toLowerCase()} còn dư.
                    {arSettledCount > 0 ? (
                      <>
                        {" "}
                        Có {arSettledCount} khoản{" "}
                        <Link
                          className="row-link"
                          href={apArHref({ tab: "ar", status: "settled" })}
                        >
                          {settledLabel.toLowerCase()}
                        </Link>
                        .
                      </>
                    ) : null}
                  </>
                ) : statusFilter === "settled" ? (
                  <>Không có {arLabel.toLowerCase()} đã tất toán.</>
                ) : (
                  <>Chưa có {arLabel.toLowerCase()} nào.</>
                )}
              </div>
            ) : (
              <ApArTable
                terms={terms}
                items={arItems}
                kind="receivable"
                billLabel={billLabel}
                outstandingLabel={outstandingLabel}
                agingLabel={agingLabel}
                showSettledAmount={showSettledAmount}
              />
            )}
          </>
        ) : null}

        {activeTab === "exposure" ? (
          <>
            <p className="note">
              Exposure = nghĩa vụ / quyền dự kiến trước khi ghi nhận AP/AR.
              Chưa phải {paymentLabel}. Ghi nhận tạo sổ {apLabel}/{arLabel}{" "}
              riêng — không đổi {costLabel}/{revenueLabel}.
            </p>
            <p className="cta-row" style={{ marginTop: 0 }}>
              <Link
                className="btn btn-sm"
                href="/ap-ar/exposures/new?kind=payable"
              >
                Tạo {payableExposureLabel}
              </Link>{" "}
              <Link
                className="btn btn-sm"
                href="/ap-ar/exposures/new?kind=receivable"
              >
                Tạo {receivableExposureLabel}
              </Link>
            </p>

            <h2 className="section-title sm">{payableExposureLabel}</h2>
            {!peRes.ok ? (
              <div className="alert alert-error" role="alert">
                {peRes.message}
              </div>
            ) : peItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có exposure phải trả còn mở.{" "}
                <Link
                  className="row-link"
                  href="/ap-ar/exposures/new?kind=payable"
                >
                  Tạo exposure
                </Link>
                .
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">{billLabel}</th>
                      <th scope="col" className="num">
                        Số tiền
                      </th>
                      <th scope="col" className="num">
                        Còn mở
                      </th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {peItems.map((row) => (
                      <tr key={row.id}>
                        <td>
                          {row.billId ? (
                            <Link className="row-link" href={`/bills/${row.billId}`}>
                              Mở {billLabel}
                            </Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="num">
                          {formatMoney(row.amount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.openAmount, row.currencyCode)}
                        </td>
                        <td>{exposureStatusLabel(row.status)}</td>
                        <td>
                          {row.openAmount > 0 ? (
                            <Link
                              className="btn btn-sm"
                              href={`/ap-ar/exposures/${row.id}/recognize?kind=payable`}
                            >
                              Ghi nhận → {apLabel}
                            </Link>
                          ) : (
                            <span className="muted small">—</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <h2 className="section-title sm">{receivableExposureLabel}</h2>
            {!reRes.ok ? (
              <div className="alert alert-error" role="alert">
                {reRes.message}
              </div>
            ) : reItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có exposure phải thu còn mở.{" "}
                <Link
                  className="row-link"
                  href="/ap-ar/exposures/new?kind=receivable"
                >
                  Tạo exposure
                </Link>
                .
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">{billLabel}</th>
                      <th scope="col" className="num">
                        Số tiền
                      </th>
                      <th scope="col" className="num">
                        Còn mở
                      </th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {reItems.map((row) => (
                      <tr key={row.id}>
                        <td>
                          {row.billId ? (
                            <Link className="row-link" href={`/bills/${row.billId}`}>
                              Mở {billLabel}
                            </Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="num">
                          {formatMoney(row.amount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.openAmount, row.currencyCode)}
                        </td>
                        <td>{exposureStatusLabel(row.status)}</td>
                        <td>
                          {row.openAmount > 0 ? (
                            <Link
                              className="btn btn-sm"
                              href={`/ap-ar/exposures/${row.id}/recognize?kind=receivable`}
                            >
                              Ghi nhận → {arLabel}
                            </Link>
                          ) : (
                            <span className="muted small">—</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : null}
      </section>
    </AppShell>
  );
}
