import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import {
  BillListItemDto,
  CostItemDto,
  createIdempotencyKey,
  formatMoney,
} from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { CompactTxnCard } from "../../src/components/CompactTxnCard";
import { BalanceImpactCard } from "../../src/components/BalanceImpactCard";
import { AttachmentGallery } from "../../src/components/AttachmentGallery";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";
import { enqueueOfflineMutation } from "../../src/offline/outbox";

const COST_TYPE_OPTIONS = [
  "FREIGHT",
  "TRUCKING",
  "HANDLING",
  "CUSTOMS",
  "SURCHARGE",
] as const;

export default function CostsScreen() {
  const { bootstrap, t } = useAuth();
  const [costs, setCosts] = useState<CostItemDto[]>([]);
  const [bills, setBills] = useState<BillListItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [selectedCost, setSelectedCost] = useState<CostItemDto | null>(null);
  const [nextAmountInput, setNextAmountInput] = useState<string>("");
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Create Cost form state
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [attributionType, setAttributionType] = useState<"direct" | "shared">("direct");
  const [costTypeCode, setCostTypeCode] = useState<string>("FREIGHT");
  const [createAmount, setCreateAmount] = useState<string>("");
  const [createCurrency, setCreateCurrency] = useState<"VND" | "USD">("VND");
  const [selectedBillId, setSelectedBillId] = useState<string>("");

  // Adjustment & Allocation state inside detail sheet
  const [detailTab, setDetailTab] = useState<"maturity" | "adjust" | "allocate">("maturity");
  const [adjustDelta, setAdjustDelta] = useState<string>("");
  const [adjustReason, setAdjustReason] = useState<string>("");
  const [allocationBasis, setAllocationBasis] = useState<"equal" | "weight" | "manual">("equal");
  const [allocBillIds, setAllocBillIds] = useState<string[]>([]);

  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;

  const loadCosts = useCallback(async () => {
    if (!canViewCost) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const [costRes, billRes] = await Promise.all([
        apiRequest<CostItemDto[] | { items: CostItemDto[] }>("/api/costs"),
        apiRequest<BillListItemDto[] | { items: BillListItemDto[] }>("/api/bills").catch(
          () => [] as BillListItemDto[]
        ),
      ]);
      setCosts(Array.isArray(costRes) ? costRes : costRes?.items ?? []);
      const billList = Array.isArray(billRes) ? billRes : billRes?.items ?? [];
      setBills(billList.slice(0, 12));
      if (!selectedBillId && billList.length > 0) {
        setSelectedBillId(billList[0].id);
      }
    } catch {
      setCosts([]);
    } finally {
      setLoading(false);
    }
  }, [canViewCost, selectedBillId]);

  useEffect(() => {
    void loadCosts();
  }, [loadCosts]);

  if (!canViewCost) {
    return (
      <View style={styles.sodBlocked}>
        <Text style={styles.sodTitle}>
          Giới hạn Quyền Truy cập Tài chính
        </Text>
        <Text style={styles.sodDesc}>
          Theo chính sách phân định trách nhiệm nội bộ, tài khoản hiện tại không có quyền truy cập phân hệ Chi phí &amp; Công nợ Phải trả.
        </Text>
      </View>
    );
  }

  const handleCreateCost = async (offlineOnly = false) => {
    const amt = Number(createAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Số tiền không hợp lệ", "Vui lòng nhập số tiền chi phí lớn hơn 0.");
      return;
    }
    if (attributionType === "direct" && !selectedBillId) {
      Alert.alert("Thiếu Vận đơn", "Chi phí trực tiếp (Direct) cần chọn Vận đơn gắn kết.");
      return;
    }

    const payload = {
      billId: attributionType === "direct" ? selectedBillId : null,
      attributionType,
      amount: amt,
      currencyCode: createCurrency,
      costTypeCode,
      sourceType: "mobile_entry",
    };

    if (offlineOnly) {
      await enqueueOfflineMutation({
        titleVi: `Tạo chi phí ${costTypeCode} (${formatMoney(amt, createCurrency)})`,
        endpoint: "/api/costs",
        method: "POST",
        payload,
        idempotencyPrefix: "cost-new",
      });
      setShowCreate(false);
      setCreateAmount("");
      Alert.alert("Đã lưu Offline", "Khoản chi phí dự kiến đã được đưa vào Hàng đợi Đồng bộ.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest("/api/costs", {
        method: "POST",
        idempotencyKey: createIdempotencyKey("cost-new"),
        body: payload,
      });
      setShowCreate(false);
      setCreateAmount("");
      await loadCosts();
      Alert.alert("Thành công", "Đã tạo mới khoản chi phí dự kiến (Expected).");
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

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi:
        action === "confirm"
          ? "Xác nhận Chi phí (Confirmed)"
          : "Chốt Chi phí Thực tế (Actual)",
      amountSummaryVi: formatMoney(targetAmount, cost.currencyCode),
    });

    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/costs/${cost.id}/${action}`, {
        method: "POST",
        ifMatch: cost.rowVersion,
        idempotencyKey: createIdempotencyKey(`cost-${action}`),
        body:
          action === "confirm"
            ? { confirmedAmount: targetAmount }
            : { actualAmount: targetAmount },
      });
      setSelectedCost(null);
      setNextAmountInput("");
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
    if (!delta || !adjustReason.trim()) {
      Alert.alert(
        "Thiếu thông tin điều chỉnh",
        "Vui lòng nhập chênh lệch số tiền (+/-) và lý do điều chỉnh kiểm toán."
      );
      return;
    }

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Điều chỉnh Chi phí (Cost Adjustment)",
      amountSummaryVi: `${delta > 0 ? "+" : ""}${formatMoney(delta, cost.currencyCode)}`,
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      await apiRequest(`/api/costs/${cost.id}/adjustments`, {
        method: "POST",
        ifMatch: cost.rowVersion,
        idempotencyKey: createIdempotencyKey("cost-adj"),
        body: {
          adjustmentType: delta >= 0 ? "increase" : "decrease",
          deltaAmount: delta,
          reason: adjustReason.trim(),
        },
      });
      setAdjustDelta("");
      setAdjustReason("");
      setSelectedCost(null);
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
    if (targets.length === 0) {
      Alert.alert("Chưa chọn Bill", "Vui lòng chọn ít nhất 1 Vận đơn để nhận phân bổ chi phí chung.");
      return;
    }

    setSubmitting(true);
    try {
      const sharePerBill = Math.round((cost.amount / targets.length) * 100) / 100;
      const res = await apiRequest<{ id: string }>(`/api/costs/${cost.id}/allocations`, {
        method: "POST",
        idempotencyKey: createIdempotencyKey("cost-alloc"),
        body: {
          allocationBasis,
          details: targets.map((bId) => ({
            billId: bId,
            basisValue: 1,
            manualOverrideAmount: allocationBasis === "manual" ? sharePerBill : null,
            overrideReason: allocationBasis === "manual" ? "Phân bổ trên Mobile" : null,
          })),
        },
      });

      if (res?.id) {
        await apiRequest(`/api/cost-allocations/${res.id}/calculate`, {
          method: "POST",
        }).catch(() => undefined);
      }

      setSelectedCost(null);
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

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadCosts()} />
      }
    >
      <View style={styles.headerRow}>
        <Text style={styles.heading}>
          Quản trị Chi phí ({costs.length})
        </Text>
        <TouchableOpacity
          style={styles.addBtn}
          onPress={() => setShowCreate((v) => !v)}
        >
          <Text style={styles.addBtnText}>
            {showCreate ? "✕ Đóng" : "+ Thêm Chi phí"}
          </Text>
        </TouchableOpacity>
      </View>

      {showCreate ? (
        <View style={styles.createCard}>
          <Text style={styles.detailTitle}>Ghi nhận Chi phí Mới (Expected Cost)</Text>

          <Text style={styles.label}>Loại quy속 (Attribution)</Text>
          <View style={styles.chipRow}>
            <TouchableOpacity
              style={[styles.chip, attributionType === "direct" && styles.chipActive]}
              onPress={() => setAttributionType("direct")}
            >
              <Text
                style={[
                  styles.chipText,
                  attributionType === "direct" && styles.chipTextActive,
                ]}
              >
                Trực tiếp theo Bill (Direct)
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.chip, attributionType === "shared" && styles.chipActive]}
              onPress={() => setAttributionType("shared")}
            >
              <Text
                style={[
                  styles.chipText,
                  attributionType === "shared" && styles.chipTextActive,
                ]}
              >
                Chi phí dùng chung (Shared)
              </Text>
            </TouchableOpacity>
          </View>

          <Text style={styles.label}>Nhóm Chi phí</Text>
          <View style={styles.chipRow}>
            {COST_TYPE_OPTIONS.map((code) => (
              <TouchableOpacity
                key={code}
                style={[styles.chip, costTypeCode === code && styles.chipActive]}
                onPress={() => setCostTypeCode(code)}
              >
                <Text
                  style={[
                    styles.chipText,
                    costTypeCode === code && styles.chipTextActive,
                  ]}
                >
                  {code}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {attributionType === "direct" && bills.length > 0 ? (
            <>
              <Text style={styles.label}>Chọn Vận đơn (Bill)</Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <View style={styles.chipRow}>
                  {bills.slice(0, 8).map((b) => (
                    <TouchableOpacity
                      key={b.id}
                      style={[
                        styles.chip,
                        selectedBillId === b.id && styles.chipActive,
                      ]}
                      onPress={() => setSelectedBillId(b.id)}
                    >
                      <Text
                        style={[
                          styles.chipText,
                          selectedBillId === b.id && styles.chipTextActive,
                        ]}
                      >
                        {b.billNo}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </ScrollView>
            </>
          ) : null}

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Số tiền nguyên tệ</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={createAmount}
                onChangeText={setCreateAmount}
                placeholder="VD: 1500000"
              />
            </View>
            <View>
              <Text style={styles.label}>Tiền tệ</Text>
              <View style={styles.chipRow}>
                {(["VND", "USD"] as const).map((cur) => (
                  <TouchableOpacity
                    key={cur}
                    style={[styles.chip, createCurrency === cur && styles.chipActive]}
                    onPress={() => setCreateCurrency(cur)}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        createCurrency === cur && styles.chipTextActive,
                      ]}
                    >
                      {cur}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
            </View>
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => void handleCreateCost(false)}
              disabled={submitting}
            >
              <Text style={styles.primaryBtnText}>✓ Tạo Chi phí</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.secondaryBtn}
              onPress={() => void handleCreateCost(true)}
              disabled={submitting}
            >
              <Text style={styles.secondaryBtnText}>📡 Lưu Offline</Text>
            </TouchableOpacity>
          </View>
        </View>
      ) : null}

      {selectedCost ? (
        <View style={styles.detailSheet}>
          <Text style={styles.detailTitle}>
            Xử lý Chi phí: {selectedCost.costTypeCode ?? "CHI PHÍ"} (
            {formatMoney(selectedCost.amount, selectedCost.currencyCode)})
          </Text>

          <View style={styles.chipRow}>
            <TouchableOpacity
              style={[styles.chip, detailTab === "maturity" && styles.chipActive]}
              onPress={() => setDetailTab("maturity")}
            >
              <Text
                style={[
                  styles.chipText,
                  detailTab === "maturity" && styles.chipTextActive,
                ]}
              >
                1. Chuyển trạng thái
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.chip, detailTab === "adjust" && styles.chipActive]}
              onPress={() => setDetailTab("adjust")}
            >
              <Text
                style={[
                  styles.chipText,
                  detailTab === "adjust" && styles.chipTextActive,
                ]}
              >
                2. Điều chỉnh (+/-)
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.chip, detailTab === "allocate" && styles.chipActive]}
              onPress={() => setDetailTab("allocate")}
            >
              <Text
                style={[
                  styles.chipText,
                  detailTab === "allocate" && styles.chipTextActive,
                ]}
              >
                3. Phân bổ cho Bill
              </Text>
            </TouchableOpacity>
          </View>

          {detailTab === "maturity" ? (
            <>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={nextAmountInput}
                onChangeText={setNextAmountInput}
                placeholder={`Số tiền mới (${selectedCost.currencyCode})`}
              />

              {selectedCost.financialMaturity === "expected" ? (
                <BalanceImpactCard
                  titleVi="Xác nhận Chi phí (Expected → Confirmed)"
                  currencyCode={selectedCost.currencyCode}
                  beforeAmount={selectedCost.expectedAmount}
                  afterAmount={
                    nextAmountInput.trim()
                      ? Number(nextAmountInput.trim())
                      : selectedCost.amount
                  }
                  rowVersion={selectedCost.rowVersion}
                  confirmLabelVi="Xác nhận (Confirmed)"
                  isSubmitting={submitting}
                  onConfirmWithBiometric={() =>
                    handleAdvanceMaturity(selectedCost, "confirm")
                  }
                  onCancel={() => setSelectedCost(null)}
                />
              ) : selectedCost.financialMaturity === "confirmed" ? (
                <BalanceImpactCard
                  titleVi="Chốt Chi phí Thực tế (Confirmed → Actual)"
                  currencyCode={selectedCost.currencyCode}
                  beforeAmount={selectedCost.confirmedAmount ?? selectedCost.amount}
                  afterAmount={
                    nextAmountInput.trim()
                      ? Number(nextAmountInput.trim())
                      : selectedCost.amount
                  }
                  rowVersion={selectedCost.rowVersion}
                  confirmLabelVi="Chốt Thực tế (Actual)"
                  isSubmitting={submitting}
                  onConfirmWithBiometric={() =>
                    handleAdvanceMaturity(selectedCost, "actualize")
                  }
                  onCancel={() => setSelectedCost(null)}
                />
              ) : (
                <Text style={styles.actualDoneNote}>
                  ✓ Khoản chi phí này đã ở trạng thái Thực tế (Actual). Sử dụng tab Điều chỉnh nếu cần thay đổi số tiền.
                </Text>
              )}
            </>
          ) : null}

          {detailTab === "adjust" ? (
            <View style={{ marginTop: 8 }}>
              <Text style={styles.label}>
                Chênh lệch điều chỉnh ({selectedCost.currencyCode}, nhập âm nếu giảm)
              </Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={adjustDelta}
                onChangeText={setAdjustDelta}
                placeholder="VD: 250000 hoặc -100000"
              />
              <Text style={styles.label}>Lý do điều chỉnh kiểm toán (Bắt buộc)</Text>
              <TextInput
                style={styles.input}
                value={adjustReason}
                onChangeText={setAdjustReason}
                placeholder="Nhập lý do điều chỉnh chi phí..."
              />
              <BalanceImpactCard
                titleVi="Tác động Điều chỉnh Chi phí (Before → After)"
                currencyCode={selectedCost.currencyCode}
                beforeAmount={selectedCost.amount}
                afterAmount={selectedCost.amount + (Number(adjustDelta) || 0)}
                rowVersion={selectedCost.rowVersion}
                confirmLabelVi="Xác nhận Điều chỉnh"
                isSubmitting={submitting}
                onConfirmWithBiometric={() => handleAdjustCost(selectedCost)}
                onCancel={() => setSelectedCost(null)}
              />
            </View>
          ) : null}

          {detailTab === "allocate" ? (
            <View style={{ marginTop: 8 }}>
              <Text style={styles.label}>Tiêu thức phân bổ (Allocation Basis)</Text>
              <View style={styles.chipRow}>
                {(["equal", "weight", "manual"] as const).map((b) => (
                  <TouchableOpacity
                    key={b}
                    style={[styles.chip, allocationBasis === b && styles.chipActive]}
                    onPress={() => setAllocationBasis(b)}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        allocationBasis === b && styles.chipTextActive,
                      ]}
                    >
                      {b === "equal"
                        ? "Chia đều (Equal)"
                        : b === "weight"
                        ? "Theo Trọng lượng (CW)"
                        : "Chỉ định thủ công"}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>

              <Text style={styles.label}>Chọn các Vận đơn nhận phân bổ:</Text>
              <View style={styles.chipRow}>
                {bills.slice(0, 6).map((b) => {
                  const active = allocBillIds.includes(b.id);
                  return (
                    <TouchableOpacity
                      key={b.id}
                      style={[styles.chip, active && styles.chipActive]}
                      onPress={() => toggleAllocBill(b.id)}
                    >
                      <Text style={[styles.chipText, active && styles.chipTextActive]}>
                        {active ? "✓ " : ""}{b.billNo}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>

              <TouchableOpacity
                style={[styles.primaryBtn, { marginTop: 10 }]}
                onPress={() => void handleAllocateSharedCost(selectedCost)}
                disabled={submitting}
              >
                <Text style={styles.primaryBtnText}>
                  ✓ Thực hiện Phân bổ Chi phí cho Bill
                </Text>
              </TouchableOpacity>
            </View>
          ) : null}

          <AttachmentGallery
            objectType="cost"
            objectId={selectedCost.id}
            objectLabelVi={`Chi phí ${selectedCost.costTypeCode ?? ""}`}
          />
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : (
        costs.map((c) => (
          <CompactTxnCard
            key={c.id}
            primaryCode={`${c.costTypeCode ?? "COST"} • ${
              c.billNo ?? t(`attribution.${c.attributionType}`, c.attributionType)
            }`}
            statusLabelVi={t(
              `maturity.${c.financialMaturity}`,
              c.financialMaturity
            )}
            statusTone={
              c.financialMaturity === "actual"
                ? "success"
                : c.financialMaturity === "confirmed"
                ? "info"
                : "warning"
            }
            secondaryLabelVi={
              c.vendorName ??
              t(`attribution.${c.attributionType}`, c.attributionType)
            }
            effectiveDate={c.effectiveDate}
            amount={c.amount}
            currencyCode={c.currencyCode}
            reportingAmount={c.baseAmount}
            reportingCurrencyCode={c.reportingCurrencyCode}
            fxStatusLabelVi={t(`fx.${c.fxStatus}`, c.fxStatus)}
            actionLabelVi="Xử lý & Chứng từ"
            onPress={() => {
              setSelectedCost(c);
              setNextAmountInput(String(c.amount));
            }}
            onActionPress={() => {
              setSelectedCost(c);
              setNextAmountInput(String(c.amount));
            }}
          />
        ))
      )}
      <View style={{ height: 32 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 16,
  },
  headerRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  heading: {
    fontSize: 14,
    fontWeight: "800",
    color: "#0F172A",
  },
  addBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 8,
  },
  addBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  createCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#BFDBFE",
    marginBottom: 14,
  },
  sodBlocked: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 24,
    backgroundColor: "#F8FAFC",
  },
  sodTitle: {
    fontSize: 16,
    fontWeight: "800",
    color: "#DC2626",
  },
  sodDesc: {
    fontSize: 13,
    color: "#475569",
    textAlign: "center",
    marginTop: 8,
  },
  detailSheet: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginBottom: 16,
  },
  detailTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 8,
  },
  label: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
    marginTop: 6,
    marginBottom: 4,
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 13,
    backgroundColor: "#F8FAFC",
    color: "#0F172A",
    marginBottom: 6,
  },
  twoCol: {
    flexDirection: "row",
    gap: 10,
    alignItems: "flex-end",
  },
  chipRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginVertical: 4,
  },
  chip: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 6,
    backgroundColor: "#F8FAFC",
  },
  chipActive: {
    borderColor: "#2563EB",
    backgroundColor: "#EFF6FF",
  },
  chipText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#475569",
  },
  chipTextActive: {
    color: "#1D4ED8",
    fontWeight: "700",
  },
  actionRow: {
    flexDirection: "row",
    gap: 8,
    marginTop: 10,
  },
  primaryBtn: {
    flex: 1,
    backgroundColor: "#0F172A",
    paddingVertical: 11,
    borderRadius: 8,
    alignItems: "center",
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  secondaryBtn: {
    backgroundColor: "#EFF6FF",
    borderWidth: 1,
    borderColor: "#93C5FD",
    paddingHorizontal: 12,
    paddingVertical: 11,
    borderRadius: 8,
  },
  secondaryBtnText: {
    color: "#1D4ED8",
    fontSize: 12,
    fontWeight: "700",
  },
  actualDoneNote: {
    fontSize: 12,
    color: "#047857",
    fontWeight: "600",
    marginVertical: 8,
  },
});
