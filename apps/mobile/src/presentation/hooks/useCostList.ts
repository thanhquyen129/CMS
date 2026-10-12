import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert } from "react-native";
import { BillListItemDto, CostItemDto, formatMoney } from "@lcms/shared";
import { useAuth } from "../../auth/AuthContext";
import { CostLocalDataSource } from "../../data/datasources/CostLocalDataSource";
import { CostRemoteDataSource } from "../../data/datasources/CostRemoteDataSource";
import { CostRepositoryImpl } from "../../data/repositories/CostRepositoryImpl";
import {
  CostAllocationBasis,
  CostAttributionType,
  CostCurrencyCode,
  CostFinancialMaturity,
} from "../../domain/entities/Cost";
import { AdjustCostUseCase } from "../../domain/usecases/AdjustCostUseCase";
import { AdvanceCostMaturityUseCase } from "../../domain/usecases/AdvanceCostMaturityUseCase";
import { AllocateCostUseCase } from "../../domain/usecases/AllocateCostUseCase";
import { CreateCostUseCase } from "../../domain/usecases/CreateCostUseCase";
import { GetCostsUseCase } from "../../domain/usecases/GetCostsUseCase";
import { verifyBiometricForMoneyAction } from "../../security/biometrics";

// Khởi tạo các tầng Singleton/Instances
const costRemoteDataSource = new CostRemoteDataSource();
const costLocalDataSource = new CostLocalDataSource();
const costRepository = new CostRepositoryImpl(costRemoteDataSource, costLocalDataSource);

const getCostsUseCase = new GetCostsUseCase(costRepository);
const createCostUseCase = new CreateCostUseCase(costRepository);
const advanceCostMaturityUseCase = new AdvanceCostMaturityUseCase(costRepository, createCostUseCase);
const adjustCostUseCase = new AdjustCostUseCase(costRepository);
const allocateCostUseCase = new AllocateCostUseCase(costRepository);

