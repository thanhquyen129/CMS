import { BillListItemDto, CostItemDto } from "@lcms/shared";
import {
  AdjustCostInput,
  AdvanceCostMaturityInput,
  AllocateCostInput,
  CostFilterParams,
  CreateCostInput,
} from "../entities/Cost";

export interface CreateCostResult {
  success: boolean;
  isOffline: boolean;
  id?: string;
  messageVi: string;
}

export interface ICostRepository {
  getCosts(filter?: CostFilterParams): Promise<CostItemDto[]>;
  getBills(): Promise<BillListItemDto[]>;
  createCost(input: CreateCostInput, isOffline?: boolean): Promise<CreateCostResult>;
  advanceMaturity(input: AdvanceCostMaturityInput): Promise<void>;
  adjustCost(input: AdjustCostInput): Promise<void>;
  allocateCost(input: AllocateCostInput): Promise<{ allocationId?: string }>;
}
