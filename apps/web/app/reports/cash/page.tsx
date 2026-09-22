import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE, getApiInternalUrl } from "@/lib/auth";
import { fetchTerminology, getSessionToken, term } from "@/lib/api";
import { formatMoney } from "@/lib/money";
import type { ApiResult } from "@/lib/bills";

type SearchParams = Promise<{ asOf?: string }>;

type CashRow = {
  id: string;
  kind: string;
  amount: number;
  allocatedAmount: number;
  unappliedAmount: number;
  currencyCode: string;
  valueDate: string;
  billId: string | null;
  status: string;
};

type CashReport = {
  asOf: string | null;
  items: CashRow[];
  unappliedTotal: number;
};

async function getCashReport(asOf?: string): Promise<ApiResult<CashReport>> {
  const token = await getSessionToken();
  if (!token) {
    redirect("/login");
  }

  const qs = asOf ? `?asOf=${encodeURIComponent(asOf)}` : "";
  try {
    const res = await fetch(
      `${getApiInternalUrl()}/api/reports/cash-settlement${qs}`,
      {
        headers: {
          Accept: "application/json",
          Authorization: `Bearer ${token}`,
        },
        cache: "no-store",
      }
    );
    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message: body.message || "Không tải được báo cáo tiền.",
      };
    }
    return { ok: true, data: (await res.json()) as CashReport };
  } catch {
    return {
      ok: false,
      status: 0,
      message: "Không kết nối được máy chủ API.",
    };
  }
}

export default async function CashSettlementReportPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const result = await getCashReport(sp.asOf);

  return (
    <AppShell terms={terms} active="reports">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/reports", label: "Báo cáo & Phân tích" },
            { label: "Tiền và tất toán" },
          ]}
          title="Tiền và tất toán"
          lede={`Số tiền thanh toán/thu chưa gán theo as-of tường minh. Drill về ${billLabel} hoặc giao dịch.`}
        />

        <form className="filter-bar" method="get">
          <label>
            As-of
            <input type="date" name="asOf" defaultValue={sp.asOf ?? ""} />
          </label>
          <button type="submit" className="btn btn-sm">
            Áp dụng
          </button>
          <Link className="btn btn-ghost btn-sm" href="/reports/cash">
            Xóa lọc
          </Link>
        </form>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : (
          <>
            <p className="meta-line muted">
              Tổng chưa gán (các dòng):{" "}
              {formatMoney(result.data.unappliedTotal, "VND")}
              {result.data.asOf ? ` · as-of ${result.data.asOf}` : " · hiện tại"}
            </p>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Loại</th>
                    <th>Ngày giá trị</th>
                    <th>Số tiền</th>
                    <th>Đã gán</th>
                    <th>Chưa gán</th>
                    <th>{billLabel}</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {result.data.items.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="muted">
                        Không có giao dịch tiền.
                      </td>
                    </tr>
                  ) : (
                    result.data.items.map((row) => (
                      <tr key={`${row.kind}-${row.id}`}>
                        <td>
                          <Link
                            href={
                              row.kind === "payment"
                                ? `/settlements/payments/${row.id}`
                                : `/settlements/collections/${row.id}`
                            }
                          >
                            {row.kind === "payment" ? "Thanh toán" : "Thu tiền"}
                          </Link>
                        </td>
                        <td>{row.valueDate}</td>
                        <td>{formatMoney(row.amount, row.currencyCode)}</td>
                        <td>
                          {formatMoney(row.allocatedAmount, row.currencyCode)}
                        </td>
                        <td>
                          {formatMoney(row.unappliedAmount, row.currencyCode)}
                        </td>
                        <td>
                          {row.billId ? (
                            <Link href={`/bills/${row.billId}`}>Xem</Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td>
                          <span className="status-pill">{row.status}</span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
      </section>
    </AppShell>
  );
}
