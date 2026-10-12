export type DriverExpenseCategory =
  | "fuel"
  | "toll_bot"
  | "repair"
  | "parking_port";

export interface DriverFuelExpenseInput {
  category: "fuel";
  billId: string;
  liters: number;
  pricePerLiter: number;
  odometerKm?: number | null;
  gasStationName?: string | null;
  receiptImageUri?: string | null;
  customNotes?: string | null;
}

export interface DriverStandardExpenseInput {
  category: "toll_bot" | "repair" | "parking_port";
  billId: string;
  amount: number;
  stationOrShopName?: string | null;
  odometerKm?: number | null;
  receiptImageUri?: string | null;
  customNotes?: string | null;
}

export type DriverExpenseInput =
  | DriverFuelExpenseInput
  | DriverStandardExpenseInput;

export interface AdaptedCostPayload {
  billId: string;
  amount: number;
  currencyCode: "VND";
  costTypeCode: "TRUCKING" | "HANDLING";
  attributionType: "direct";
  sourceType: "driver_mobile_entry";
  notes: string;
  localReceiptUri?: string | null;
}

export class DriverExpenseAdapter {
  /**
   * Chuyển đổi dữ liệu nhập hiện trường của tài xế thành DTO tiêu chuẩn của /api/costs.
   * Áp dụng Adapter Pattern để đảm bảo Zero-Backend-Change.
   */
  public static adaptToCostPayload(input: DriverExpenseInput): AdaptedCostPayload {
    if (!input.billId || input.billId.trim() === "") {
      throw new Error("Vui lòng chọn Vận đơn (Bill) liên kết với khoản chi phí này.");
    }

    let calculatedAmount = 0;
    let costTypeCode: "TRUCKING" | "HANDLING" = "TRUCKING";
    let formattedNotes = "";

    const odoPart = input.odometerKm ? ` | ODO: ${input.odometerKm.toLocaleString()} km` : "";
    const extraNotesPart = input.customNotes && input.customNotes.trim() ? ` | Ghi chú: ${input.customNotes.trim()}` : "";

    switch (input.category) {
      case "fuel": {
        if (!input.liters || input.liters <= 0) {
          throw new Error("Số lít nhiên liệu phải lớn hơn 0.");
        }
        if (!input.pricePerLiter || input.pricePerLiter <= 0) {
          throw new Error("Đơn giá xăng dầu phải lớn hơn 0.");
        }

        calculatedAmount = Math.round(input.liters * input.pricePerLiter);
        costTypeCode = "TRUCKING";

        const stationName = input.gasStationName?.trim() || "Cây xăng dọc đường";
        formattedNotes = `[XĂNG DẦU] Cây xăng: ${stationName} | Số lượng: ${input.liters} L | Đơn giá: ${input.pricePerLiter.toLocaleString()} đ/L${odoPart}${extraNotesPart}`;
        break;
      }

      case "toll_bot": {
        if (!input.amount || input.amount <= 0) {
          throw new Error("Số tiền vé BOT phải lớn hơn 0.");
        }

        calculatedAmount = Math.round(input.amount);
        costTypeCode = "TRUCKING";

        const stationName = input.stationOrShopName?.trim() || "Trạm thu phí BOT";
        formattedNotes = `[VÉ CẦU ĐƯỜNG BOT] Trạm: ${stationName}${odoPart}${extraNotesPart}`;
        break;
      }

      case "repair": {
        if (!input.amount || input.amount <= 0) {
          throw new Error("Số tiền sửa chữa / vá vỏ phải lớn hơn 0.");
        }

        calculatedAmount = Math.round(input.amount);
        costTypeCode = "TRUCKING";

        const shopName = input.stationOrShopName?.trim() || "Tiệm sửa xe / vá vỏ";
        formattedNotes = `[SỬA CHỮA / VÁ VỎ] Tiệm: ${shopName}${odoPart}${extraNotesPart}`;
        break;
      }

      case "parking_port": {
        if (!input.amount || input.amount <= 0) {
          throw new Error("Số tiền bến bãi / phí cảng phải lớn hơn 0.");
        }

        calculatedAmount = Math.round(input.amount);
        costTypeCode = "HANDLING"; // Ánh xạ chuẩn sang nhóm chi phí xếp dỡ, bến bãi

        const portName = input.stationOrShopName?.trim() || "Bến bãi / Cảng / Kho";
        formattedNotes = `[PHỤ PHÍ BẾN BÃI / CẢNG] Điểm: ${portName}${odoPart}${extraNotesPart}`;
        break;
      }

      default: {
        throw new Error("Loại chi phí tài xế không hợp lệ.");
      }
    }

    return {
      billId: input.billId.trim(),
      amount: calculatedAmount,
      currencyCode: "VND",
      costTypeCode,
      attributionType: "direct",
      sourceType: "driver_mobile_entry",
      notes: formattedNotes,
      localReceiptUri: input.receiptImageUri ?? null,
    };
  }
}

