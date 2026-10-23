import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";
import {
  CostAllocationReconciliationPanel,
  type SourceCostInfo,
  type EligibleBill,
} from "./CostAllocationReconciliationPanel";

describe("CostAllocationReconciliationPanel", () => {
  afterEach(() => {
    cleanup();
  });

  const defaultSourceCost: SourceCostInfo = {
    id: "cost-leg-01",
    costNo: "C-LEG-01",
    name: "Cước thuê xe chặng HCM-BD",
    amount: 1000000,
    currency: "VND",
    isDangerousGoods: false,
    objectType: "LEG",
    objectNo: "LEG-001",
    sourceGrossWeight: 100,
    sourceCbm: 2.0,
  };

  const defaultEligibleBills: EligibleBill[] = [
    {
      id: "b1",
      billNo: "BILL-001",
      customerName: "Công ty A",
      isDangerousGoods: false,
      grossWeightKg: 50,
      volumeCbm: 1.0,
      chargeableWeightKg: 50,
      cargoPortionKg: 50,
      hasLineage: true,
    },
    {
      id: "b2",
      billNo: "BILL-002",
      customerName: "Công ty B",
      isDangerousGoods: false,
      grossWeightKg: 50,
      volumeCbm: 1.0,
      chargeableWeightKg: 50,
      cargoPortionKg: 50,
      hasLineage: true,
    },
  ];

  it("renders source cost info and eligible bills properly", () => {
    render(
      <CostAllocationReconciliationPanel
        sourceCost={defaultSourceCost}
        eligibleBills={defaultEligibleBills}
      />
    );

    expect(screen.getByText(/C-LEG-01 — Cước thuê xe chặng HCM-BD/i)).toBeTruthy();
    expect(screen.getByText("BILL-001")).toBeTruthy();
    expect(screen.getByText("BILL-002")).toBeTruthy();
  });

  it("filters out non-DG bills when source cost is DG (UAT-034)", () => {
    const dgSourceCost: SourceCostInfo = {
      ...defaultSourceCost,
      isDangerousGoods: true,
      name: "Phụ phí hàng nguy hiểm DG",
    };

    const mixedBills: EligibleBill[] = [
      {
        id: "b1",
        billNo: "BILL-DG-01",
        customerName: "Hóa chất Á Châu",
        isDangerousGoods: true,
        grossWeightKg: 40,
        volumeCbm: 0.8,
        chargeableWeightKg: 40,
        hasLineage: true,
      },
      {
        id: "b2",
        billNo: "BILL-NORMAL-02",
        customerName: "Dệt May Việt",
        isDangerousGoods: false, // Non-DG
        grossWeightKg: 60,
        volumeCbm: 1.2,
        chargeableWeightKg: 60,
        hasLineage: true,
      },
    ];

    render(
      <CostAllocationReconciliationPanel
        sourceCost={dgSourceCost}
        eligibleBills={mixedBills}
      />
    );

    expect(screen.getByText(/Không hợp lệ với phí DG/i)).toBeTruthy();
    const checkboxNonDg = screen.getByLabelText(/Chọn bill BILL-NORMAL-02/i) as HTMLInputElement;
    expect(checkboxNonDg.disabled).toBe(true);
  });

  it("shows warning when driver sum < source measurement (CASE-D / UAT-028 / BR-16 / R-08)", () => {
    // 40kg + 50kg = 90kg vs Source 100kg
    const underBills: EligibleBill[] = [
      { ...defaultEligibleBills[0], grossWeightKg: 40 },
      { ...defaultEligibleBills[1], grossWeightKg: 50 },
    ];

    render(
      <CostAllocationReconciliationPanel
        sourceCost={defaultSourceCost}
        eligibleBills={underBills}
      />
    );

    const warn = screen.getByTestId("warn-driver-under");
    expect(warn).toBeTruthy();
    expect(warn.textContent).toContain("CASE-D / UAT-028 / BR-16 / R-08");
    expect(warn.textContent).toContain("Cảnh báo chênh lệch 10 kg");
    expect(warn.textContent).toContain("Bảo toàn phân bổ 100% chi phí nguồn trên tổng driver hợp lệ (90 kg)");
  });

  it("hard blocks Finalize when Gross Weight driver sum > source physical weight (BR-17, CASE-F, R-09, UAT-030)", () => {
    // 60kg + 60kg = 120kg vs Source 100kg -> Physical overload block
    const overBills: EligibleBill[] = [
      { ...defaultEligibleBills[0], grossWeightKg: 60 },
      { ...defaultEligibleBills[1], grossWeightKg: 60 },
    ];

    render(
      <CostAllocationReconciliationPanel
        sourceCost={defaultSourceCost}
        eligibleBills={overBills}
      />
    );

    const block = screen.getByTestId("block-physical-overload");
    expect(block).toBeTruthy();
    expect(block.textContent).toContain("BR-17, CASE-F, R-09, UAT-030");

    const finalizeBtn = screen.getByTestId("finalize-allocation-btn") as HTMLButtonElement;
    expect(finalizeBtn.disabled).toBe(true);
  });

  it("hard blocks Finalize when any participating bill is missing lineage context (UAT-042)", () => {
    const missingLineageBills: EligibleBill[] = [
      { ...defaultEligibleBills[0], hasLineage: true },
      { ...defaultEligibleBills[1], hasLineage: false }, // missing lineage!
    ];

    render(
      <CostAllocationReconciliationPanel
        sourceCost={defaultSourceCost}
        eligibleBills={missingLineageBills}
      />
    );

    const block = screen.getByTestId("block-insufficient-context");
    expect(block).toBeTruthy();
    expect(block.textContent).toContain("UAT-042 & BR-18");
    expect(block.textContent).toContain("Chưa đủ dữ liệu đối chiếu");

    const finalizeBtn = screen.getByTestId("finalize-allocation-btn") as HTMLButtonElement;
    expect(finalizeBtn.disabled).toBe(true);
  });

  it("enables and executes Finalize when all checks pass", async () => {
    const handleFinalize = vi.fn().mockResolvedValue(undefined);

    render(
      <CostAllocationReconciliationPanel
        sourceCost={defaultSourceCost}
        eligibleBills={defaultEligibleBills}
        onFinalize={handleFinalize}
      />
    );

    const finalizeBtn = screen.getByTestId("finalize-allocation-btn") as HTMLButtonElement;
    expect(finalizeBtn.disabled).toBe(false);

    fireEvent.click(finalizeBtn);
    expect(handleFinalize).toHaveBeenCalledWith({
      driver: "gross_kg",
      billAllocations: [
        { billId: "b1", amount: 500000, ratio: 0.5 },
        { billId: "b2", amount: 500000, ratio: 0.5 },
      ],
    });
  });
});

