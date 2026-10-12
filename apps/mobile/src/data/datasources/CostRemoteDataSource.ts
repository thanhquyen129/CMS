import { BillListItemDto, CostItemDto, createIdempotencyKey } from "@lcms/shared";
import { apiRequest } from "../../api/client";
import {
  AdjustCostInput,
  AdvanceCostMaturityInput,
  AllocateCostInput,
  CreateCostInput,
} from "../../domain/entities/Cost";

export class CostRemoteDataSource {
  public async fetchCosts(): Promise<CostItemDto[]> {
    const res = await apiRequest<CostItemDto[] | { items: CostItemDto[] }>("/api/costs");
    return Array.isArray(res) ? res : res?.items ?? [];
  }

  public async fetchBills(): Promise<BillListItemDto[]> {
    try {
      const res = await apiRequest<BillListItemDto[] | { items: BillListItemDto[] }>("/api/bills");
      const list = Array.isArray(res) ? res : res?.items ?? [];
      return list.slice(0, 12);
    } catch {
      return [];
    }
  }

  public async createCost(input: CreateCostInput): Promise<{ id?: string }> {
    const payload = {
      billId: input.attributionType === "direct" ? input.billId : null,
      attributionType: input.attributionType,
      amount: input.amount,
      currencyCode: input.currencyCode,
      costTypeCode: input.costTypeCode,
      sourceType: input.sourceType ?? "mobile_entry",
    };

    return await apiRequest<{ id?: string }>("/api/costs", {
      method: "POST",
      idempotencyKey: createIdempotencyKey("cost-new"),
      body: payload,
    });
  }

  public async advanceMaturity(input: AdvanceCostMaturityInput): Promise<void> {
    const endpoint = `/api/costs/${input.costId}/${input.action}`;
    const body =
      input.action === "confirm"
        ? { confirmedAmount: input.targetAmount }
        : { actualAmount: input.targetAmount };

    await apiRequest(endpoint, {
      method: "POST",
      ifMatch: input.rowVersion,
      idempotencyKey: createIdempotencyKey(`cost-${input.action}`),
      body,
    });
  }

  public async adjustCost(input: AdjustCostInput): Promise<void> {
    const delta = input.deltaAmount;
    await apiRequest(`/api/costs/${input.costId}/adjustments`, {
      method: "POST",
      ifMatch: input.rowVersion,
      idempotencyKey: createIdempotencyKey("cost-adj"),
      body: {
        adjustmentType: delta >= 0 ? "increase" : "decrease",
        deltaAmount: delta,
        reason: input.reason.trim(),
      },
    });
  }

  public async allocateCost(input: AllocateCostInput): Promise<{ allocationId?: string }> {
    const targets = input.targetBillIds;
    const sharePerBill = Math.round((input.totalAmount / targets.length) * 100) / 100;

    const res = await apiRequest<{ id: string }>(`/api/costs/${input.costId}/allocations`, {
      method: "POST",
      idempotencyKey: createIdempotencyKey("cost-alloc"),
      body: {
        allocationBasis: input.allocationBasis,
        details: targets.map((bId) => ({
          billId: bId,
          basisValue: 1,
          manualOverrideAmount: input.allocationBasis === "manual" ? sharePerBill : null,
          overrideReason: input.allocationBasis === "manual" ? "Phân bổ trên Mobile" : null,
        })),
      },
    });

    if (res?.id) {
      await apiRequest(`/api/cost-allocations/${res.id}/calculate`, {
        method: "POST",
      }).catch(() => undefined);
    }

    return { allocationId: res?.id };
  }
}
