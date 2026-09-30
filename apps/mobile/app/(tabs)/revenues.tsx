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
  createIdempotencyKey,
  formatMoney,
  RevenueItemDto,
} from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { CompactTxnCard } from "../../src/components/CompactTxnCard";
import { BalanceImpactCard } from "../../src/components/BalanceImpactCard";
import { AttachmentGallery } from "../../src/components/AttachmentGallery";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";
import { enqueueOfflineMutation } from "../../src/offline/outbox";

const REVENUE_TYPE_OPTIONS = [
  "FREIGHT_REV",
  "SURCHARGE_REV",
  "HANDLING_REV",
  "CUSTOMS_REV",
  "COD_FEE",
] as const;

export default function RevenuesScreen() {
  const { bootstrap, t } = useAuth();
  const [revenues, setRevenues] = useState<RevenueItemDto[]>([]);
  const [bills, setBills] = useState<BillListItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [selectedRev, setSelectedRev] = useState<RevenueItemDto | null>(null);
  const [nextAmountInput, setNextAmountInput] = useState<string>("");
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Create Revenue form state
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [revenueTypeCode, setRevenueTypeCode] = useState<string>("FREIGHT_REV");
  const [createAmount, setCreateAmount] = useState<string>("");
  const [createCurrency, setCreateCurrency] = useState<"VND" | "USD">("VND");
  const [selectedBillId, setSelectedBillId] = useState<string>("");

  // Detail sheet tabs (Maturity / Adjust / Mapping)
  const [detailTab, setDetailTab] = useState<"maturity" | "adjust" | "mapping">("maturity");
  const [adjustDelta, setAdjustDelta] = useState<string>("");
  const [adjustReason, setAdjustReason] = useState<string>("");
  const [mappingBillIds, setMappingBillIds] = useState<string[]>([]);

  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? false;

  const loadRevenues = useCallback(async () => {
    if (!canViewRevenue) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const [revRes, billRes] = await Promise.all([
        apiRequest<RevenueItemDto[] | { items: RevenueItemDto[] }>("/api/revenues"),
        apiRequest<BillListItemDto[] | { items: BillListItemDto[] }>("/api/bills").catch(
          () => [] as BillListItemDto[]
        ),
      ]);
      setRevenues(Array.isArray(revRes) ? revRes : revRes?.items ?? []);
      const billList = Array.isArray(billRes) ? billRes : billRes?.items ?? [];
      setBills(billList.slice(0, 12));
      if (!selectedBillId && billList.length > 0) {
        setSelectedBillId(billList[0].id);
      }
    } catch {
      setRevenues([]);
    } finally {
      setLoading(false);
    }
  }, [canViewRevenue, selectedBillId]);

  useEffect(() => {
    void loadRevenues();
  }, [loadRevenues]);

  if (!canViewRevenue) {
    return (
      <View style={styles.sodBlocked}>
        <Text style={styles.sodTitle}>
          Phân tách Nhiệm vụ (Chi phí ≠ Doanh thu)
        </Text>
        <Text style={styles.sodDesc}>
          Tài khoản của bạn không được cấp quyền truy cập phân hệ Doanh thu (Revenue).
        </Text>
      </View>
    );
  }

  const handleCreateRevenue = async (offlineOnly = false) => {
    const amt = Number(createAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Số tiền không hợp lệ", "Vui lòng nhập số tiền doanh thu lớn hơn 0.");
      return;
    }
    if (!selectedBillId) {
      Alert.alert("Thiếu Vận đơn", "Vui lòng chọn Vận đơn (Bill) gắn với khoản doanh thu.");
      return;
    }

    const payload = {
      billId: selectedBillId,
      amount: amt,
      currencyCode: createCurrency,
      revenueTypeCode,
      sourceType: "mobile_entry",
    };

    if (offlineOnly) {
      await enqueueOfflineMutation({
        titleVi: `Tạo doanh thu ${revenueTypeCode} (${formatMoney(amt, createCurrency)})`,
        endpoint: "/api/revenues",
        method: "POST",
        payload,
        idempotencyPrefix: "rev-new",
      });
      setShowCreate(false);
      setCreateAmount("");
      Alert.alert("Đã lưu Offline", "Khoản doanh thu đã được đưa vào Hàng đợi Đồng bộ.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest("/api/revenues", {
        method: "POST",
        idempotencyKey: createIdempotencyKey("rev-new"),
        body: payload,
      });
      setShowCreate(false);
      setCreateAmount("");
      await loadRevenues();
      Alert.alert("Thành công", "Đã tạo mới khoản doanh thu dự kiến (Expected).");
    } catch (err) {
      Alert.alert(
        "Lỗi tạo Doanh thu",
        err instanceof Error ? err.message : "Không thể tạo doanh thu."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAdvanceMaturity = async (
    rev: RevenueItemDto,
    action: "confirm" | "actualize"
  ) => {
    const targetAmount = nextAmountInput.trim()
      ? Number(nextAmountInput.trim())
      : rev.amount;

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi:
        action === "confirm"
          ? "Xác nhận Doanh thu (Confirmed)"
          : "Chốt Doanh thu Thực tế (Actual)",
      amountSummaryVi: formatMoney(targetAmount, rev.currencyCode),
    });

    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/revenues/${rev.id}/${action}`, {
        method: "POST",
        ifMatch: rev.rowVersion,
        idempotencyKey: createIdempotencyKey(`rev-${action}`),
        body:
          action === "confirm"
            ? { confirmedAmount: targetAmount }
            : { actualAmount: targetAmount },
      });
      setSelectedRev(null);
      setNextAmountInput("");
      await loadRevenues();
      Alert.alert(
        "Đã cập nhật Doanh thu",
        action === "confirm"
          ? "Khoản doanh thu đã chuyển sang trạng thái Đã xác nhận (Confirmed)."
          : "Khoản doanh thu đã chuyển sang trạng thái Thực tế (Actual)."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi cập nhật Doanh thu",
        err instanceof Error ? err.message : "Không thể cập nhật khoản doanh thu."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAdjustRevenue = async (rev: RevenueItemDto) => {
    const delta = Number(adjustDelta);
    if (!delta || !adjustReason.trim()) {
      Alert.alert(
        "Thiếu thông tin điều chỉnh",
        "Vui lòng nhập chênh lệch số tiền (+/-) và lý do điều chỉnh kiểm toán."
      );
      return;
    }

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Điều chỉnh Doanh thu (Revenue Adjustment)",
      amountSummaryVi: `${delta > 0 ? "+" : ""}${formatMoney(delta, rev.currencyCode)}`,
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      await apiRequest(`/api/revenues/${rev.id}/adjustments`, {
        method: "POST",
        ifMatch: rev.rowVersion,
        idempotencyKey: createIdempotencyKey("rev-adj"),
        body: {
          adjustmentType: delta >= 0 ? "increase" : "decrease",
          deltaAmount: delta,
          reason: adjustReason.trim(),
        },
      });
      setAdjustDelta("");
      setAdjustReason("");
      setSelectedRev(null);
      await loadRevenues();
      Alert.alert("Đã điều chỉnh", "Đã ghi nhận bút toán điều chỉnh doanh thu & AuditEvent.");
    } catch (err) {
      Alert.alert(
        "Lỗi điều chỉnh",
        err instanceof Error ? err.message : "Không thể điều chỉnh doanh thu."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateMapping = async (rev: RevenueItemDto) => {
    const targets = mappingBillIds.length > 0 ? mappingBillIds : bills.slice(0, 2).map((b) => b.id);
    if (targets.length === 0) {
      Alert.alert("Chưa chọn Bill", "Vui lòng chọn ít nhất 1 Vận đơn để gán doanh thu.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/revenues/${rev.id}/mappings`, {
        method: "POST",
        body: {
          allocationBasis: "equal",
          details: targets.map((bId) => ({
            billId: bId,
            basisValue: 1,
          })),
        },
      });
      setSelectedRev(null);
      await loadRevenues();
      Alert.alert(
        "Đã gán Doanh thu",
        `Đã chia/gán khoản doanh thu ${rev.revenueTypeCode ?? ""} cho ${targets.length} Vận đơn.`
      );
    } catch (err) {
      Alert.alert(
        "Lỗi gán Doanh thu",
        err instanceof Error ? err.message : "Không thể tạo gán doanh thu."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const toggleMappingBill = (id: string) => {
    setMappingBillIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={loading}
          onRefresh={() => void loadRevenues()}
        />
      }
    >
      <View style={styles.headerRow}>
        <Text style={styles.heading}>
          Quản trị Doanh thu ({revenues.length})
        </Text>
        <TouchableOpacity
          style={styles.addBtn}
          onPress={() => setShowCreate((v) => !v)}
        >
          <Text style={styles.addBtnText}>
            {showCreate ? "✕ Đóng" : "+ Thêm Doanh thu"}
          </Text>
        </TouchableOpacity>
      </View>

      {showCreate ? (
        <View style={styles.createCard}>
          <Text style={styles.detailTitle}>Ghi nhận Doanh thu Mới (Expected Revenue)</Text>

          <Text style={styles.label}>Nhóm Doanh thu</Text>
          <View style={styles.chipRow}>
            {REVENUE_TYPE_OPTIONS.map((code) => (
              <TouchableOpacity
                key={code}
                style={[styles.chip, revenueTypeCode === code && styles.chipActive]}
                onPress={() => setRevenueTypeCode(code)}
              >
                <Text
                  style={[
                    styles.chipText,
                    revenueTypeCode === code && styles.chipTextActive,
                  ]}
                >
                  {code}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {bills.length > 0 ? (
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
                placeholder="VD: 3500000"
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
              onPress={() => void handleCreateRevenue(false)}
              disabled={submitting}
            >
              <Text style={styles.primaryBtnText}>✓ Tạo Doanh thu</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.secondaryBtn}
              onPress={() => void handleCreateRevenue(true)}
              disabled={submitting}
            >
              <Text style={styles.secondaryBtnText}>📡 Lưu Offline</Text>
            </TouchableOpacity>
          </View>
        </View>
      ) : null}

      {selectedRev ? (
        <View style={styles.detailSheet}>
          <Text style={styles.detailTitle}>
            Xử lý Doanh thu: {selectedRev.revenueTypeCode ?? "DOANH THU"} (
            {formatMoney(selectedRev.amount, selectedRev.currencyCode)})
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
              style={[styles.chip, detailTab === "mapping" && styles.chipActive]}
              onPress={() => setDetailTab("mapping")}
            >
              <Text
                style={[
                  styles.chipText,
                  detailTab === "mapping" && styles.chipTextActive,
                ]}
              >
                3. Chia/Gán theo Bill
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
                placeholder={`Số tiền mới (${selectedRev.currencyCode})`}
              />

              {selectedRev.financialMaturity === "expected" ? (
                <BalanceImpactCard
                  titleVi="Xác nhận Doanh thu (Expected → Confirmed)"
                  currencyCode={selectedRev.currencyCode}
                  beforeAmount={selectedRev.expectedAmount}
                  afterAmount={
                    nextAmountInput.trim()
                      ? Number(nextAmountInput.trim())
                      : selectedRev.amount
                  }
                  rowVersion={selectedRev.rowVersion}
                  confirmLabelVi="Xác nhận (Confirmed)"
                  isSubmitting={submitting}
                  onConfirmWithBiometric={() =>
                    handleAdvanceMaturity(selectedRev, "confirm")
                  }
                  onCancel={() => setSelectedRev(null)}
                />
              ) : selectedRev.financialMaturity === "confirmed" ? (
                <BalanceImpactCard
                  titleVi="Chốt Doanh thu Thực tế (Confirmed → Actual)"
                  currencyCode={selectedRev.currencyCode}
                  beforeAmount={selectedRev.confirmedAmount ?? selectedRev.amount}
                  afterAmount={
                    nextAmountInput.trim()
                      ? Number(nextAmountInput.trim())
                      : selectedRev.amount
                  }
                  rowVersion={selectedRev.rowVersion}
                  confirmLabelVi="Chốt Thực tế (Actual)"
                  isSubmitting={submitting}
                  onConfirmWithBiometric={() =>
                    handleAdvanceMaturity(selectedRev, "actualize")
                  }
                  onCancel={() => setSelectedRev(null)}
                />
              ) : (
                <Text style={styles.actualDoneNote}>
                  ✓ Khoản doanh thu này đã ở trạng thái Thực tế (Actual). Sử dụng tab Điều chỉnh nếu cần thay đổi số tiền.
                </Text>
              )}
            </>
          ) : null}

          {detailTab === "adjust" ? (
            <View style={{ marginTop: 8 }}>
              <Text style={styles.label}>
                Chênh lệch điều chỉnh ({selectedRev.currencyCode}, nhập âm nếu giảm)
              </Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={adjustDelta}
                onChangeText={setAdjustDelta}
                placeholder="VD: 500000 hoặc -200000"
              />
              <Text style={styles.label}>Lý do điều chỉnh kiểm toán (Bắt buộc)</Text>
              <TextInput
                style={styles.input}
                value={adjustReason}
                onChangeText={setAdjustReason}
                placeholder="Nhập lý do điều chỉnh doanh thu..."
              />
              <BalanceImpactCard
                titleVi="Tác động Điều chỉnh Doanh thu (Before → After)"
                currencyCode={selectedRev.currencyCode}
                beforeAmount={selectedRev.amount}
                afterAmount={selectedRev.amount + (Number(adjustDelta) || 0)}
                rowVersion={selectedRev.rowVersion}
                confirmLabelVi="Xác nhận Điều chỉnh"
                isSubmitting={submitting}
                onConfirmWithBiometric={() => handleAdjustRevenue(selectedRev)}
                onCancel={() => setSelectedRev(null)}
              />
            </View>
          ) : null}

          {detailTab === "mapping" ? (
            <View style={{ marginTop: 8 }}>
              <Text style={styles.label}>Chọn các Vận đơn (Bills) nhận chia doanh thu:</Text>
              <View style={styles.chipRow}>
                {bills.slice(0, 6).map((b) => {
                  const active = mappingBillIds.includes(b.id);
                  return (
                    <TouchableOpacity
                      key={b.id}
                      style={[styles.chip, active && styles.chipActive]}
                      onPress={() => toggleMappingBill(b.id)}
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
                onPress={() => void handleCreateMapping(selectedRev)}
                disabled={submitting}
              >
                <Text style={styles.primaryBtnText}>
                  ✓ Thực hiện Chia/Gán Doanh thu
                </Text>
              </TouchableOpacity>
            </View>
          ) : null}

          <AttachmentGallery
            objectType="revenue"
            objectId={selectedRev.id}
            objectLabelVi={`Doanh thu ${selectedRev.revenueTypeCode ?? ""}`}
          />
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : (
        revenues.map((r) => (
          <CompactTxnCard
            key={r.id}
            primaryCode={`${r.revenueTypeCode ?? "REVENUE"} • ${
              r.billNo ?? "Bill"
            }`}
            statusLabelVi={t(
              `maturity.${r.financialMaturity}`,
              r.financialMaturity
            )}
            statusTone={
              r.financialMaturity === "actual"
                ? "success"
                : r.financialMaturity === "confirmed"
                ? "info"
                : "warning"
            }
            secondaryLabelVi={r.customerName ?? "Khách hàng theo vận đơn"}
            effectiveDate={r.effectiveDate}
            amount={r.amount}
            currencyCode={r.currencyCode}
            reportingAmount={r.baseAmount}
            reportingCurrencyCode={r.reportingCurrencyCode}
            fxStatusLabelVi={t(`fx.${r.fxStatus}`, r.fxStatus)}
            actionLabelVi="Xử lý & Chứng từ"
            onPress={() => {
              setSelectedRev(r);
              setNextAmountInput(String(r.amount));
            }}
            onActionPress={() => {
              setSelectedRev(r);
              setNextAmountInput(String(r.amount));
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
    backgroundColor: "#059669",
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
    borderColor: "#A7F3D0",
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
    borderColor: "#059669",
    backgroundColor: "#ECFDF5",
  },
  chipText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#475569",
  },
  chipTextActive: {
    color: "#047857",
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
    backgroundColor: "#ECFDF5",
    borderWidth: 1,
    borderColor: "#6EE7B7",
    paddingHorizontal: 12,
    paddingVertical: 11,
    borderRadius: 8,
  },
  secondaryBtnText: {
    color: "#047857",
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
