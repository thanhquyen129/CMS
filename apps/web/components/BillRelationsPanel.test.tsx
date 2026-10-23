import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";
import { BillRelationsPanel, type OrderLinkItem, type ShipmentLinkItem } from "./BillRelationsPanel";

describe("BillRelationsPanel", () => {
  afterEach(() => {
    cleanup();
  });

  const mockOrders: OrderLinkItem[] = [
    {
      id: "ord-1",
      orderNo: "ORD-2026-001",
      customerName: "Khách hàng Alpha",
      hasFinancialDependencies: false,
    },
    {
      id: "ord-2",
      orderNo: "ORD-2026-002",
      customerName: "Khách hàng Beta",
      hasFinancialDependencies: true, // locked
    },
  ];

  const mockShipments: ShipmentLinkItem[] = [
    {
      id: "shp-1",
      shipmentNo: "SHP-2026-001",
      hasRatingDependency: false,
    },
  ];

  it("renders linked orders and shipments N:N", () => {
    render(
      <BillRelationsPanel
        billId="bill-001"
        billNo="BILL-2026-001"
        billCustomerId="cust-1"
        billCustomerName="Khách hàng Alpha"
        orders={mockOrders}
        shipments={mockShipments}
      />
    );

    expect(screen.getByText("ORD-2026-001")).toBeTruthy();
    expect(screen.getByText("ORD-2026-002")).toBeTruthy();
    expect(screen.getByText("SHP-2026-001")).toBeTruthy();
  });

  it("warns about customer conflict when order customer does not match bill customer", () => {
    render(
      <BillRelationsPanel
        billId="bill-001"
        billNo="BILL-2026-001"
        billCustomerId="cust-1"
        billCustomerName="Khách hàng Alpha"
        orders={mockOrders}
        shipments={mockShipments}
      />
    );

    const conflictBadges = screen.getAllByText(/Lệch khách hàng/i);
    expect(conflictBadges.length).toBeGreaterThan(0);
  });

  it("disables unlink button when relation has financial dependencies (cost/rating/allocation)", () => {
    render(
      <BillRelationsPanel
        billId="bill-001"
        billNo="BILL-2026-001"
        billCustomerId="cust-1"
        billCustomerName="Khách hàng Alpha"
        orders={mockOrders}
        shipments={mockShipments}
      />
    );

    const unlinkBtns = screen.getAllByTestId("unlink-order-btn");
    // ord-1 has no dependencies: enabled
    expect((unlinkBtns[0] as HTMLButtonElement).disabled).toBe(false);
    // ord-2 has dependencies: disabled
    expect((unlinkBtns[1] as HTMLButtonElement).disabled).toBe(true);
  });

  it("triggers onAddOrder when selecting an available order", () => {
    const handleAddOrder = vi.fn();
    const available = [
      {
        id: "ord-3",
        orderNo: "ORD-2026-003",
        customerName: "Khách hàng Alpha",
      },
    ];

    render(
      <BillRelationsPanel
        billId="bill-001"
        billNo="BILL-2026-001"
        billCustomerId="cust-1"
        billCustomerName="Khách hàng Alpha"
        orders={[]}
        shipments={[]}
        availableOrders={available}
        onAddOrder={handleAddOrder}
      />
    );

    const select = screen.getByRole("combobox", { name: /thêm đơn hàng/i });
    fireEvent.change(select, { target: { value: "ord-3" } });
    const addBtn = screen.getByRole("button", { name: /\+ liên kết đơn/i });
    fireEvent.click(addBtn);

    expect(handleAddOrder).toHaveBeenCalledWith("ord-3");
  });
});
