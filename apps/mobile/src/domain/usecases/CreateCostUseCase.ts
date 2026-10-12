import {
  CostCurrencyCode,
  CostToleranceValidationResult,
  CreateCostInput,
} from "../entities/Cost";
import { CreateCostResult, ICostRepository } from "../repositories/ICostRepository";

export const MAX_MOBILE_COST_LIMITS: Record<CostCurrencyCode, number> = {
  VND: 5_000_000_000, // 5 tỷ VND giới hạn trần giao dịch hiện trường
  USD: 250_000,       // 250,000 USD
};

export const DEFAULT_TOLERANCE_PERCENT = 10; // 10% ngưỡng dung sai cảnh báo chênh lệch

export class CreateCostUseCase {
  constructor(private readonly costRepository: ICostRepository) {}

  /**
   * Kiểm tra tính hợp lệ và hạn mức dung sai của số tiền chi phí.
   */
  public validateAmount(amount: number, currency: CostCurrencyCode): CostToleranceValidationResult {
    if (isNaN(amount) || amount <= 0) {
      return {
        isValid: false,
        errorVi: "Số tiền chi phí phải là số dương lớn hơn 0.",
      };
    }

    const ceiling = MAX_MOBILE_COST_LIMITS[currency] ?? Infinity;
    if (amount > ceiling) {
      return {
        isValid: false,
        errorVi: `Số tiền vượt hạn mức tối đa cho phép trên Mobile (${ceiling.toLocaleString()} ${currency}). Vui lòng lập đề nghị trên Webapp.`,
      };
    }

    return {
      isValid: true,
    };
  }

  /**
   * Tính toán độ lệch dung sai giữa số tiền Dự kiến (Expected) và Thực tế/Xác nhận (Confirmed/Actual).
   * Giúp phát hiện sai số tài chính vượt ngưỡng cho phép (Variance control).
   */
  public calculateMaturityTolerance(
    baseAmount: number,
    targetAmount: number,
    tolerancePercent: number = DEFAULT_TOLERANCE_PERCENT
  ): CostToleranceValidationResult {
    if (baseAmount <= 0) {
      return {
        isValid: true,
        variancePercentage: 0,
      };
    }

    const diff = Math.abs(targetAmount - baseAmount);
    const variancePercentage = (diff / baseAmount) * 100;

    if (variancePercentage > tolerancePercent) {
      return {
        isValid: true,
        variancePercentage: Number(variancePercentage.toFixed(2)),
        warningVi: `Chênh lệch ${variancePercentage.toFixed(1)}% vượt ngưỡng dung sai ${tolerancePercent}%. Bút toán sẽ tạo Variance cần duyệt.`,
      };
    }

    return {
      isValid: true,
      variancePercentage: Number(variancePercentage.toFixed(2)),
    };
  }

  /**
   * Kiểm tra toàn diện dữ liệu đầu vào theo quy tắc nghiệp vụ tài chính LCMS.
   */
  public validateInput(input: CreateCostInput): { isValid: boolean; errorVi?: string } {
    const amountVal = this.validateAmount(input.amount, input.currencyCode);
    if (!amountVal.isValid) {
      return { isValid: false, errorVi: amountVal.errorVi };
    }

    if (!input.costTypeCode || input.costTypeCode.trim() === "") {
      return { isValid: false, errorVi: "Vui lòng chọn loại chi phí (Cost Type)." };
    }

    if (input.attributionType === "direct" && (!input.billId || input.billId.trim() === "")) {
      return {
        isValid: false,
        errorVi: "Chi phí trực tiếp (Direct) bắt buộc phải gắn kết với Vận đơn (Bill).",
      };
    }

    return { isValid: true };
  }

  /**
   * Thực thi UseCase tạo mới chi phí (Expected Cost).
   */
  public async execute(
    input: CreateCostInput,
    isOffline = false
  ): Promise<CreateCostResult> {
    const validation = this.validateInput(input);
    if (!validation.isValid) {
      throw new Error(validation.errorVi ?? "Dữ liệu chi phí không hợp lệ.");
    }

    return await this.costRepository.createCost(input, isOffline);
  }
}

