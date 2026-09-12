import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listSharedCosts } from "@/lib/costs-revenues-server";
import { maturityLabelKey } from "@/lib/costs-revenues";
import { formatMoney } from "@/lib/money";

export default async function SharedCostsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");
  const billLabel = term(terms, "BILL", "Bill");
  const allocLabel = term(terms, "COST_ALLOCATION", "Phân bổ chi phí");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");

  const listRes = await listSharedCosts();
  const costs = listRes.ok ? listRes.data : [];

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel panel-wide">
        <h1>
          {costLabel} {sharedLabel.toLowerCase()}
        </h1>
        <p className="lede">
          {costLabel} chung không gắn {billLabel}. Phân bổ (equal / quantity /
          manual_ratio) → nháp → <strong>chốt</strong> mới vào hồ sơ {billLabel}.{" "}
          {costLabel} ≠ Thanh toán.
        </p>

        <p className="cta-row" style={{ marginTop: 0 }}>
          <Link className="btn" href="/costs/shared/new">
            Tạo {costLabel.toLowerCase()} {sharedLabel.toLowerCase()}
          </Link>
          <Link className="btn btn-ghost" href="/bills">
            Danh sách {billLabel}
          </Link>
        </p>

        {!listRes.ok ? (
          <div className="alert alert-error" role="alert">
            {listRes.message}
          </div>
        ) : costs.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {costLabel.toLowerCase()} {sharedLabel.toLowerCase()}. Tạo
            mới rồi {allocLabel.toLowerCase()} sang ≥2 {billLabel}.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Mã / loại</th>
                  <th scope="col">Độ chín</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col">Ngày hiệu lực</th>
                  <th scope="col">
                    <span className="sr-only">Mở</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {costs.map((c) => {
                  const maturityKey = maturityLabelKey(c.financialMaturity);
                  const maturityVi =
                    maturityKey === "EXPECTED"
                      ? expected
                      : maturityKey === "CONFIRMED"
                        ? confirmed
                        : maturityKey === "ACTUAL"
                          ? actual
                          : c.financialMaturity;
                  return (
                    <tr key={c.id}>
                      <td>
                        <Link
                          className="row-link"
                          href={`/costs/shared/${c.id}`}
                        >
                          {c.costTypeCode || c.id.slice(0, 8)}
                        </Link>
                      </td>
                      <td>{maturityVi}</td>
                      <td className="num">
                        {formatMoney(c.amount, c.currencyCode)}
                      </td>
                      <td>{c.effectiveDate}</td>
                      <td>
                        <Link
                          className="btn btn-sm btn-ghost"
                          href={`/costs/shared/${c.id}`}
                        >
                          Phân bổ
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
