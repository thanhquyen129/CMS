import { BillListItemDto, CostItemDto, formatMoney } from "@lcms/shared";
import { enqueueOfflineMutation } from "../../offline/outbox";
import {
  AdjustCostInput,
  AdvanceCostMaturityInput,
  AllocateCostInput,
  CostFilterParams,
  CreateCostInput,
} from "../../domain/entities/Cost";
import { CreateCostResult, ICostRepository } from "../../domain/repositories/ICostRepository";
import { CostLocalDataSource } from "../datasources/CostLocalDataSource";
import { CostRemoteDataSource } from "../datasources/CostRemoteDataSource";

export class CostRepositoryImpl implements ICostRepository {
  constructor(
    private readonly remoteDataSource: CostRemoteDataSource,
    private readonly localDataSource: CostLocalDataSource
  ) {}

  public async getCosts(filter?: CostFilterParams): Promise<CostItemDto[]> {
    try {
      const remoteData = await this.remoteDataSource.fetchCosts();
      // Đệm ngầm vào SQLite (fire and forget cache update)
      void this.localDataSource.saveCachedCosts(remoteData);
      return remoteData;
    } catch {
      // Khi mất kết nối mạng hoặc lỗi server, tự động fallback đọc từ SQLite cache
      const cached = await this.localDataSource.getCachedCosts();
      return cached;
    }
  }

  public async getBills(): Promise<BillListItemDto[]> {
    return await this.remoteDataSource.fetchBills();
  }

  public async createCost(
    input: CreateCostInput,
    isOffline = false
  ): Promise<CreateCostResult> {
    const payload = {
      billId: input.attributionType === "direct" ? input.billId : null,
      attributionType: input.attributionType,
      amount: input.amount,
      currencyCode: input.currencyCode,
      costTypeCode: input.costTypeCode,
      sourceType: input.sourceType ?? "mobile_entry",
    };

    if (isOffline) {
      await enqueueOfflineMutation({
        titleVi: `Tạo chi phí ${input.costTypeCode} (${formatMoney(input.amount, input.currencyCode)})`,
        endpoint: "/api/costs",
        method: "POST",
        payload,
        idempotencyPrefix: "cost-new",
      });

      return {
        success: true,
        isOffline: true,
        messageVi: "Khoản chi phí dự kiến đã được lưu trữ ngoại tuyến và đưa vào Hàng đợi Đồng bộ.",
      };
    }

    try {
      const res = await this.remoteDataSource.createCost(input);
      return {
        success: true,
        isOffline: false,
        id: res?.id,
        messageVi: "Đã tạo mới khoản chi phí dự kiến (Expected) thành công.",
      };
    } catch (err) {
      // Nếu gặp lỗi mạng không có kết nối, tự động chuyển vào hàng đợi offline
      const isNetworkIssue =
        err instanceof Error &&
        (err.message.includes("Network") ||
          err.message.includes("fetch") ||
          err.message.includes("kết nối"));

      if (isNetworkIssue) {
        await enqueueOfflineMutation({
          titleVi: `Tạo chi phí ${input.costTypeCode} (${formatMoney(input.amount, input.currencyCode)})`,
          endpoint: "/api/costs",
          method: "POST",
          payload,
          idempotencyPrefix: "cost-new",
        });
        return {
          success: true,
          isOffline: true,
          messageVi: "Không có kết nối mạng. Đã tự động lưu vào Hàng đợi Đồng bộ Offline.",
        };
      }
      throw err;
    }
  }

  public async advanceMaturity(input: AdvanceCostMaturityInput): Promise<void> {
    await this.remoteDataSource.advanceMaturity(input);
  }

  public async adjustCost(input: AdjustCostInput): Promise<void> {
    await this.remoteDataSource.adjustCost(input);
  }

  public async allocateCost(input: AllocateCostInput): Promise<{ allocationId?: string }> {
    return await this.remoteDataSource.allocateCost(input);
  }
}

