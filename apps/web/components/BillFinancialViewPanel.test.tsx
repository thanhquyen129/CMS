import { render, screen, cleanup } from "@testing-library/react";
import { describe, it, expect, afterEach } from "vitest";
import { BillFinancialViewPanel, type CostItem, type RevenueItem } from "./BillFinancialViewPanel";

describe("BillFinancialViewPanel", () => {
  afterEach(() => {
    cleanup();
  });

  const directCosts: CostItem[] = [
    {
      id: "cost-1",
      costNo: "C-001",
      category: "DIRECT",
      name: "Cước vận chuyển chính",
      amount: 1000000,
      currency: "VND",
      maturity: "CONFIRMED",
    },
  ];

  const allocatedCosts: CostItem[] = [
    {
      id: "cost-2",
      costNo: "C-ALLOC-01",
      category: "ALLOCATED",
      name: "Phí xếp dỡ chặng 1 phân bổ",
      amount: 300000,
      currency: "VND",
      maturity: "ACTUAL",
      sourceType: "LEG",
      sourceRef: "LEG-01",
    },
  ];

  const pendingSharedCosts: CostItem[] = [
    {
      id: "cost-3",
      costNo: "C-SHARED-01",
      category: "PENDING_SHARED",
      name: "Phí lưu kho dùng chung",
      amount: 500000,
      currency: "VND",
      maturity: "EXPECTED",
      sourceType: "LEG",
      sourceRef: "LEG-02",
    },
  ];

  const revenues: RevenueItem[] = [
    {
      id: "rev-1",
      revenueNo: "R-001",
      name: "Cước thu khách hàng",
      amount: 2500000,
      currency: "VND",
      maturity: "CONFIRMED",
    },
  ];

  it("displays 3 distinct categories: Direct Costs, Finalized Allocated Costs, and Pending Shared Costs", () => {
    render(
      <BillFinancialViewPanel
        billId="bill-01"
        billNo="BILL-2026-001"
        directCosts={directCosts}
        allocatedCosts={allocatedCosts}
        pendingSharedCosts={pendingSharedCosts}
        revenues={revenues}
      />
    );

    expect(screen.getByText(/1\. Chi phí trực tiếp/i)).toBeTruthy();
    expect(screen.getByText(/2\. Chi phí phân bổ đã xác nhận/i)).toBeTruthy();
    expect(screen.getByText(/3\. Chi phí dùng chung chờ phân bổ/i)).toBeTruthy();
  });

  it("displays warning banner with exact wording when pending shared costs exist", () => {
    render(
      <BillFinancialViewPanel
        billId="bill-01"
        billNo="BILL-2026-001"
        directCosts={directCosts}
        allocatedCosts={allocatedCosts}
        pendingSharedCosts={pendingSharedCosts}
        revenues={revenues}
      />
    );

    const banner = screen.getByTestId("pending-shared-warning");
    expect(banner).toBeTruthy();
    expect(banner.textContent).toContain("Lợi nhuận hiện tại chưa phản ánh đủ chi phí thực tế");
    expect(banner.textContent).toContain("Zero Duplicate Economic Cost");
  });

  it("hides warning banner when there are no pending shared costs", () => {
    render(
      <BillFinancialViewPanel
        billId="bill-01"
        billNo="BILL-2026-001"
        directCosts={directCosts}
        allocatedCosts={allocatedCosts}
        pendingSharedCosts={[]}
        revenues={revenues}
      />
    );

    expect(screen.queryByTestId("pending-shared-warning")).toBeNull();
  });

  it("calculates recognized cost strictly from direct + finalized allocated without pending shared", () => {
    render(
      <BillFinancialViewPanel
        billId="bill-01"
        billNo="BILL-2026-001"
        directCosts={directCosts}
        allocatedCosts={allocatedCosts}
        pendingSharedCosts={pendingSharedCosts}
        revenues={revenues}
      />
    );

    // Revenue = 2,500,000
    // Recognized Cost = 1,000,000 + 300,000 = 1,300,000
    // Profit = 2,500,000 - 1,300,000 = 1,200,000 (NOT deducting 500,000 pending)
    expect(screen.getByText("1.200.000 ₫")).toBeTruthy();
    // Preview profit if allocated is shown as 700,000
    expect(screen.getByText(/Tạm tính sau phân bổ: 700.000 ₫/i)).toBeTruthy();
  });
});
