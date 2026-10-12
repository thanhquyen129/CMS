import React from "react";
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { formatMoney } from "@lcms/shared";
import { AttachmentGallery } from "../../src/components/AttachmentGallery";
import { BalanceImpactCard } from "../../src/components/BalanceImpactCard";
import { CompactTxnCard } from "../../src/components/CompactTxnCard";
import { useCostList } from "../../src/presentation/hooks/useCostList";

const COST_TYPES = ["FREIGHT", "TRUCKING", "HANDLING", "CUSTOMS", "SURCHARGE"] as const;
const MATURITY_FILTERS = [
  { key: "all", label: "Tất cả" },
  { key: "expected", label: "Dự kiến" },
  { key: "confirmed", label: "Đã xác nhận" },
  { key: "actual", label: "Thực tế" },
] as const;

export default function CostsScreen() {
  const c = useCostList();

  if (!c.canViewCost) {
    return (
      <View style={styles.sodBlocked}>
        <Text style={styles.sodTitle}>Giới hạn Quyền Truy cập Tài chính</Text>
        <Text style={styles.sodDesc}>
          Tài khoản hiện tại không có quyền truy cập phân hệ Chi phí &amp; Công nợ Phải trả.
        </Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={c.loading} onRefresh={() => void c.loadCosts()} />}
    >
      <View style={styles.headerRow}>
        <Text style={styles.heading}>Quản trị Chi phí ({c.costs.length})</Text>
        <TouchableOpacity style={styles.addBtn} onPress={() => c.setShowCreate(!c.showCreate)}>
          <Text style={styles.addBtnText}>{c.showCreate ? "✕ Đóng" : "+ Thêm Chi phí"}</Text>
        </TouchableOpacity>
      </View>

      {/* Filter Tabs */}
      <View style={styles.chipRow}>
        {MATURITY_FILTERS.map((f) => (
          <TouchableOpacity
            key={f.key}
            style={[styles.chip, c.filterMaturity === f.key && styles.chipActive]}
            onPress={() => c.setFilterMaturity(f.key)}
          >
            <Text style={[styles.chipText, c.filterMaturity === f.key && styles.chipTextActive]}>
              {f.label}
            </Text>
          </TouchableOpacity>
        ))}
      </View>

      {/* Create Form */}
      {c.showCreate && (
        <View style={styles.createCard}>
          <Text style={styles.detailTitle}>Ghi nhận Chi phí Mới (Expected Cost)</Text>
          <Text style={styles.label}>Loại quy thuộc</Text>
          <View style={styles.chipRow}>
            {(["direct", "shared"] as const).map((at) => (
              <TouchableOpacity
                key={at}
                style={[styles.chip, c.attributionType === at && styles.chipActive]}
                onPress={() => c.setAttributionType(at)}
              >
                <Text style={[styles.chipText, c.attributionType === at && styles.chipTextActive]}>
                  {at === "direct" ? "Trực tiếp theo Bill (Direct)" : "Chi phí dùng chung (Shared)"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <Text style={styles.label}>Nhóm Chi phí</Text>
          <View style={styles.chipRow}>
            {COST_TYPES.map((code) => (
              <TouchableOpacity
                key={code}
                style={[styles.chip, c.costTypeCode === code && styles.chipActive]}
                onPress={() => c.setCostTypeCode(code)}
              >
                <Text style={[styles.chipText, c.costTypeCode === code && styles.chipTextActive]}>{code}</Text>
              </TouchableOpacity>
            ))}
          </View>

          {c.attributionType === "direct" && c.bills.length > 0 && (
            <>
              <Text style={styles.label}>Chọn Vận đơn (Bill)</Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                <View style={styles.chipRow}>
                  {c.bills.slice(0, 8).map((b) => (
                    <TouchableOpacity
                      key={b.id}
                      style={[styles.chip, c.selectedBillId === b.id && styles.chipActive]}
                      onPress={() => c.setSelectedBillId(b.id)}
                    >
                      <Text style={[styles.chipText, c.selectedBillId === b.id && styles.chipTextActive]}>
                        {b.billNo}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </ScrollView>
            </>
          )}

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Số tiền nguyên tệ</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={c.createAmount}
                onChangeText={c.setCreateAmount}
                placeholder="VD: 1500000"
              />
            </View>
            <View>
              <Text style={styles.label}>Tiền tệ</Text>
              <View style={styles.chipRow}>
                {(["VND", "USD"] as const).map((cur) => (
                  <TouchableOpacity
                    key={cur}
                    style={[styles.chip, c.createCurrency === cur && styles.chipActive]}
                    onPress={() => c.setCreateCurrency(cur)}
                  >
                    <Text style={[styles.chipText, c.createCurrency === cur && styles.chipTextActive]}>{cur}</Text>
                  </TouchableOpacity>
                ))}
              </View>
            </View>
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity style={styles.primaryBtn} onPress={() => void c.handleCreateCost(false)} disabled={c.submitting}>
              <Text style={styles.primaryBtnText}>✓ Tạo Chi phí</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.secondaryBtn} onPress={() => void c.handleCreateCost(true)} disabled={c.submitting}>
              <Text style={styles.secondaryBtnText}>📡 Lưu Offline</Text>
            </TouchableOpacity>
          </View>
        </View>
      )}

      {/* Selected Cost Detail Sheet */}
      {c.selectedCost && (
        <View style={styles.detailSheet}>
          <Text style={styles.detailTitle}>
            Xử lý Chi phí: {c.selectedCost.costTypeCode ?? "CHI PHÍ"} ({formatMoney(c.selectedCost.amount, c.selectedCost.currencyCode)})
          </Text>

          <View style={styles.chipRow}>
            {(["maturity", "adjust", "allocate"] as const).map((tab, idx) => (
              <TouchableOpacity
                key={tab}
                style={[styles.chip, c.detailTab === tab && styles.chipActive]}
                onPress={() => c.setDetailTab(tab)}
              >
                <Text style={[styles.chipText, c.detailTab === tab && styles.chipTextActive]}>
                  {idx + 1}. {tab === "maturity" ? "Chuyển trạng thái" : tab === "adjust" ? "Điều chỉnh (+/-)" : "Phân bổ cho Bill"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {c.detailTab === "maturity" && (
            <>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={c.nextAmountInput}
                onChangeText={c.setNextAmountInput}
                placeholder={`Số tiền mới (${c.selectedCost.currencyCode})`}
              />
              {c.selectedCost.financialMaturity === "expected" ? (
                <BalanceImpactCard
                  titleVi="Xác nhận Chi phí (Expected → Confirmed)"
                  currencyCode={c.selectedCost.currencyCode}
                  beforeAmount={c.selectedCost.expectedAmount}
                  afterAmount={c.nextAmountInput.trim() ? Number(c.nextAmountInput.trim()) : c.selectedCost.amount}
                  rowVersion={c.selectedCost.rowVersion}
                  confirmLabelVi="Xác nhận (Confirmed)"
                  isSubmitting={c.submitting}
                  onConfirmWithBiometric={() => c.handleAdvanceMaturity(c.selectedCost!, "confirm")}
                  onCancel={() => c.handleSelectCost(null)}
                />
              ) : c.selectedCost.financialMaturity === "confirmed" ? (
                <BalanceImpactCard
                  titleVi="Chốt Chi phí Thực tế (Confirmed → Actual)"
                  currencyCode={c.selectedCost.currencyCode}
                  beforeAmount={c.selectedCost.confirmedAmount ?? c.selectedCost.amount}
                  afterAmount={c.nextAmountInput.trim() ? Number(c.nextAmountInput.trim()) : c.selectedCost.amount}
                  rowVersion={c.selectedCost.rowVersion}
                  confirmLabelVi="Chốt Thực tế (Actual)"
                  isSubmitting={c.submitting}
                  onConfirmWithBiometric={() => c.handleAdvanceMaturity(c.selectedCost!, "actualize")}
                  onCancel={() => c.handleSelectCost(null)}
                />
              ) : (
                <Text style={styles.actualDoneNote}>✓ Đã ở trạng thái Thực tế (Actual). Dùng tab Điều chỉnh nếu cần đổi số tiền.</Text>
              )}
            </>
          )}

          {c.detailTab === "adjust" && (
            <View style={{ marginTop: 8 }}>
              <Text style={styles.label}>Chênh lệch điều chỉnh ({c.selectedCost.currencyCode})</Text>
              <TextInput style={styles.input} keyboardType="numeric" value={c.adjustDelta} onChangeText={c.setAdjustDelta} placeholder="VD: 250000 hoặc -100000" />
              <Text style={styles.label}>Lý do điều chỉnh kiểm toán (Bắt buộc)</Text>
              <TextInput style={styles.input} value={c.adjustReason} onChangeText={c.setAdjustReason} placeholder="Nhập lý do điều chỉnh..." />
              <BalanceImpactCard
                titleVi="Tác động Điều chỉnh Chi phí"
                currencyCode={c.selectedCost.currencyCode}
                beforeAmount={c.selectedCost.amount}
                afterAmount={c.selectedCost.amount + (Number(c.adjustDelta) || 0)}
                rowVersion={c.selectedCost.rowVersion}
                confirmLabelVi="Xác nhận Điều chỉnh"
                isSubmitting={c.submitting}
                onConfirmWithBiometric={() => c.handleAdjustCost(c.selectedCost!)}
                onCancel={() => c.handleSelectCost(null)}
              />
            </View>
          )}

          {c.detailTab === "allocate" && (
            <View style={{ marginTop: 8 }}>
              <Text style={styles.label}>Tiêu thức phân bổ</Text>
              <View style={styles.chipRow}>
                {(["equal", "weight", "manual"] as const).map((b) => (
                  <TouchableOpacity key={b} style={[styles.chip, c.allocationBasis === b && styles.chipActive]} onPress={() => c.setAllocationBasis(b)}>
                    <Text style={[styles.chipText, c.allocationBasis === b && styles.chipTextActive]}>
                      {b === "equal" ? "Chia đều (Equal)" : b === "weight" ? "Theo CW" : "Thủ công"}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
              <Text style={styles.label}>Chọn Vận đơn nhận phân bổ:</Text>
              <View style={styles.chipRow}>
                {c.bills.slice(0, 6).map((b) => {
                  const active = c.allocBillIds.includes(b.id);
                  return (
                    <TouchableOpacity key={b.id} style={[styles.chip, active && styles.chipActive]} onPress={() => c.toggleAllocBill(b.id)}>
                      <Text style={[styles.chipText, active && styles.chipTextActive]}>{active ? "✓ " : ""}{b.billNo}</Text>
                    </TouchableOpacity>
                  );
                })}
              </View>
              <TouchableOpacity style={[styles.primaryBtn, { marginTop: 10 }]} onPress={() => void c.handleAllocateSharedCost(c.selectedCost!)} disabled={c.submitting}>
                <Text style={styles.primaryBtnText}>✓ Thực hiện Phân bổ Chi phí</Text>
              </TouchableOpacity>
            </View>
          )}

          <AttachmentGallery objectType="cost" objectId={c.selectedCost.id} objectLabelVi={`Chi phí ${c.selectedCost.costTypeCode ?? ""}`} />
        </View>
      )}

      {/* Cost List */}
      {c.loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : (
        c.costs.map((item) => (
          <CompactTxnCard
            key={item.id}
            primaryCode={`${item.costTypeCode ?? "COST"} • ${item.billNo ?? c.t(`attribution.${item.attributionType}`, item.attributionType)}`}
            statusLabelVi={c.t(`maturity.${item.financialMaturity}`, item.financialMaturity)}
            statusTone={item.financialMaturity === "actual" ? "success" : item.financialMaturity === "confirmed" ? "info" : "warning"}
            secondaryLabelVi={item.vendorName ?? c.t(`attribution.${item.attributionType}`, item.attributionType)}
            effectiveDate={item.effectiveDate}
            amount={item.amount}
            currencyCode={item.currencyCode}
            reportingAmount={item.baseAmount}
            reportingCurrencyCode={item.reportingCurrencyCode}
            fxStatusLabelVi={c.t(`fx.${item.fxStatus}`, item.fxStatus)}
            actionLabelVi="Xử lý & Chứng từ"
            onPress={() => c.handleSelectCost(item)}
            onActionPress={() => c.handleSelectCost(item)}
          />
        ))
      )}
      <View style={{ height: 32 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#F8FAFC", padding: 16 },
  headerRow: { flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: 12 },
  heading: { fontSize: 14, fontWeight: "800", color: "#0F172A" },
  addBtn: { backgroundColor: "#2563EB", paddingHorizontal: 12, paddingVertical: 7, borderRadius: 8 },
  addBtnText: { color: "#FFFFFF", fontSize: 12, fontWeight: "700" },
  createCard: { backgroundColor: "#FFFFFF", borderRadius: 12, padding: 14, borderWidth: 1, borderColor: "#BFDBFE", marginBottom: 14 },
  sodBlocked: { flex: 1, justifyContent: "center", alignItems: "center", padding: 24, backgroundColor: "#F8FAFC" },
  sodTitle: { fontSize: 16, fontWeight: "800", color: "#DC2626" },
  sodDesc: { fontSize: 13, color: "#475569", textAlign: "center", marginTop: 8 },
  detailSheet: { backgroundColor: "#FFFFFF", borderRadius: 12, padding: 14, borderWidth: 1, borderColor: "#CBD5E1", marginBottom: 16 },
  detailTitle: { fontSize: 15, fontWeight: "800", color: "#0F172A", marginBottom: 8 },
  label: { fontSize: 12, fontWeight: "700", color: "#334155", marginTop: 6, marginBottom: 4 },
  input: { borderWidth: 1, borderColor: "#CBD5E1", borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8, fontSize: 13, backgroundColor: "#F8FAFC", color: "#0F172A", marginBottom: 6 },
  twoCol: { flexDirection: "row", gap: 10, alignItems: "flex-end" },
  chipRow: { flexDirection: "row", flexWrap: "wrap", gap: 6, marginVertical: 4 },
  chip: { borderWidth: 1, borderColor: "#CBD5E1", borderRadius: 8, paddingHorizontal: 10, paddingVertical: 6, backgroundColor: "#F8FAFC" },
  chipActive: { borderColor: "#2563EB", backgroundColor: "#EFF6FF" },
  chipText: { fontSize: 12, fontWeight: "600", color: "#475569" },
  chipTextActive: { color: "#1D4ED8", fontWeight: "700" },
  actionRow: { flexDirection: "row", gap: 8, marginTop: 10 },
  primaryBtn: { flex: 1, backgroundColor: "#0F172A", paddingVertical: 11, borderRadius: 8, alignItems: "center" },
  primaryBtnText: { color: "#FFFFFF", fontSize: 13, fontWeight: "700" },
  secondaryBtn: { backgroundColor: "#EFF6FF", borderWidth: 1, borderColor: "#93C5FD", paddingHorizontal: 12, paddingVertical: 11, borderRadius: 8 },
  secondaryBtnText: { color: "#1D4ED8", fontSize: 12, fontWeight: "700" },
  actualDoneNote: { fontSize: 12, color: "#047857", fontWeight: "600", marginVertical: 8 },
});
