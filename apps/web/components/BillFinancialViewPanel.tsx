"use client";

import { useMemo } from "react";
import { formatMoney } from "@/lib/money";

export type CostItem = {
  id: string;
  costNo?: string;
  costCode?: string;
  category: "DIRECT" | "ALLOCATED" | "PENDING_SHARED";
  name: string;
  amount: number;
  currency: string;
  maturity: "EXPECTED" | "CONFIRMED" | "ACTUAL";
  sourceRef?: string;
  sourceType?: "LEG" | "SHIPMENT" | "MOVEMENT" | "BILL";
};

export type RevenueItem = {
  id: string;
  revenueNo?: string;
  name: string;
  amount: number;
  currency: string;
  maturity: "EXPECTED" | "CONFIRMED" | "ACTUAL";
};

type Props = {
  billId: string;
  billNo: string;
  directCosts: CostItem[];
  allocatedCosts: CostItem[];
  pendingSharedCosts: CostItem[];
  revenues?: RevenueItem[];
  reportingCurrency?: string;
};

export function BillFinancialViewPanel({
  billNo,
  directCosts,
  allocatedCosts,
  pendingSharedCosts,
  revenues = [],
  reportingCurrency = "VND",
}: Props) {
  const directTotal = useMemo(
    () => directCosts.reduce((acc, c) => acc + c.amount, 0),
    [directCosts]
  );
  const allocatedTotal = useMemo(
    () => allocatedCosts.reduce((acc, c) => acc + c.amount, 0),
    [allocatedCosts]
  );
  const recognizedCostTotal = directTotal + allocatedTotal;

  const pendingSharedTotal = useMemo(
    () => pendingSharedCosts.reduce((acc, c) => acc + c.amount, 0),
    [pendingSharedCosts]
  );

  const revenueTotal = useMemo(
    () => revenues.reduce((acc, r) => acc + r.amount, 0),
    [revenues]
  );

  const currentProfit = revenueTotal - recognizedCostTotal;
  const estimatedProfitIfAllocated = revenueTotal - (recognizedCostTotal + pendingSharedTotal);

  return (
    <div className="bill-financial-view-panel space-y-6" data-testid="bill-financial-view-panel">
      {/* Pending Shared Cost Warning Banner */}
      {pendingSharedCosts.length > 0 && (
        <div
          className="alert alert-warning border-l-4 border-amber-500 bg-amber-50 p-4 text-amber-900 rounded-md shadow-sm"
          role="alert"
          data-testid="pending-shared-warning"
        >
          <div className="flex items-start gap-3">
            <span className="text-xl font-bold text-amber-600">⚠️</span>
            <div>
              <h4 className="font-semibold text-amber-950">
                Chi phí dùng chung chờ phân bổ trên chặng/lô liên quan
              </h4>
              <p className="text-sm mt-1">
                Có <strong>{pendingSharedCosts.length}</strong> khoản chi phí dùng chung (tổng{" "}
                <strong>{formatMoney(pendingSharedTotal, reportingCurrency)}</strong>) trên các chặng/lô liên quan
                đang chờ phân bổ — <em>Lợi nhuận hiện tại chưa phản ánh đủ chi phí thực tế.</em>
              </p>
              <p className="text-xs text-amber-700 mt-1 italic">
                * Nguyên tắc kế toán kiểm soát (BR-20, Zero Duplicate Economic Cost): Chi phí dùng chung chờ phân bổ
                chưa được tính gộp vào chi phí chính thức của Bill để tránh nhân đôi chi phí kinh tế.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Summary KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="p-4 bg-white border rounded-lg shadow-sm">
          <div className="text-xs uppercase text-gray-500 font-medium">Doanh thu</div>
          <div className="text-xl font-bold text-emerald-700 mt-1">
            {formatMoney(revenueTotal, reportingCurrency)}
          </div>
          <div className="text-xs text-gray-400 mt-1">{revenues.length} khoản mục</div>
        </div>

        <div className="p-4 bg-white border rounded-lg shadow-sm">
          <div className="text-xs uppercase text-gray-500 font-medium">Chi phí trực tiếp</div>
          <div className="text-xl font-bold text-gray-900 mt-1">
            {formatMoney(directTotal, reportingCurrency)}
          </div>
          <div className="text-xs text-gray-400 mt-1">{directCosts.length} khoản trực tiếp</div>
        </div>

        <div className="p-4 bg-white border rounded-lg shadow-sm">
          <div className="text-xs uppercase text-gray-500 font-medium">Chi phí phân bổ đã xác nhận</div>
          <div className="text-xl font-bold text-indigo-700 mt-1">
            {formatMoney(allocatedTotal, reportingCurrency)}
          </div>
          <div className="text-xs text-gray-400 mt-1">{allocatedCosts.length} dòng đã chốt</div>
        </div>

        <div className="p-4 bg-white border rounded-lg shadow-sm">
          <div className="text-xs uppercase text-gray-500 font-medium">Lợi nhuận hiện tại</div>
          <div
            className={`text-xl font-bold mt-1 ${
              currentProfit >= 0 ? "text-emerald-600" : "text-rose-600"
            }`}
          >
            {formatMoney(currentProfit, reportingCurrency)}
          </div>
          {pendingSharedCosts.length > 0 && (
            <div className="text-xs text-amber-600 mt-1">
              Tạm tính sau phân bổ: {formatMoney(estimatedProfitIfAllocated, reportingCurrency)}
            </div>
          )}
        </div>
      </div>

      {/* Category 1: Chi phí trực tiếp */}
      <section className="bg-white border rounded-lg p-5 shadow-sm">
        <div className="flex justify-between items-center mb-4">
          <div>
            <h3 className="font-semibold text-gray-900 text-base">
              1. Chi phí trực tiếp (Direct Costs)
            </h3>
            <p className="text-xs text-gray-500">
              Chi phí phát sinh trực tiếp gắn liền với Bill {billNo}.
            </p>
          </div>
          <span className="text-sm font-semibold text-gray-700">
            Tổng: {formatMoney(directTotal, reportingCurrency)}
          </span>
        </div>

        {directCosts.length === 0 ? (
          <p className="text-sm text-gray-400 italic py-2">Chưa có chi phí trực tiếp.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="bg-gray-50 text-gray-600 text-xs uppercase">
                <tr>
                  <th className="py-2 px-3">Mã chi phí</th>
                  <th className="py-2 px-3">Tên khoản mục</th>
                  <th className="py-2 px-3">Trạng thái (Maturity)</th>
                  <th className="py-2 px-3 text-right">Số tiền</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {directCosts.map((c) => (
                  <tr key={c.id}>
                    <td className="py-2 px-3 font-mono text-xs">{c.costNo || c.id.slice(0, 8)}</td>
                    <td className="py-2 px-3">{c.name}</td>
                    <td className="py-2 px-3">
                      <span className="inline-block px-2 py-0.5 text-xs rounded bg-gray-100 text-gray-700">
                        {c.maturity}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-right font-medium">
                      {formatMoney(c.amount, c.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Category 2: Chi phí phân bổ đã xác nhận */}
      <section className="bg-white border rounded-lg p-5 shadow-sm">
        <div className="flex justify-between items-center mb-4">
          <div>
            <h3 className="font-semibold text-gray-900 text-base">
              2. Chi phí phân bổ đã xác nhận (Finalized Allocated Costs)
            </h3>
            <p className="text-xs text-gray-500">
              Chi phí dùng chung từ chặng/lô đã được chốt phân bổ chính thức vào Bill này.
            </p>
          </div>
          <span className="text-sm font-semibold text-indigo-700">
            Tổng: {formatMoney(allocatedTotal, reportingCurrency)}
          </span>
        </div>

        {allocatedCosts.length === 0 ? (
          <p className="text-sm text-gray-400 italic py-2">Chưa có chi phí phân bổ đã xác nhận.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="bg-indigo-50/50 text-gray-600 text-xs uppercase">
                <tr>
                  <th className="py-2 px-3">Mã phân bổ</th>
                  <th className="py-2 px-3">Nguồn phân bổ</th>
                  <th className="py-2 px-3">Khoản mục</th>
                  <th className="py-2 px-3">Trạng thái</th>
                  <th className="py-2 px-3 text-right">Số tiền phân bổ</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {allocatedCosts.map((c) => (
                  <tr key={c.id}>
                    <td className="py-2 px-3 font-mono text-xs">{c.costNo || c.id.slice(0, 8)}</td>
                    <td className="py-2 px-3 text-xs text-gray-600">
                      {c.sourceType || "LEG"}: {c.sourceRef || "—"}
                    </td>
                    <td className="py-2 px-3">{c.name}</td>
                    <td className="py-2 px-3">
                      <span className="inline-block px-2 py-0.5 text-xs rounded bg-indigo-100 text-indigo-700">
                        {c.maturity}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-right font-medium text-indigo-900">
                      {formatMoney(c.amount, c.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Category 3: Chi phí dùng chung chờ phân bổ */}
      <section className="bg-amber-50/30 border border-amber-200 rounded-lg p-5 shadow-sm">
        <div className="flex justify-between items-center mb-4">
          <div>
            <h3 className="font-semibold text-amber-950 text-base">
              3. Chi phí dùng chung chờ phân bổ (Pending Shared Costs)
            </h3>
            <p className="text-xs text-amber-800">
              Chi phí dùng chung đang treo trên các chặng/lô có liên quan tới Bill này nhưng chưa chốt phân bổ.
            </p>
          </div>
          <span className="text-sm font-semibold text-amber-800">
            Ước tính treo: {formatMoney(pendingSharedTotal, reportingCurrency)}
          </span>
        </div>

        {pendingSharedCosts.length === 0 ? (
          <p className="text-sm text-gray-400 italic py-2">
            Không có chi phí dùng chung nào đang chờ phân bổ.
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="bg-amber-100/60 text-amber-900 text-xs uppercase">
                <tr>
                  <th className="py-2 px-3">Mã chi phí nguồn</th>
                  <th className="py-2 px-3">Đối tượng mang phí</th>
                  <th className="py-2 px-3">Khoản mục chi phí</th>
                  <th className="py-2 px-3">Trạng thái</th>
                  <th className="py-2 px-3 text-right">Tổng chi phí chặng</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-amber-200/40">
                {pendingSharedCosts.map((c) => (
                  <tr key={c.id}>
                    <td className="py-2 px-3 font-mono text-xs">{c.costNo || c.id.slice(0, 8)}</td>
                    <td className="py-2 px-3 text-xs text-amber-900">
                      {c.sourceType || "LEG"}: {c.sourceRef || "—"}
                    </td>
                    <td className="py-2 px-3">{c.name}</td>
                    <td className="py-2 px-3">
                      <span className="inline-block px-2 py-0.5 text-xs rounded bg-amber-200 text-amber-900 font-medium">
                        CHỜ PHÂN BỔ
                      </span>
                    </td>
                    <td className="py-2 px-3 text-right font-medium text-amber-950">
                      {formatMoney(c.amount, c.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

