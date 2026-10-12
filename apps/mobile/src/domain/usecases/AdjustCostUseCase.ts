import { AdjustCostInput } from "../entities/Cost";
import { ICostRepository } from "../repositories/ICostRepository";

export class AdjustCostUseCase {
  constructor(private readonly costRepository: ICostRepository) {}

  public validateInput(input: AdjustCostInput): { isValid: boolean; errorVi?: string } {
    if (isNaN(input.deltaAmount) || input.deltaAmount === 0) {
      return {
        isValid: false,
        errorVi: "Chênh lệch điều chỉnh phải khác 0 (nhập dương nếu tăng, âm nếu giảm).",
      };
    }

    if (!input.reason || input.reason.trim().length < 5) {
      return {
        isValid: false,
        errorVi: "Lý do điều chỉnh kiểm toán bắt buộc nhập tối thiểu 5 ký tự để lưu vết Audit.",
      };
    }

    return { isValid: true };
  }

  public async execute(input: AdjustCostInput): Promise<void> {
    const val = this.validateInput(input);
    if (!val.isValid) {
      throw new Error(val.errorVi ?? "Thông tin điều chỉnh chi phí không hợp lệ.");
    }

    await this.costRepository.adjustCost(input);
  }
}
