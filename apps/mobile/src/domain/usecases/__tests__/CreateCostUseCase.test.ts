import { BillListItemDto, CostItemDto } from "@lcms/shared";
import {
  AdjustCostInput,
  AdvanceCostMaturityInput,
  AllocateCostInput,
  CostFilterParams,
  CreateCostInput,
} from "../../entities/Cost";
import { CreateCostResult, ICostRepository } from "../../repositories/ICostRepository";
import { CreateCostUseCase, MAX_MOBILE_COST_LIMITS } from "../CreateCostUseCase";

// Mock implementation của ICostRepository
class MockCostRepository implements ICostRepository {
  public createdCosts: Array<{ input: CreateCostInput; isOffline?: boolean }> = [];

  public async getCosts(filter?: CostFilterParams): Promise<CostItemDto[]> {
    return [];
  }

  public async getBills(): Promise<BillListItemDto[]> {
    return [];
  }

  public async createCost(
    input: CreateCostInput,
    isOffline = false
  ): Promise<CreateCostResult> {
    this.createdCosts.push({ input, isOffline });
    return {
      success: true,
      isOffline,
      id: isOffline ? undefined : "cost-mock-123",
      messageVi: isOffline ? "Đã lưu offline" : "Tạo thành công",
    };
  }

  public async advanceMaturity(input: AdvanceCostMaturityInput): Promise<void> {}
  public async adjustCost(input: AdjustCostInput): Promise<void> {}
  public async allocateCost(input: AllocateCostInput): Promise<{ allocationId?: string }> {
    return { allocationId: "alloc-mock-123" };
  }
}

describe("CreateCostUseCase (Clean Architecture Domain Unit Tests)", () => {
  let mockRepo: MockCostRepository;
  let useCase: CreateCostUseCase;

  beforeEach(() => {
    mockRepo = new MockCostRepository();
    useCase = new CreateCostUseCase(mockRepo);
  });

  describe("validateAmount", () => {
    it("phải từ chối số tiền <= 0 hoặc NaN", () => {
      expect(useCase.validateAmount(0, "VND").isValid).toBe(false);
      expect(useCase.validateAmount(-50000, "VND").isValid).toBe(false);
      expect(useCase.validateAmount(NaN, "USD").isValid).toBe(false);
    });

    it("phải chấp nhận số tiền hợp lệ nằm trong hạn mức", () => {
      const res = useCase.validateAmount(1500000, "VND");
      expect(res.isValid).toBe(true);
      expect(res.errorVi).toBeUndefined();
    });

    it("phải từ chối số tiền vượt hạn mức tối đa cho phép trên Mobile", () => {
      const overLimitVnd = MAX_MOBILE_COST_LIMITS.VND + 1;
      const resVnd = useCase.validateAmount(overLimitVnd, "VND");
      expect(resVnd.isValid).toBe(false);
      expect(resVnd.errorVi).toContain("vượt hạn mức tối đa");

      const overLimitUsd = MAX_MOBILE_COST_LIMITS.USD + 1;
      const resUsd = useCase.validateAmount(overLimitUsd, "USD");
      expect(resUsd.isValid).toBe(false);
      expect(resUsd.errorVi).toContain("vượt hạn mức tối đa");
    });
  });

  describe("calculateMaturityTolerance", () => {
    it("phải trả về variancePercentage = 0 khi không có chênh lệch", () => {
      const res = useCase.calculateMaturityTolerance(1000, 1000, 10);
      expect(res.isValid).toBe(true);
      expect(res.variancePercentage).toBe(0);
      expect(res.warningVi).toBeUndefined();
    });

    it("phải chấp nhận chênh lệch nằm trong ngưỡng dung sai 10%", () => {
      // Chênh lệch 5% (1000 -> 1050)
      const res = useCase.calculateMaturityTolerance(1000, 1050, 10);
      expect(res.isValid).toBe(true);
      expect(res.variancePercentage).toBe(5);
      expect(res.warningVi).toBeUndefined();
    });

    it("phải đưa ra cảnh báo Variance khi chênh lệch vượt quá 10%", () => {
      // Chênh lệch 25% (1000 -> 1250)
      const res = useCase.calculateMaturityTolerance(1000, 1250, 10);
      expect(res.isValid).toBe(true);
      expect(res.variancePercentage).toBe(25);
      expect(res.warningVi).toContain("vượt ngưỡng dung sai");
    });
  });

  describe("validateInput & execute", () => {
    it("phải báo lỗi khi chi phí trực tiếp (Direct) mà không có billId", async () => {
      const invalidInput: CreateCostInput = {
        attributionType: "direct",
        billId: "",
        amount: 2000000,
        currencyCode: "VND",
        costTypeCode: "TRUCKING",
      };

      await expect(useCase.execute(invalidInput)).rejects.toThrow(
        "Chi phí trực tiếp (Direct) bắt buộc phải gắn kết với Vận đơn (Bill)."
      );
      expect(mockRepo.createdCosts).toHaveLength(0);
    });

    it("phải tạo chi phí trực tiếp thành công khi có đủ billId", async () => {
      const validInput: CreateCostInput = {
        attributionType: "direct",
        billId: "bill-001",
        amount: 3500000,
        currencyCode: "VND",
        costTypeCode: "FREIGHT",
      };

      const result = await useCase.execute(validInput, false);
      expect(result.success).toBe(true);
      expect(result.isOffline).toBe(false);
      expect(result.id).toBe("cost-mock-123");
      expect(mockRepo.createdCosts).toHaveLength(1);
      expect(mockRepo.createdCosts[0].input.billId).toBe("bill-001");
    });

    it("phải tạo chi phí dùng chung (Shared) thành công dù không có billId", async () => {
      const sharedInput: CreateCostInput = {
        attributionType: "shared",
        billId: null,
        amount: 500,
        currencyCode: "USD",
        costTypeCode: "CUSTOMS",
      };

      const result = await useCase.execute(sharedInput, false);
      expect(result.success).toBe(true);
      expect(result.isOffline).toBe(false);
      expect(mockRepo.createdCosts).toHaveLength(1);
      expect(mockRepo.createdCosts[0].input.attributionType).toBe("shared");
    });

    it("phải hỗ trợ lưu ngoại tuyến (Offline Outbox) thông qua cờ isOffline", async () => {
      const offlineInput: CreateCostInput = {
        attributionType: "direct",
        billId: "bill-002",
        amount: 1200000,
        currencyCode: "VND",
        costTypeCode: "HANDLING",
      };

      const result = await useCase.execute(offlineInput, true);
      expect(result.success).toBe(true);
      expect(result.isOffline).toBe(true);
      expect(mockRepo.createdCosts[0].isOffline).toBe(true);
    });
  });
});
