import { DriverExpenseAdapter } from "../DriverExpenseAdapter";

describe("DriverExpenseAdapter (Zero-Backend-Change Adapter Pattern)", () => {
  describe("Nhiên liệu Xăng dầu (Fuel)", () => {
    it("phải tự động nhân số lít với đơn giá và định dạng ghi chú chuẩn Kế toán Web", () => {
      const adapted = DriverExpenseAdapter.adaptToCostPayload({
        category: "fuel",
        billId: "bill-driver-999",
        liters: 65.5,
        pricePerLiter: 23500,
        odometerKm: 145200,
        gasStationName: "Petrolimex Số 15 Biên Hòa",
        customNotes: "Đầy bình dầu DO 0.05S",
      });

      expect(adapted.amount).toBe(Math.round(65.5 * 23500)); // 1,539,250 VND
      expect(adapted.costTypeCode).toBe("TRUCKING");
      expect(adapted.currencyCode).toBe("VND");
      expect(adapted.attributionType).toBe("direct");
      expect(adapted.billId).toBe("bill-driver-999");
      expect(adapted.notes).toContain("[XĂNG DẦU]");
      expect(adapted.notes).toContain("Petrolimex Số 15 Biên Hòa");
      expect(adapted.notes).toContain("65.5 L");
      expect(adapted.notes).toContain("23,500 đ/L");
      expect(adapted.notes).toContain("ODO: 145,200 km");
      expect(adapted.notes).toContain("Đầy bình dầu DO 0.05S");
    });

    it("phải ném ngoại lệ khi số lít hoặc đơn giá <= 0", () => {
      expect(() =>
        DriverExpenseAdapter.adaptToCostPayload({
          category: "fuel",
          billId: "bill-1",
          liters: 0,
          pricePerLiter: 23000,
        })
      ).toThrow("Số lít nhiên liệu phải lớn hơn 0.");

      expect(() =>
        DriverExpenseAdapter.adaptToCostPayload({
          category: "fuel",
          billId: "bill-1",
          liters: 50,
          pricePerLiter: -100,
        })
      ).toThrow("Đơn giá xăng dầu phải lớn hơn 0.");
    });
  });

  describe("Vé cầu đường BOT (Toll BOT)", () => {
    it("phải ánh xạ chính xác vé BOT sang nhóm TRUCKING kèm tên trạm", () => {
      const adapted = DriverExpenseAdapter.adaptToCostPayload({
        category: "toll_bot",
        billId: "bill-driver-888",
        amount: 140000,
        stationOrShopName: "Trạm BOT Long Thành - Dầu Giây",
        odometerKm: 145050,
      });

      expect(adapted.amount).toBe(140000);
      expect(adapted.costTypeCode).toBe("TRUCKING");
      expect(adapted.notes).toContain("[VÉ CẦU ĐƯỜNG BOT]");
      expect(adapted.notes).toContain("Trạm BOT Long Thành - Dầu Giây");
      expect(adapted.notes).toContain("ODO: 145,050 km");
    });
  });

  describe("Sửa chữa / Vá vỏ (Repair)", () => {
    it("phải ánh xạ chi phí sửa xe dọc đường vào TRUCKING", () => {
      const adapted = DriverExpenseAdapter.adaptToCostPayload({
        category: "repair",
        billId: "bill-driver-777",
        amount: 250000,
        stationOrShopName: "Tiệm Vá vỏ lưu động Km45",
        customNotes: "Vá lốp phụ sau bên tài",
      });

      expect(adapted.amount).toBe(250000);
      expect(adapted.costTypeCode).toBe("TRUCKING");
      expect(adapted.notes).toContain("[SỬA CHỮA / VÁ VỎ]");
      expect(adapted.notes).toContain("Tiệm Vá vỏ lưu động Km45");
      expect(adapted.notes).toContain("Vá lốp phụ sau bên tài");
    });
  });

  describe("Phụ phí bến bãi / Cảng (Parking & Port)", () => {
    it("phải ánh xạ chính xác sang nhóm chi phí HANDLING", () => {
      const adapted = DriverExpenseAdapter.adaptToCostPayload({
        category: "parking_port",
        billId: "bill-driver-666",
        amount: 120000,
        stationOrShopName: "Cảng Cát Lái - Cổng C",
        customNotes: "Phí lưu bãi chờ bốc dỡ container",
      });

      expect(adapted.amount).toBe(120000);
      expect(adapted.costTypeCode).toBe("HANDLING");
      expect(adapted.notes).toContain("[PHỤ PHÍ BẾN BÃI / CẢNG]");
      expect(adapted.notes).toContain("Cảng Cát Lái - Cổng C");
    });
  });

  describe("Ràng buộc Vận đơn (Bill Validation)", () => {
    it("phải yêu cầu bắt buộc có billId", () => {
      expect(() =>
        DriverExpenseAdapter.adaptToCostPayload({
          category: "toll_bot",
          billId: "",
          amount: 50000,
        })
      ).toThrow("Vui lòng chọn Vận đơn (Bill) liên kết với khoản chi phí này.");
    });
  });
});
