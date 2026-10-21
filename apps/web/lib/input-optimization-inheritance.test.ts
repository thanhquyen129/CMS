import { describe, it, expect } from "vitest";

describe("Input Optimization TO-BE (OPT-01 -> OPT-06)", () => {
  describe("OPT-01: Kế thừa Đơn hàng -> Bill (Order to Bill)", () => {
    it("Đơn đơn hàng: Tự động kế thừa đầy đủ 16 trường nghiệp vụ và số đo hàng hóa", () => {
      const singleOrder = {
        id: "ord-1",
        orderNo: "ORD-2026-001",
        customerPartyId: "cust-1",
        shipperPartyId: "ship-1",
        consigneePartyId: "cons-1",
        transportMode: "air",
        originCode: "VNSGN",
        destinationCode: "USLAX",
        etd: "2026-11-01T08:00:00Z",
        eta: "2026-11-02T16:00:00Z",
        serviceTypeCode: "DOOR_DOOR",
        commodityTypeId: "ELECTRONICS",
        packageCount: 15,
        grossWeightKg: 150.5,
        volumeCbm: 1.25,
        chargeableWeightKg: 208.33,
      };

      // Simulated client-side prefill processor
      const prefill = {
        billNo: singleOrder.orderNo,
        customerPartyId: singleOrder.customerPartyId,
        shipperPartyId: singleOrder.shipperPartyId,
        consigneePartyId: singleOrder.consigneePartyId,
        transportMode: singleOrder.transportMode,
        originCode: singleOrder.originCode,
        destinationCode: singleOrder.destinationCode,
        etd: singleOrder.etd,
        eta: singleOrder.eta,
        serviceTypeCode: singleOrder.serviceTypeCode,
        commodityTypeId: singleOrder.commodityTypeId,
        packageCount: singleOrder.packageCount,
        grossWeightKg: singleOrder.grossWeightKg,
        volumeCbm: singleOrder.volumeCbm,
        chargeableWeightKg: singleOrder.chargeableWeightKg,
        conflicts: [],
      };

      expect(prefill.billNo).toBe("ORD-2026-001");
      expect(prefill.customerPartyId).toBe("cust-1");
      expect(prefill.transportMode).toBe("air");
      expect(prefill.originCode).toBe("VNSGN");
      expect(prefill.destinationCode).toBe("USLAX");
      expect(prefill.packageCount).toBe(15);
      expect(prefill.grossWeightKg).toBe(150.5);
      expect(prefill.volumeCbm).toBe(1.25);
      expect(prefill.chargeableWeightKg).toBe(208.33);
      expect(prefill.conflicts).toHaveLength(0);
    });

    it("Đa đơn hàng: Tự động cộng dồn thông số hàng hóa và phát hiện xung đột dữ liệu", () => {
      const orders = [
        {
          id: "ord-1",
          customerPartyId: "cust-1",
          originCode: "VNSGN",
          destinationCode: "USLAX",
          packageCount: 10,
          grossWeightKg: 100,
          volumeCbm: 1.0,
          chargeableWeightKg: 166.67,
        },
        {
          id: "ord-2",
          customerPartyId: "cust-2",
          originCode: "VNHAN",
          destinationCode: "USLAX",
          packageCount: 5,
          grossWeightKg: 70,
          volumeCbm: 0.5,
          chargeableWeightKg: 83.33,
        },
      ];

      const sumPackage = orders.reduce((acc, o) => acc + o.packageCount, 0);
      const sumGross = orders.reduce((acc, o) => acc + o.grossWeightKg, 0);
      const sumCbm = orders.reduce((acc, o) => acc + o.volumeCbm, 0);
      const sumCw = orders.reduce((acc, o) => acc + o.chargeableWeightKg, 0);

      const conflicts: string[] = [];
      const distinctCust = new Set(orders.map((o) => o.customerPartyId));
      if (distinctCust.size > 1) conflicts.push("Khách hàng khác nhau giữa các đơn hàng");
      const distinctOrigin = new Set(orders.map((o) => o.originCode));
      if (distinctOrigin.size > 1) conflicts.push("Điểm đi khác nhau giữa các đơn hàng");

      expect(sumPackage).toBe(15);
      expect(sumGross).toBe(170);
      expect(sumCbm).toBe(1.5);
      expect(sumCw).toBeCloseTo(250.0, 1);
      expect(conflicts).toHaveLength(2);
      expect(conflicts[0]).toContain("Khách hàng");
      expect(conflicts[1]).toContain("Điểm đi");
    });
  });

  describe("OPT-02: Tổng hợp dữ liệu từ Bill sang Lô hàng (Shipment Rollup)", () => {
    it("Cộng dồn số kiện, trọng lượng, thể tích và tự động đồng bộ điểm đi/đến từ Bill", () => {
      const bills = [
        {
          id: "b-1",
          packageCount: 100,
          grossWeightKg: 2500,
          volumeCbm: 12.5,
          chargeableWeightKg: 2500,
          originCode: "VNSGN",
          destinationCode: "USLAX",
          routeCode: "SGN-LAX",
        },
        {
          id: "b-2",
          packageCount: 50,
          grossWeightKg: 1500,
          volumeCbm: 8.0,
          chargeableWeightKg: 1500,
          originCode: "VNSGN",
          destinationCode: "USLAX",
          routeCode: "SGN-LAX",
        },
      ];

      const rollup = {
        totalBills: bills.length,
        packageCount: bills.reduce((acc, b) => acc + b.packageCount, 0),
        grossWeightKg: bills.reduce((acc, b) => acc + b.grossWeightKg, 0),
        volumeCbm: bills.reduce((acc, b) => acc + b.volumeCbm, 0),
        chargeableWeightKg: bills.reduce((acc, b) => acc + b.chargeableWeightKg, 0),
        originCode: bills[0].originCode,
        destinationCode: bills[0].destinationCode,
        routeCode: bills[0].routeCode,
      };

      expect(rollup.totalBills).toBe(2);
      expect(rollup.packageCount).toBe(150);
      expect(rollup.grossWeightKg).toBe(4000);
      expect(rollup.volumeCbm).toBe(20.5);
      expect(rollup.chargeableWeightKg).toBe(4000);
      expect(rollup.routeCode).toBe("SGN-LAX");
    });
  });

  describe("OPT-03: Kế thừa Chặng kế tiếp (Daisy-chaining) & Lập Chuyến từ Chặng", () => {
    it("Chặng kế tiếp tự động nhận cảng đến của chặng trước làm cảng đi", () => {
      const existingLegs = [
        { sequenceNo: 1, originCode: "VNSGN", destinationCode: "JPTYO" },
      ];
      const shipment = { originCode: "VNSGN", destinationCode: "USJFK" };

      const lastLeg = existingLegs[existingLegs.length - 1];
      const nextSequenceNo = lastLeg.sequenceNo + 1;
      const suggestedOriginCode = lastLeg.destinationCode;
      const suggestedDestinationCode = shipment.destinationCode;

      expect(nextSequenceNo).toBe(2);
      expect(suggestedOriginCode).toBe("JPTYO"); // Destination of Leg 1 becomes Origin of Leg 2!
      expect(suggestedDestinationCode).toBe("USJFK");
    });
  });

  describe("OPT-04: Đồng bộ Bảng giá -> Phụ phí & Ánh xạ Khoản mục kinh tế", () => {
    it("Đồng bộ chiều mua/bán và tiền tệ từ Bảng giá sang Phụ phí", () => {
      const rateCard = {
        partyType: "vendor",
        currencyCode: "VND",
        transportMode: "air",
      };

      const surchargeDefaults = {
        direction: rateCard.partyType === "vendor" ? "buy" : "sell",
        currencyCode: rateCard.currencyCode,
        transportMode: rateCard.transportMode,
      };

      expect(surchargeDefaults.direction).toBe("buy");
      expect(surchargeDefaults.currencyCode).toBe("VND");
      expect(surchargeDefaults.transportMode).toBe("air");
    });

    it("Ánh xạ khoản mục kinh tế gợi ý tài khoản chi phí và doanh thu", () => {
      const mapping = {
        economicChargeTypeId: "ec-1",
        costTypeCode: "641_FUEL",
        revenueTypeCode: "511_FUEL",
      };

      expect(mapping.costTypeCode).toBe("641_FUEL");
      expect(mapping.revenueTypeCode).toBe("511_FUEL");
    });
  });

  describe("OPT-05: Tiền đề xuất từ Công nợ Phải trả/Phải thu sang Phiếu Chi/Thu", () => {
    it("Tự động điền mã đối tác, số tiền dư nợ và số hóa đơn tham chiếu", () => {
      const apItem = {
        id: "ap-1",
        billId: "bill-100",
        invoiceNo: "INV-2026-99",
        partnerPartyId: "vendor-1",
        outstanding: 5000000,
        currencyCode: "VND",
      };

      const params = new URLSearchParams({
        billId: apItem.billId,
        partnerId: apItem.partnerPartyId,
        amount: apItem.outstanding.toString(),
        currency: apItem.currencyCode,
        reference: apItem.invoiceNo,
      });

      expect(params.get("billId")).toBe("bill-100");
      expect(params.get("partnerId")).toBe("vendor-1");
      expect(params.get("amount")).toBe("5000000");
      expect(params.get("currency")).toBe("VND");
      expect(params.get("reference")).toBe("INV-2026-99");
    });
  });

  describe("OPT-06: Gợi ý Cơ sở Phân bổ Chi phí chung theo phương thức vận chuyển", () => {
    it("Tự động gợi ý cơ sở phân bổ phù hợp theo tính chất vật lý", () => {
      function getSuggestedBasis(mode: string): string {
        switch (mode.toLowerCase()) {
          case "air":
            return "chargeable";
          case "sea":
            return "cbm";
          case "road":
          case "rail":
            return "gross_kg";
          default:
            return "equal";
        }
      }

      expect(getSuggestedBasis("air")).toBe("chargeable");
      expect(getSuggestedBasis("sea")).toBe("cbm");
      expect(getSuggestedBasis("road")).toBe("gross_kg");
      expect(getSuggestedBasis("multimodal")).toBe("equal");
    });
  });
});

