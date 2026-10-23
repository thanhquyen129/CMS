"use client";

import { useMemo, useState } from "react";
import { formatMoney } from "@/lib/money";

export type AllocationDriver =
  | "gross_kg"
  | "cbm"
  | "chargeable_weight"
  | "equal"
  | "disjoint_cargo";

export type SourceCostInfo = {
  id: string;
  costNo: string;
  name: string;
  amount: number;
  currency: string;
  isDangerousGoods?: boolean;
  objectType: "LEG" | "SHIPMENT" | "MOVEMENT";
  objectNo: string;
  sourceGrossWeight?: number;
  sourceCbm?: number;
};

export type EligibleBill = {
  id: string;
  billNo: string;
  customerName: string;
  isDangerousGoods?: boolean;
  grossWeightKg?: number;
  volumeCbm?: number;
  chargeableWeightKg?: number;
  cargoPortionKg?: number;
  hasLineage: boolean; // false triggers INSUFFICIENT_CONTEXT block
};

type Props = {
  sourceCost: SourceCostInfo;
  eligibleBills: EligibleBill[];
  onFinalize?: (allocationData: {
    driver: AllocationDriver;
    billAllocations: { billId: string; amount: number; ratio: number }[];
  }) => Promise<void>;
};

export function CostAllocationReconciliationPanel({
  sourceCost,
  eligibleBills,
  onFinalize,
}: Props) {
  const [driver, setDriver] = useState<AllocationDriver>("gross_kg");
  const [selectedBillIds, setSelectedBillIds] = useState<Set<string>>(() => {
    // If DG cost, only pre-select DG bills
    const initial = new Set<string>();
    eligibleBills.forEach((b) => {
      if (!sourceCost.isDangerousGoods || b.isDangerousGoods) {
        initial.add(b.id);
      }
    });
    return initial;
  });

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);

  // Check DG filtering constraint (UAT-034)
  const isDgCost = Boolean(sourceCost.isDangerousGoods);

  const activeBills = useMemo(() => {
    return eligibleBills.filter((b) => {
      if (isDgCost && !b.isDangerousGoods) return false;
      return selectedBillIds.has(b.id);
    });
  }, [eligibleBills, isDgCost, selectedBillIds]);

  // Compute driver values for active bills
  const billDriverValues = useMemo(() => {
    return activeBills.map((b) => {
      let val = 0;
      if (driver === "gross_kg") {
        val = b.grossWeightKg ?? 0;
      } else if (driver === "cbm") {
        val = b.volumeCbm ?? 0;
      } else if (driver === "chargeable_weight") {
        val = b.chargeableWeightKg ?? 0;
      } else if (driver === "disjoint_cargo") {
        val = b.cargoPortionKg ?? b.grossWeightKg ?? 0;
      } else {
        val = 1; // equal
      }
      return { bill: b, value: val };
    });
  }, [activeBills, driver]);

  const driverSum = useMemo(() => {
    return billDriverValues.reduce((sum, item) => sum + item.value, 0);
  }, [billDriverValues]);

  // Source measurement based on driver
  const sourceMeasurement = useMemo(() => {
    if (driver === "gross_kg" || driver === "disjoint_cargo") {
      return sourceCost.sourceGrossWeight ?? 0;
    }
    if (driver === "cbm") {
      return sourceCost.sourceCbm ?? 0;
    }
    return null;
  }, [driver, sourceCost]);

  // Reconciliation checks
  const isDriverLessThanSource = useMemo(() => {
    if (sourceMeasurement == null || sourceMeasurement <= 0) return false;
    return driverSum < sourceMeasurement;
  }, [driverSum, sourceMeasurement]);

  const isDriverGreaterThanSource = useMemo(() => {
    if (sourceMeasurement == null || sourceMeasurement <= 0) return false;
    return driverSum > sourceMeasurement;
  }, [driverSum, sourceMeasurement]);

  // Hard Block 1: Physical cargo sum > source physical capacity/measurement (BR-18, CASE-A)
  const isPhysicalOverloadBlocked = useMemo(() => {
    if (driver === "gross_kg" && sourceMeasurement != null && sourceMeasurement > 0) {
      return driverSum > sourceMeasurement;
    }
    return false;
  }, [driver, driverSum, sourceMeasurement]);

  // Hard Block 2: Missing cargo portion lineage (UAT-042)
  const missingLineageBills = useMemo(() => {
    return activeBills.filter((b) => !b.hasLineage);
  }, [activeBills]);

  const hasInsufficientContext = missingLineageBills.length > 0;

  const canFinalize =
    !isPhysicalOverloadBlocked &&
    !hasInsufficientContext &&
    activeBills.length >= 2 &&
    driverSum > 0 &&
    !submitting;

  // Compute allocated amounts
  const allocations = useMemo(() => {
    if (driverSum <= 0) return [];
    return billDriverValues.map((item) => {
      const ratio = item.value / driverSum;
      const amount = Math.round(sourceCost.amount * ratio);
      return {
        billId: item.bill.id,
        billNo: item.bill.billNo,
        customerName: item.bill.customerName,
        driverValue: item.value,
        ratio,
        amount,
      };
    });
  }, [billDriverValues, driverSum, sourceCost.amount]);

  async function handleFinalize() {
    if (!canFinalize) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      if (onFinalize) {
        await onFinalize({
          driver,
          billAllocations: allocations.map((a) => ({
            billId: a.billId,
            amount: a.amount,
            ratio: a.ratio,
          })),
        });
      }
      setSubmitSuccess(true);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Có lỗi xảy ra khi chốt phân bổ.";
      setSubmitError(message);
    } finally {
      setSubmitting(false);
    }
  }

  function toggleBill(billId: string) {
    setSelectedBillIds((prev) => {
      const next = new Set(prev);
      if (next.has(billId)) {
        next.delete(billId);
      } else {
        next.add(billId);
      }
      return next;
    });
  }

  return (
    <div
      className="cost-allocation-reconciliation-panel bg-white border rounded-xl p-6 shadow-sm space-y-6"
      data-testid="cost-allocation-reconciliation-panel"
    >
      <div className="border-b pb-4">
        <h2 className="text-xl font-bold text-gray-900">
          Đối chiếu & Phân bổ chi phí dùng chung (Cost Allocation Reconciliation)
        </h2>
        <p className="text-xs text-gray-500 mt-1">
          Hỗ trợ phân bổ đa chặng, loại trừ phí đặc thù (DG) và kiểm soát số đo đối chiếu chống thất thoát (CR-04, CR-05, BR-18..20).
        </p>
      </div>

      {/* Source Cost Card */}
      <div className="bg-slate-50 border border-slate-200 rounded-lg p-4">
        <div className="text-xs uppercase font-semibold text-slate-500 mb-2">Khoản chi phí nguồn (Source Cost)</div>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <div className="text-xs text-slate-500">Mã chi phí / Khoản mục</div>
            <div className="font-semibold text-slate-900">
              {sourceCost.costNo} — {sourceCost.name}
            </div>
          </div>
          <div>
            <div className="text-xs text-slate-500">Đối tượng phát sinh</div>
            <div className="font-semibold text-slate-900">
              {sourceCost.objectType} {sourceCost.objectNo}
            </div>
          </div>
          <div>
            <div className="text-xs text-slate-500">Số đo nguồn</div>
            <div className="font-semibold text-slate-900">
              {sourceCost.sourceGrossWeight != null ? `${sourceCost.sourceGrossWeight} kg` : "—"}{" "}
              {sourceCost.sourceCbm != null ? `· ${sourceCost.sourceCbm} CBM` : ""}
            </div>
          </div>
          <div>
            <div className="text-xs text-slate-500">Số tiền cần phân bổ</div>
            <div className="text-lg font-bold text-blue-700">
              {formatMoney(sourceCost.amount, sourceCost.currency)}
            </div>
          </div>
        </div>
        {isDgCost && (
          <div className="mt-2 inline-flex items-center gap-1.5 px-2.5 py-1 bg-red-100 text-red-800 text-xs font-semibold rounded">
            <span>🔥 Phí Hàng Nguy Hiểm (DG)</span>
            <span className="font-normal">— Chỉ các Bill có đánh dấu DG mới đủ điều kiện nhận phân bổ (UAT-034).</span>
          </div>
        )}
      </div>

      {/* Driver Selector */}
      <div className="space-y-2">
        <label htmlFor="driver-select" className="text-sm font-semibold text-gray-800">
          Tiêu thức phân bổ (Allocation Driver)
        </label>
        <select
          id="driver-select"
          className="w-full md:w-80 border rounded-md p-2 text-sm bg-white"
          value={driver}
          onChange={(e) => setDriver(e.target.value as AllocationDriver)}
        >
          <option value="gross_kg">Trọng lượng vật lý (Gross Weight kg)</option>
          <option value="cbm">Thể tích (CBM)</option>
          <option value="chargeable_weight">Trọng lượng tính cước (Chargeable Weight CW kg)</option>
          <option value="disjoint_cargo">Phần hàng tham gia rời rạc (Disjoint Cargo Split)</option>
          <option value="equal">Chia đều (1/N)</option>
        </select>
      </div>

      {/* Warnings & Hard Blocks */}
      {isPhysicalOverloadBlocked && (
        <div
          className="alert alert-danger bg-red-50 border-l-4 border-red-600 p-4 rounded text-red-900"
          role="alert"
          data-testid="block-physical-overload"
        >
          <h4 className="font-bold text-sm">Chặn phân bổ (BR-18 & CASE-A)</h4>
          <p className="text-sm mt-0.5">
            Tổng Gross Weight của các phần hàng ({driverSum.toFixed(2)} kg) vượt quá số đo vật lý của chặng nguồn ({sourceMeasurement?.toFixed(2)} kg).
            Hệ thống không cho phép dồn số đo vật lý vượt tải trọng thực tế.
          </p>
        </div>
      )}

      {hasInsufficientContext && (
        <div
          className="alert alert-danger bg-red-50 border-l-4 border-red-600 p-4 rounded text-red-900"
          role="alert"
          data-testid="block-insufficient-context"
        >
          <h4 className="font-bold text-sm">Chặn phân bổ (UAT-042 & BR-18)</h4>
          <p className="text-sm mt-0.5">
            Chưa đủ dữ liệu đối chiếu: Thiếu thông tin phần hàng (cargo portion lineage) để đối chiếu đa chặng. Cần bổ sung trước khi chốt phân bổ.
          </p>
          <ul className="list-disc list-inside text-xs mt-1">
            {missingLineageBills.map((b) => (
              <li key={b.id}>
                Bill {b.billNo} ({b.customerName}): Thiếu cargo portion lineage.
              </li>
            ))}
          </ul>
        </div>
      )}

      {!isPhysicalOverloadBlocked && isDriverLessThanSource && (
        <div
          className="alert alert-warning bg-amber-50 border-l-4 border-amber-500 p-4 rounded text-amber-900"
          role="alert"
          data-testid="warn-driver-under"
        >
          <h4 className="font-bold text-sm">Cảnh báo đối chiếu (R-08 Thất thoát chi phí)</h4>
          <p className="text-sm mt-0.5">
            Tổng trọng số đối chiếu ({driverSum.toFixed(2)}) nhỏ hơn số đo chặng nguồn ({sourceMeasurement?.toFixed(2)}).
            Ước tính thất thoát{" "}
            {sourceMeasurement ? (((sourceMeasurement - driverSum) / sourceMeasurement) * 100).toFixed(1) : 0}%
            chi phí chưa được gán vào Bill nào.
          </p>
        </div>
      )}

      {!isPhysicalOverloadBlocked && isDriverGreaterThanSource && (
        <div
          className="alert alert-warning bg-amber-50 border-l-4 border-amber-500 p-4 rounded text-amber-900"
          role="alert"
          data-testid="warn-driver-over"
        >
          <h4 className="font-bold text-sm">Cảnh báo đối chiếu (R-09 Dồn tải)</h4>
          <p className="text-sm mt-0.5">
            Tổng số đo driver ({driverSum.toFixed(2)}) lớn hơn số đo chặng nguồn ({sourceMeasurement?.toFixed(2)}) — hiện tượng dồn tải hoặc đo lường lệch.
          </p>
        </div>
      )}

      {/* Eligible Bills Table */}
      <div className="space-y-3">
        <div className="flex justify-between items-center">
          <h3 className="text-sm font-semibold text-gray-800">
            Danh sách Bill tham gia đối chiếu ({activeBills.length}/{eligibleBills.length})
          </h3>
          <span className="text-xs text-gray-500">
            Tổng driver: <strong>{driverSum.toFixed(2)}</strong>
            {sourceMeasurement != null ? ` / Số đo nguồn: ${sourceMeasurement.toFixed(2)}` : ""}
          </span>
        </div>

        <div className="overflow-x-auto border rounded-lg">
          <table className="w-full text-left text-sm">
            <thead className="bg-gray-50 text-gray-600 text-xs uppercase">
              <tr>
                <th className="py-2.5 px-3 w-10 text-center">Chọn</th>
                <th className="py-2.5 px-3">Mã Bill</th>
                <th className="py-2.5 px-3">Khách hàng</th>
                <th className="py-2.5 px-3">Thuộc tính</th>
                <th className="py-2.5 px-3 text-right">Trọng số driver</th>
                <th className="py-2.5 px-3 text-right">Tỷ lệ (%)</th>
                <th className="py-2.5 px-3 text-right">Tiền phân bổ</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {eligibleBills.map((b) => {
                const isSelected = selectedBillIds.has(b.id);
                const isDgExcluded = isDgCost && !b.isDangerousGoods;
                const alloc = allocations.find((a) => a.billId === b.id);

                return (
                  <tr
                    key={b.id}
                    className={`${isDgExcluded ? "bg-gray-100/50 opacity-60" : isSelected ? "bg-blue-50/20" : ""}`}
                  >
                    <td className="py-2.5 px-3 text-center">
                      <input
                        type="checkbox"
                        checked={isSelected && !isDgExcluded}
                        disabled={isDgExcluded}
                        onChange={() => toggleBill(b.id)}
                        aria-label={`Chọn bill ${b.billNo}`}
                      />
                    </td>
                    <td className="py-2.5 px-3 font-medium text-gray-900">{b.billNo}</td>
                    <td className="py-2.5 px-3 text-gray-600">{b.customerName}</td>
                    <td className="py-2.5 px-3">
                      <div className="flex gap-1 flex-wrap">
                        {b.isDangerousGoods ? (
                          <span className="px-1.5 py-0.5 text-xs bg-red-100 text-red-800 rounded font-semibold">
                            DG
                          </span>
                        ) : (
                          <span className="px-1.5 py-0.5 text-xs bg-gray-100 text-gray-600 rounded">
                            Non-DG
                          </span>
                        )}
                        {!b.hasLineage && (
                          <span className="px-1.5 py-0.5 text-xs bg-amber-100 text-amber-800 rounded">
                            Thiếu lineage
                          </span>
                        )}
                        {isDgExcluded && (
                          <span className="px-1.5 py-0.5 text-xs bg-gray-200 text-gray-700 rounded italic">
                            Không hợp lệ với phí DG
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="py-2.5 px-3 text-right font-mono text-xs">
                      {alloc ? alloc.driverValue.toFixed(2) : "—"}
                    </td>
                    <td className="py-2.5 px-3 text-right font-mono text-xs">
                      {alloc ? `${(alloc.ratio * 100).toFixed(2)}%` : "—"}
                    </td>
                    <td className="py-2.5 px-3 text-right font-semibold text-gray-900">
                      {alloc ? formatMoney(alloc.amount, sourceCost.currency) : "—"}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {submitError && (
        <div className="alert alert-danger bg-red-50 border border-red-200 p-3 rounded text-red-800 text-sm">
          {submitError}
        </div>
      )}

      {submitSuccess && (
        <div className="alert alert-success bg-emerald-50 border border-emerald-200 p-3 rounded text-emerald-800 text-sm">
          Đã chốt phân bổ thành công! Các bản ghi chi phí phân bổ con đã được tạo và liên kết chặt chẽ với bản ghi nguồn.
        </div>
      )}

      {/* Finalize Action */}
      <div className="flex justify-between items-center pt-4 border-t">
        <div className="text-xs text-gray-500">
          * Tuân thủ quy tắc bất biến: Khi chốt phân bổ, bản ghi chi phí nguồn được giữ nguyên gốc (H-003, BR-20),
          tạo {allocations.length} bản ghi chi phí con loại ALLOCATED vào từng Bill tương ứng.
        </div>
        <button
          type="button"
          disabled={!canFinalize}
          onClick={handleFinalize}
          className={`px-5 py-2.5 rounded-lg text-sm font-semibold transition shadow ${
            canFinalize
              ? "bg-blue-600 hover:bg-blue-700 text-white cursor-pointer"
              : "bg-gray-200 text-gray-400 cursor-not-allowed"
          }`}
          data-testid="finalize-allocation-btn"
        >
          {submitting ? "Đang chốt phân bổ..." : "Chốt phân bổ (Finalize)"}
        </button>
      </div>
    </div>
  );
}