export function useCostList() {
  const { bootstrap, t } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;

  const [costs, setCosts] = useState<CostItemDto[]>([]);
  const [bills, setBills] = useState<BillListItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Bộ lọc
  const [filterMaturity, setFilterMaturity] = useState<CostFinancialMaturity | "all">("all");
  const [searchQuery, setSearchQuery] = useState<string>("");

  // Chi tiết & Xử lý chi phí được chọn
  const [selectedCost, setSelectedCost] = useState<CostItemDto | null>(null);
  const [nextAmountInput, setNextAmountInput] = useState<string>("");
  const [detailTab, setDetailTab] = useState<"maturity" | "adjust" | "allocate">("maturity");
  const [adjustDelta, setAdjustDelta] = useState<string>("");
  const [adjustReason, setAdjustReason] = useState<string>("");
  const [allocationBasis, setAllocationBasis] = useState<CostAllocationBasis>("equal");
  const [allocBillIds, setAllocBillIds] = useState<string[]>([]);

  // Form tạo chi phí mới
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [attributionType, setAttributionType] = useState<CostAttributionType>("direct");
  const [costTypeCode, setCostTypeCode] = useState<string>("FREIGHT");
  const [createAmount, setCreateAmount] = useState<string>("");
  const [createCurrency, setCreateCurrency] = useState<CostCurrencyCode>("VND");
  const [selectedBillId, setSelectedBillId] = useState<string>("");

  const loadCosts = useCallback(async () => {
    if (!canViewCost) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const output = await getCostsUseCase.execute({
        financialMaturity: filterMaturity === "all" ? undefined : filterMaturity,
        searchQuery: searchQuery || undefined,
      });
      setCosts(output.costs);
      setBills(output.bills);
      if (!selectedBillId && output.bills.length > 0) {
        setSelectedBillId(output.bills[0].id);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không thể tải danh sách chi phí.");
      setCosts([]);
    } finally {
      setLoading(false);
    }
  }, [canViewCost, filterMaturity, searchQuery, selectedBillId]);

  useEffect(() => {
    void loadCosts();
  }, [loadCosts]);

  const handleSelectCost = (cost: CostItemDto | null) => {
    setSelectedCost(cost);
    if (cost) {
      setNextAmountInput(String(cost.amount));
      setDetailTab("maturity");
    } else {
      setNextAmountInput("");
      setAdjustDelta("");
      setAdjustReason("");
    }
  };

  const handleCreateCost = async (offlineOnly = false) => {
    const amt = Number(createAmount);
    try {
      setSubmitting(true);
      const res = await createCostUseCase.execute(
        {
          billId: attributionType === "direct" ? selectedBillId : null,
          attributionType,
          amount: amt,
          currencyCode: createCurrency,
          costTypeCode,
          sourceType: "mobile_entry",
        },
        offlineOnly
      );

      setShowCreate(false);
      setCreateAmount("");
      if (!res.isOffline) {
        await loadCosts();
      }
      Alert.alert(res.isOffline ? "Đã lưu Offline" : "Thành công", res.messageVi);
    } catch (err) {
      Alert.alert(
        "Lỗi tạo Chi phí",
        err instanceof Error ? err.message : "Không thể tạo chi phí."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAdvanceMaturity = async (
    cost: CostItemDto,
    action: "confirm" | "actualize"
  ) => {
    const targetAmount = nextAmountInput.trim()
      ? Number(nextAmountInput.trim())
      : cost.amount;

    const actionLabel =
      action === "confirm" ? "Xác nhận Chi phí (Confirmed)" : "Chốt Chi phí Thực tế (Actual)";

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: actionLabel,
      amountSummaryVi: formatMoney(targetAmount, cost.currencyCode),
    });

    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    try {
      setSubmitting(true);
      await advanceCostMaturityUseCase.execute(
        {
          costId: cost.id,
          action,
          targetAmount,
          rowVersion: cost.rowVersion,
        },
        cost
      );

      handleSelectCost(null);
      await loadCosts();
      Alert.alert(
        "Đã cập nhật Chi phí",
        action === "confirm"
          ? "Khoản chi phí đã chuyển sang trạng thái Đã xác nhận (Confirmed)."
          : "Khoản chi phí đã chuyển sang trạng thái Thực tế (Actual)."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi cập nhật Chi phí",
        err instanceof Error ? err.message : "Không thể cập nhật khoản chi phí."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAdjustCost = async (cost: CostItemDto) => {
    const delta = Number(adjustDelta);
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Điều chỉnh Chi phí (Cost Adjustment)",
      amountSummaryVi: `${delta > 0 ? "+" : ""}${formatMoney(delta, cost.currencyCode)}`,
    });
    if (!bio.verified) return;

    try {
      setSubmitting(true);
      await adjustCostUseCase.execute({
        costId: cost.id,
        deltaAmount: delta,
        reason: adjustReason.trim(),
        rowVersion: cost.rowVersion,
      });

      setAdjustDelta("");
      setAdjustReason("");
      handleSelectCost(null);
      await loadCosts();
      Alert.alert("Đã điều chỉnh", "Đã ghi nhận bút toán điều chỉnh chi phí & AuditEvent.");
    } catch (err) {
      Alert.alert(
        "Lỗi điều chỉnh",
        err instanceof Error ? err.message : "Không thể điều chỉnh chi phí."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAllocateSharedCost = async (cost: CostItemDto) => {
    const targets = allocBillIds.length > 0 ? allocBillIds : bills.slice(0, 2).map((b) => b.id);
    try {
      setSubmitting(true);
      await allocateCostUseCase.execute({
        costId: cost.id,
        costTypeCode: cost.costTypeCode,
        totalAmount: cost.amount,
        allocationBasis,
        targetBillIds: targets,
      });

      handleSelectCost(null);
      await loadCosts();
      Alert.alert(
        "Đã tạo Phân bổ Chi phí",
        `Đã phân bổ khoản chi phí ${cost.costTypeCode ?? ""} cho ${targets.length} Vận đơn.`
      );
    } catch (err) {
      Alert.alert(
        "Lỗi phân bổ",
        err instanceof Error ? err.message : "Không thể tạo phân bổ chi phí."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const toggleAllocBill = (id: string) => {
    setAllocBillIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  return {
    canViewCost,
    t,
    costs,
    bills,
    loading,
    submitting,
    error,
    // Filters
    filterMaturity,
    setFilterMaturity,
    searchQuery,
    setSearchQuery,
    // Create form
    showCreate,
    setShowCreate,
    attributionType,
    setAttributionType,
    costTypeCode,
    setCostTypeCode,
    createAmount,
    setCreateAmount,
    createCurrency,
    setCreateCurrency,
    selectedBillId,
    setSelectedBillId,
    // Detail sheet
    selectedCost,
    handleSelectCost,
    detailTab,
    setDetailTab,
    nextAmountInput,
    setNextAmountInput,
    adjustDelta,
    setAdjustDelta,
    adjustReason,
    setAdjustReason,
    allocationBasis,
    setAllocationBasis,
    allocBillIds,
    toggleAllocBill,
    // Operations
    loadCosts,
    handleCreateCost,
    handleAdvanceMaturity,
    handleAdjustCost,
    handleAllocateSharedCost,
  };
}
