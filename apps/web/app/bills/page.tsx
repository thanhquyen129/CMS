import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  getFinancialProfile,
  listBills,
  operationalStatusLabel,
  billTypeLabel,
  profileBestRollup,
  type BillFinancialProfile,
  type BillListItem,
} from "@/lib/bills";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type SearchParams = Promise<{ q?: string; status?: string }>;

const PROFILE_ENRICH_LIMIT = 40;

export default async function BillsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { q, status } = await searchParams;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const costLabel = term(terms, "COST", "Chi phí");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");
  const bestLabel = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");

  const result = await listBills(q);
  let bills: BillListItem[] = result.ok ? result.data : [];
  if (status?.trim()) {
    const s = status.trim().toLowerCase();
    bills = bills.filter((b) => b.operationalStatus?.toLowerCase() === s);
  }

  const enrichTargets = bills.slice(0, PROFILE_ENRICH_LIMIT);
  const profiles = await Promise.all(
    enrichTargets.map((b) => getFinancialProfile(b.id))
  );
  const profileById = new Map<string, BillFinancialProfile | null>();
  enrichTargets.forEach((b, i) => {
    const res = profiles[i];
    profileById.set(b.id, res.ok ? res.data : null);
  });

  let sumRevExpected = 0;
  let sumRevConfirmed = 0;
  let sumRevActual = 0;
  let rollCurrency = "VND";
  for (const b of enrichTargets) {
    const roll = profileBestRollup(profileById.get(b.id) ?? null);
    if (!roll) continue;
    rollCurrency = roll.currencyCode;
    sumRevExpected += roll.revenueExpected;
    sumRevConfirmed += roll.revenueConfirmed;
    sumRevActual += roll.revenueActual;
  }

  const statusOptions = Array.from(
    new Set((result.ok ? result.data : []).map((b) => b.operationalStatus).filter(Boolean))
  );

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Đơn hàng vận chuyển
          {" / "}
          Danh sách {billLabel}
        </p>
        <div className="page-header-row">
          <div>
            <h1>Danh sách {billLabel}</h1>
            <p className="lede">
              Quản lý vận đơn ({billLabel}) và thông tin tài chính liên quan —{" "}
              {expectedLabel} / {confirmedLabel} / {actualLabel}. {billLabel} là neo
              tài chính (Financial Anchor).
            </p>
          </div>
          <Link className="btn" href="/bills/new">
            + Tạo {billLabel}
          </Link>
        </div>

        <form className="search-bar" method="get" action="/bills" role="search">
          <label className="sr-only" htmlFor="q">
            Tìm {billLabel}
          </label>
          <input
            id="q"
            name="q"
            type="search"
            placeholder={`Tìm theo số ${billLabel}, reference…`}
            defaultValue={q ?? ""}
            autoComplete="off"
          />
          <label className="sr-only" htmlFor="status">
            Trạng thái
          </label>
          <select id="status" name="status" defaultValue={status ?? ""}>
            <option value="">Tất cả trạng thái</option>
            {statusOptions.map((s) => (
              <option key={s} value={s}>
                {operationalStatusLabel(s)}
              </option>
            ))}
          </select>
          <button className="btn" type="submit">
            Lọc
          </button>
          {q || status ? (
            <Link className="btn btn-ghost" href="/bills">
              Làm mới
            </Link>
          ) : null}
        </form>

        {result.ok ? (
          <div className="stat-grid" style={{ marginTop: "0.85rem" }}>
            <div className="stat-card">
              <span className="stat-label">Tổng số {billLabel}</span>
              <strong className="stat-value">{bills.length}</strong>
              <span className="stat-hint">
                {result.data.length !== bills.length
                  ? `Trong ${result.data.length} bản ghi`
                  : "Trong phạm vi của bạn"}
              </span>
            </div>
            <div className="stat-card">
              <span className="stat-label">
                {revenueLabel} ({expectedLabel})
              </span>
              <strong className="stat-value">
                {formatMoney(sumRevExpected, rollCurrency)}
              </strong>
              <span className="stat-hint">Projection — không phải sổ cái</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">
                {revenueLabel} ({confirmedLabel})
              </span>
              <strong className="stat-value">
                {formatMoney(sumRevConfirmed, rollCurrency)}
              </strong>
              <span className="stat-hint">{bestLabel}</span>
            </div>
            <div className="stat-card">
              <span className="stat-label">
                {revenueLabel} ({actualLabel})
              </span>
              <strong className="stat-value">
                {formatMoney(sumRevActual, rollCurrency)}
              </strong>
              <span className="stat-hint">
                {enrichTargets.length < bills.length
                  ? `Tối đa ${PROFILE_ENRICH_LIMIT} dòng đầu`
                  : "Theo hồ sơ tài chính"}
              </span>
            </div>
          </div>
        ) : null}

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : bills.length === 0 ? (
          <div className="empty-state" role="status">
            {q?.trim() || status ? (
              `Không có ${billLabel} khớp bộ lọc. Thử điều kiện khác.`
            ) : (
              <>
                Chưa có {billLabel} nào trong phạm vi của bạn.{" "}
                <Link className="row-link" href="/bills/new">
                  Tạo {billLabel} mới
                </Link>
                .
              </>
            )}
          </div>
        ) : (
          <div className="table-wrap" style={{ marginTop: "1rem" }}>
            <table className="data-table">
              <caption className="sr-only">
                Danh sách {billLabel} ({bills.length})
              </caption>
              <thead>
                <tr>
                  <th scope="col">Số {billLabel}</th>
                  <th scope="col">Loại</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Ngày tạo</th>
                  <th scope="col" className="num">
                    {revenueLabel}
                  </th>
                  <th scope="col" className="num">
                    {costLabel}
                  </th>
                  <th scope="col" className="num">
                    {profitLabel}
                  </th>
                  <th scope="col">
                    <span className="sr-only">Mở</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {bills.map((b) => {
                  const roll = profileById.has(b.id)
                    ? profileBestRollup(profileById.get(b.id) ?? null)
                    : null;
                  return (
                    <tr key={b.id}>
                      <td>
                        <Link className="row-link" href={`/bills/${b.id}`}>
                          {b.billNo}
                        </Link>
                      </td>
                      <td>{billTypeLabel(b.billType)}</td>
                      <td>
                        <span className="status-pill">
                          {operationalStatusLabel(b.operationalStatus)}
                        </span>
                      </td>
                      <td>{formatDateTimeVi(b.createdAt)}</td>
                      <td className="num">
                        {roll
                          ? formatMoney(roll.revenue, roll.currencyCode)
                          : "—"}
                      </td>
                      <td className="num">
                        {roll ? formatMoney(roll.cost, roll.currencyCode) : "—"}
                      </td>
                      <td
                        className={`num${roll && roll.profit < 0 ? " neg" : ""}`}
                      >
                        {roll
                          ? formatMoney(roll.profit, roll.currencyCode)
                          : "—"}
                      </td>
                      <td>
                        <Link
                          className="btn btn-ghost btn-sm"
                          href={`/bills/${b.id}`}
                        >
                          Mở hồ sơ
                        </Link>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}
