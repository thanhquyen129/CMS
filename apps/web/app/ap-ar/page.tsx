import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  agingBucketLabel,
  exposureStatusLabel,
  isOutstanding,
  listAccountsPayable,
  listAccountsReceivable,
  listPayableExposures,
  listReceivableExposures,
  settlementStatusLabel,
} from "@/lib/ap-ar";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ tab?: string }>;

export default async function ApArPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { tab } = await searchParams;
  const activeTab =
    tab === "ar" || tab === "exposure" ? tab : "ap";

  const terms = await fetchTerminology();
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const outstandingLabel = term(terms, "OUTSTANDING", "Số dư còn lại");
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

  const apItems = apRes.ok ? apRes.data.filter(isOutstanding) : [];
  const arItems = arRes.ok ? arRes.data.filter(isOutstanding) : [];
  const peItems = peRes.ok
    ? peRes.data.filter((e) => e.openAmount > 0 || e.status !== "recognized")
    : [];
  const reItems = reRes.ok
    ? reRes.data.filter((e) => e.openAmount > 0 || e.status !== "recognized")
    : [];

  return (
    <AppShell terms={terms} active="ap-ar">
      <section className="panel panel-wide">
        <h1>
          {apLabel} / {arLabel}
        </h1>
        <p className="lede">
          Đọc {outstandingLabel} đã ghi nhận. {apLabel} ≠ {costLabel}; {arLabel} ≠{" "}
          {revenueLabel}; tất toán / {paymentLabel} là bước riêng (chưa UI U4).
        </p>

        <div className="search-bar" role="tablist" aria-label="Chọn sổ">
          <Link
            className={activeTab === "ap" ? "btn" : "btn btn-ghost"}
            href="/ap-ar"
            role="tab"
            aria-selected={activeTab === "ap"}
          >
            {apLabel}
          </Link>
          <Link
            className={activeTab === "ar" ? "btn" : "btn btn-ghost"}
            href="/ap-ar?tab=ar"
            role="tab"
            aria-selected={activeTab === "ar"}
          >
            {arLabel}
          </Link>
          <Link
            className={activeTab === "exposure" ? "btn" : "btn btn-ghost"}
            href="/ap-ar?tab=exposure"
            role="tab"
            aria-selected={activeTab === "exposure"}
          >
            Exposure
          </Link>
        </div>

        {activeTab === "ap" ? (
          <>
            <h2 className="section-title sm">
              {apLabel} — {outstandingLabel} &gt; 0
            </h2>
            {!apRes.ok ? (
              <div className="alert alert-error" role="alert">
                {apRes.message}
              </div>
            ) : apItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có {apLabel.toLowerCase()} còn dư. (Đã tất toán hoặc chưa
                ghi nhận từ exposure.)
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">{billLabel}</th>
                      <th scope="col" className="num">
                        Đã ghi nhận
                      </th>
                      <th scope="col" className="num">
                        {outstandingLabel}
                      </th>
                      <th scope="col">Trạng thái tất toán</th>
                      <th scope="col">Hạn</th>
                      <th scope="col">{agingLabel}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {apItems.map((row) => (
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
                        <td className="num">
                          {formatMoney(row.outstanding, row.currencyCode)}
                        </td>
                        <td>
                          {settlementStatusLabel(terms, row.settlementStatus)}
                        </td>
                        <td>{row.dueDate ?? "—"}</td>
                        <td>
                          {agingBucketLabel(row.agingBucket)}
                          {row.daysPastDue != null && row.daysPastDue > 0
                            ? ` · ${row.daysPastDue} ngày`
                            : ""}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : null}

        {activeTab === "ar" ? (
          <>
            <h2 className="section-title sm">
              {arLabel} — {outstandingLabel} &gt; 0
            </h2>
            {!arRes.ok ? (
              <div className="alert alert-error" role="alert">
                {arRes.message}
              </div>
            ) : arItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có {arLabel.toLowerCase()} còn dư.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">{billLabel}</th>
                      <th scope="col" className="num">
                        Đã ghi nhận
                      </th>
                      <th scope="col" className="num">
                        {outstandingLabel}
                      </th>
                      <th scope="col">Trạng thái tất toán</th>
                      <th scope="col">Hạn</th>
                      <th scope="col">{agingLabel}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {arItems.map((row) => (
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
                        <td className="num">
                          {formatMoney(row.outstanding, row.currencyCode)}
                        </td>
                        <td>
                          {settlementStatusLabel(terms, row.settlementStatus)}
                        </td>
                        <td>{row.dueDate ?? "—"}</td>
                        <td>
                          {agingBucketLabel(row.agingBucket)}
                          {row.daysPastDue != null && row.daysPastDue > 0
                            ? ` · ${row.daysPastDue} ngày`
                            : ""}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : null}

        {activeTab === "exposure" ? (
          <>
            <p className="note">
              Exposure = nghĩa vụ / quyền dự kiến trước khi ghi nhận AP/AR.
              Chưa phải {paymentLabel}.
            </p>

            <h2 className="section-title sm">{payableExposureLabel}</h2>
            {!peRes.ok ? (
              <div className="alert alert-error" role="alert">
                {peRes.message}
              </div>
            ) : peItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có exposure phải trả còn mở.
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
                Không có exposure phải thu còn mở.
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
