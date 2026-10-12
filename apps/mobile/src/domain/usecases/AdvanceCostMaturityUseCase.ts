import { CostItemDto } from "@lcms/shared";
import { AdvanceCostMaturityInput, CostToleranceValidationResult } from "../entities/Cost";
import { ICostRepository } from "../repositories/ICostRepository";
import { CreateCostUseCase } from "./CreateCostUseCase";

export class AdvanceCostMaturityUseCase {
  constructor(
    private readonly costRepository: ICostRepository,
    private readonly createCostUseCase: CreateCostUseCase
  ) {}

  /**
   * Kiểm tra tính hợp lệ của việc chuyển đổi vòng đời tài chính:
   * Expected -> Confirmed -> Actual
   */
  public validateTransition(
    currentCost: CostItemDto,
    action: "confirm" | "actualize",
    targetAmount: number
  ): { isValid: boolean; errorVi?: string; toleranceResult?: CostToleranceValidationResult } {
    if (isNaN(targetAmount) || targetAmount <= 0) {
      return {
        isValid: false,
        errorVi: "Số tiền chuyển đổi trạng thái phải lớn hơn 0.",
      };
    }

    if (currentCost.financialMaturity === "actual") {
      return {
        isValid: false,
        errorVi: "Khoản chi phí đã ở trạng thái Thực tế (Actual), không thể chuyển tiếp. Hãy sử dụng tính năng Điều chỉnh (Adjustment).",
      };
    }

    if (action === "confirm" && currentCost.financialMaturity !== "expected") {
      return {
        isValid: false,
        errorVi: "Chỉ chi phí ở trạng thái Dự kiến (Expected) mới có thể chuyển sang Đã xác nhận (Confirmed).",
      };
    }

    if (action === "actualize" && currentCost.financialMaturity !== "confirmed") {
      return {
        isValid: false,
        errorVi: "Chỉ chi phí ở trạng thái Đã xác nhận (Confirmed) mới có thể chốt sang Thực tế (Actual).",
      };
    }

    const baseline =
      action === "confirm"
        ? currentCost.expectedAmount
        : currentCost.confirmedAmount ?? currentCost.amount;

    const tolerance = this.createCostUseCase.calculateMaturityTolerance(baseline, targetAmount);

    return {
      isValid: true,
      toleranceResult: tolerance,
    };
  }

  public async execute(input: AdvanceCostMaturityInput, currentCost: CostItemDto): Promise<void> {
    const val = this.validateTransition(currentCost, input.action, input.targetAmount);
    if (!val.isValid) {
      throw new Error(val.errorVi ?? "Chuyển trạng thái chi phí không hợp lệ.");
    }

    await this.costRepository.advanceMaturity(input);
  }
}
