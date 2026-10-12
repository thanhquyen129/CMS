import { AllocateCostInput } from "../entities/Cost";
import { ICostRepository } from "../repositories/ICostRepository";

export class AllocateCostUseCase {
  constructor(private readonly costRepository: ICostRepository) {}

  public validateInput(input: AllocateCostInput): { isValid: boolean; errorVi?: string } {
    if (!input.targetBillIds || input.targetBillIds.length === 0) {
      return {
        isValid: false,
        errorVi: "Vui lòng chọn ít nhất 1 Vận đơn để nhận phân bổ chi phí chung.",
      };
    }

    if (isNaN(input.totalAmount) || input.totalAmount <= 0) {
      return {
        isValid: false,
        errorVi: "Tổng số tiền chi phí phân bổ phải lớn hơn 0.",
      };
    }

    return { isValid: true };
  }

  public async execute(input: AllocateCostInput): Promise<{ allocationId?: string }> {
    const val = this.validateInput(input);
    if (!val.isValid) {
      throw new Error(val.errorVi ?? "Thông tin phân bổ chi phí không hợp lệ.");
    }

    return await this.costRepository.allocateCost(input);
  }
}

