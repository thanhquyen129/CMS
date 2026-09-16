import Link from "next/link";
import { RateBillForm } from "@/components/RateBillForm";
import { SeedExpectedCostsButton } from "@/components/SeedExpectedCostsButton";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import type { RateCard, RateVersion } from "@/lib/rate-cards";
import {
  listRateCards as fetchRateCards,
  listRateVersions as fetchRateVersions,
  listRatingsByBill as fetchRatingsByBill,
} from "@/lib/rate-cards-server";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  billId: string;
};

export async function BillRatingPanel({ terms, billId }: Props) {
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");

  const [cardsRes, ratingsRes] = await Promise.all([
    fetchRateCards(),
    fetchRatingsByBill(billId),
  ]);

  let cardsWithVersions: { card: RateCard; versions: RateVersion[] }[] = [];
  if (cardsRes.ok) {
    cardsWithVersions = await Promise.all(
      cardsRes.data.items.map(async (card) => {
        const versionsRes = await fetchRateVersions(card.id);
        return {
          card,
          versions: versionsRes.ok ? versionsRes.data : [],
        };
      })
    );
  }

  const ratings = ratingsRes.ok ? ratingsRes.data : [];

  return (
    <div className="stack" style={{ marginTop: "1.5rem" }}>
      <h2 className="section-title">Tính giá / Rating</h2>
      <p className="note">
        Áp bảng giá đã phát hành lên {billLabel} này. Seed tạo{" "}
        {costLabel.toLowerCase()} lớp {expected} (không ghi đè dòng đã seed).{" "}
        <Link className="row-link" href="/rate-cards">
          Quản lý bảng giá
        </Link>
      </p>

      {!cardsRes.ok ? (
        <div className="alert alert-error" role="alert">
          {cardsRes.message}
        </div>
      ) : (
        <RateBillForm
          terms={terms}
          billId={billId}
          cardsWithVersions={cardsWithVersions}
        />
      )}

      <h3 className="section-title sm">Lịch sử tính giá</h3>
      {!ratingsRes.ok ? (
        <div className="alert alert-error" role="alert">
          {ratingsRes.message}
        </div>
      ) : ratings.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có lần tính giá trên {billLabel} này.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Thời điểm</th>
                <th scope="col" className="num">
                  Tổng
                </th>
                <th scope="col" className="num">
                  SL
                </th>
                <th scope="col">Trạng thái</th>
                <th scope="col">Seed</th>
              </tr>
            </thead>
            <tbody>
              {ratings.map((r) => (
                <tr key={r.id}>
                  <td>{formatDateTimeVi(r.ratedAt)}</td>
                  <td className="num">
                    {formatMoney(r.totalAmount, r.currencyCode)}
                  </td>
                  <td className="num">{r.quantity}</td>
                  <td>{r.status}</td>
                  <td>
                    {r.status?.toLowerCase() === "superseded" ? (
                      <span className="muted">—</span>
                    ) : (
                      <SeedExpectedCostsButton terms={terms} ratingId={r.id} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
