export type ExpectedProfitInput = {
  reportingCurrency?: string | null;
  reportingRevenue?: number | null;
  reportingCost?: number | null;
  reportingProfit?: number | null;
  reportingComplete?: boolean;
  missingFxCount?: number;
  unconvertedCurrencies?: string[] | null;
  byCurrency: { revenueLineCount: number; directCostLineCount: number }[];
};

export type ExpectedProfitView =
  | { kind: "empty"; text: string }
  | { kind: "one-side"; text: string }
  | { kind: "fx-missing"; text: string }
  | { kind: "profit"; currency: string; amount: number; text: string };

/** Hide a profit figure when one side or FX is missing. Never substitute 0. */
export function expectedProfitView(input: ExpectedProfitInput | null): ExpectedProfitView {
  if (!input) {
    return { kind: "empty", text: "Chưa có chi phí và doanh thu dự kiến." };
  }
  const revenueLines = input.byCurrency.reduce((n, row) => n + row.revenueLineCount, 0);
  const costLines = input.byCurrency.reduce((n, row) => n + row.directCostLineCount, 0);
  if (revenueLines === 0 && costLines === 0) {
    return { kind: "empty", text: "Chưa có chi phí và doanh thu dự kiến." };
  }
  if (revenueLines === 0 || costLines === 0) {
    return {
      kind: "one-side",
      text: "Chỉ có một chiều dự kiến. Không hiển thị lợi nhuận.",
    };
  }
  const currency = input.reportingCurrency;
  const amount = input.reportingProfit;
  const fxMissing =
    input.reportingComplete === false
    || (input.missingFxCount ?? 0) > 0
    || (input.unconvertedCurrencies?.length ?? 0) > 0
    || amount == null
    || !currency;
  if (fxMissing || currency == null || amount == null) {
    return {
      kind: "fx-missing",
      text: "Thiếu tỷ giá sang tiền tệ báo cáo. Không hiển thị lợi nhuận quy đổi.",
    };
  }
  return {
    kind: "profit",
    currency,
    amount,
    text: "Lợi nhuận dự kiến",
  };
}
