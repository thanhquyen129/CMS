import React from "react";
import {
  ActivityIndicator,
  Image,
  Modal,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { BillListItemDto, formatMoney } from "@lcms/shared";
import { useDriverQuickExpense } from "../hooks/useDriverQuickExpense";

export interface DriverQuickExpenseSheetProps {
  visible: boolean;
  onClose: () => void;
  availableBills: BillListItemDto[];
  defaultBillId?: string;
}

const CATEGORIES = [
  { key: "fuel", label: "Xăng dầu", icon: "⛽" },
  { key: "toll_bot", label: "Vé BOT", icon: "🛣️" },
  { key: "repair", label: "Sửa xe", icon: "🔧" },
  { key: "parking_port", label: "Bến bãi", icon: "🅿️" },
] as const;

const QUICK_LITERS = [30, 50, 70, 100];
const QUICK_BOT_AMOUNTS = [35000, 70000, 140000];

export const DriverQuickExpenseSheet: React.FC<DriverQuickExpenseSheetProps> = ({
  visible,
  onClose,
  availableBills,
  defaultBillId,
}) => {
  const expense = useDriverQuickExpense({
    initialBillId: defaultBillId || (availableBills[0]?.id ?? ""),
    onSuccess: onClose,
  });

  return (
    <Modal visible={visible} animationType="slide" transparent>
      <View style={styles.overlay}>
        <View style={styles.sheetContainer}>
          {/* Sheet Header */}
          <View style={styles.sheetHeader}>
            <View>
              <Text style={styles.sheetTitle}>Nhập Chi Phí Nhanh Tài Xế</Text>
              <Text style={styles.sheetSubtitle}>Tối ưu thao tác 1 tay • Hoàn tất &lt; 15 giây</Text>
            </View>
            <TouchableOpacity style={styles.closeBtn} onPress={onClose}>
              <Text style={styles.closeBtnText}>✕</Text>
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.scrollArea} showsVerticalScrollIndicator={false}>
            {/* 4 Nút chọn nhanh loại chi phí */}
            <Text style={styles.sectionLabel}>Loại chi phí dọc đường</Text>
            <View style={styles.categoryRow}>
              {CATEGORIES.map((cat) => {
                const active = expense.category === cat.key;
                return (
                  <TouchableOpacity
                    key={cat.key}
                    style={[styles.categoryBtn, active && styles.categoryBtnActive]}
                    onPress={() => expense.setCategory(cat.key)}
                  >
                    <Text style={styles.categoryIcon}>{cat.icon}</Text>
                    <Text style={[styles.categoryText, active && styles.categoryTextActive]}>
                      {cat.label}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>

            {/* Chọn Bill vận đơn liên kết */}
            <Text style={styles.sectionLabel}>Vận đơn đang chạy (Bill)</Text>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.billScroll}>
              <View style={styles.chipRow}>
                {availableBills.map((b) => {
                  const active = expense.selectedBillId === b.id;
                  return (
                    <TouchableOpacity
                      key={b.id}
                      style={[styles.billChip, active && styles.billChipActive]}
                      onPress={() => expense.setSelectedBillId(b.id)}
                    >
                      <Text style={[styles.billChipText, active && styles.billChipTextActive]}>
                        {active ? "✓ " : ""}{b.billNo}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>
            </ScrollView>

            {/* Form chi tiết theo Loại */}
            {expense.category === "fuel" ? (
              <View style={styles.formBox}>
                <View style={styles.twoCol}>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.inputLabel}>Số lít đổ (Lít)</Text>
                    <TextInput
                      style={styles.numericInput}
                      keyboardType="numeric"
                      value={expense.liters}
                      onChangeText={expense.setLiters}
                      placeholder="VD: 65.5"
                    />
                  </View>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.inputLabel}>Đơn giá (đ/L)</Text>
                    <TextInput
                      style={styles.numericInput}
                      keyboardType="numeric"
                      value={expense.pricePerLiter}
                      onChangeText={expense.setPricePerLiter}
                      placeholder="23500"
                    />
                  </View>
                </View>

                {/* Chọn nhanh số lít */}
                <View style={styles.quickPresetRow}>
                  {QUICK_LITERS.map((lit) => (
                    <TouchableOpacity
                      key={lit}
                      style={styles.presetChip}
                      onPress={() => expense.setLiters(String(lit))}
                    >
                      <Text style={styles.presetChipText}>+{lit} L</Text>
                    </TouchableOpacity>
                  ))}
                </View>

                <Text style={styles.inputLabel}>Tên cây xăng</Text>
                <TextInput
                  style={styles.textInput}
                  value={expense.stationName}
                  onChangeText={expense.setStationName}
                  placeholder="VD: Petrolimex Cổng Cảng Cát Lái"
                />
              </View>
            ) : (
              <View style={styles.formBox}>
                <Text style={styles.inputLabel}>Số tiền chi phí (VND)</Text>
                <TextInput
                  style={styles.numericInput}
                  keyboardType="numeric"
                  value={expense.directAmount}
                  onChangeText={expense.setDirectAmount}
                  placeholder="VD: 140000"
                />

                {expense.category === "toll_bot" && (
                  <View style={styles.quickPresetRow}>
                    {QUICK_BOT_AMOUNTS.map((amt) => (
                      <TouchableOpacity
                        key={amt}
                        style={styles.presetChip}
                        onPress={() => expense.setDirectAmount(String(amt))}
                      >
                        <Text style={styles.presetChipText}>{amt / 1000}k</Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                )}

                <Text style={styles.inputLabel}>Tên trạm / Tiệm sửa xe / Bãi xe</Text>
                <TextInput
                  style={styles.textInput}
                  value={expense.stationName}
                  onChangeText={expense.setStationName}
                  placeholder="Nhập tên trạm hoặc điểm chi trả..."
                />
              </View>
            )}

            {/* Chỉ số ODO */}
            <View style={{ marginTop: 10 }}>
              <Text style={styles.inputLabel}>Chỉ số ODO đồng hồ xe (km - Không bắt buộc)</Text>
              <TextInput
                style={styles.textInput}
                keyboardType="numeric"
                value={expense.odometerKm}
                onChangeText={expense.setOdometerKm}
                placeholder="VD: 145200"
              />
            </View>

            {/* Ảnh hóa đơn / Phiếu xuất kho */}
            <View style={styles.receiptSection}>
              <Text style={styles.inputLabel}>Ảnh chụp hóa đơn / Vé BOT</Text>
              {expense.receiptLocalUri ? (
                <View style={styles.receiptPreviewBox}>
                  <Image source={{ uri: expense.receiptLocalUri }} style={styles.receiptThumb} />
                  <View style={{ flex: 1, marginLeft: 10 }}>
                    <Text style={styles.receiptAttachedText}>✓ Đã nén &amp; lưu bộ nhớ an toàn</Text>
                    <TouchableOpacity style={styles.deletePhotoBtn} onPress={expense.handleClearReceipt}>
                      <Text style={styles.deletePhotoText}>✕ Chụp lại ảnh khác</Text>
                    </TouchableOpacity>
                  </View>
                </View>
              ) : (
                <TouchableOpacity
                  style={styles.cameraBtn}
                  onPress={expense.handlePickAndCompressReceipt}
                  disabled={expense.isCompressing}
                >
                  {expense.isCompressing ? (
                    <ActivityIndicator color="#2563EB" />
                  ) : (
                    <>
                      <Text style={styles.cameraIcon}>📷</Text>
                      <Text style={styles.cameraText}>Chụp ảnh hóa đơn (Tự nén tối ưu)</Text>
                    </>
                  )}
                </TouchableOpacity>
              )}
            </View>

            {/* Hiển thị thành tiền trực quan */}
            <View style={styles.totalSummaryCard}>
              <Text style={styles.totalSummaryLabel}>TỔNG TIỀN QUY ĐỔI:</Text>
              <Text style={styles.totalSummaryAmount}>
                {formatMoney(expense.calculatedTotalAmount, "VND")}
              </Text>
            </View>
          </ScrollView>

          {/* Action Bar dưới cùng */}
          <View style={styles.footerBar}>
            <TouchableOpacity
              style={styles.primaryActionBtn}
              onPress={() => void expense.handleSubmitExpense(false)}
              disabled={expense.isSubmitting}
            >
              {expense.isSubmitting ? (
                <ActivityIndicator color="#FFFFFF" />
              ) : (
                <Text style={styles.primaryActionText}>✓ Gửi Chi Phí (&lt; 15s)</Text>
              )}
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.offlineActionBtn}
              onPress={() => void expense.handleSubmitExpense(true)}
              disabled={expense.isSubmitting}
            >
              <Text style={styles.offlineActionText}>📡 Lưu Ngoại Tuyến</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: "rgba(15, 23, 42, 0.65)",
    justifyContent: "flex-end",
  },
  sheetContainer: {
    backgroundColor: "#FFFFFF",
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    maxHeight: "92%",
    paddingBottom: 24,
  },
  sheetHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingHorizontal: 18,
    paddingTop: 16,
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: "#E2E8F0",
  },
  sheetTitle: {
    fontSize: 16,
    fontWeight: "800",
    color: "#0F172A",
  },
  sheetSubtitle: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 2,
  },
  closeBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: "#F1F5F9",
    alignItems: "center",
    justifyContent: "center",
  },
  closeBtnText: {
    fontSize: 15,
    fontWeight: "700",
    color: "#475569",
  },
  scrollArea: {
    paddingHorizontal: 16,
    paddingTop: 12,
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 6,
    marginTop: 8,
  },
  categoryRow: {
    flexDirection: "row",
    gap: 8,
  },
  categoryBtn: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    borderWidth: 1.5,
    borderColor: "#CBD5E1",
    borderRadius: 12,
    paddingVertical: 10,
    alignItems: "center",
  },
  categoryBtnActive: {
    borderColor: "#2563EB",
    backgroundColor: "#EFF6FF",
  },
  categoryIcon: {
    fontSize: 22,
    marginBottom: 4,
  },
  categoryText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#475569",
  },
  categoryTextActive: {
    color: "#1D4ED8",
  },
  billScroll: {
    marginBottom: 8,
  },
  chipRow: {
    flexDirection: "row",
    gap: 6,
  },
  billChip: {
    backgroundColor: "#F1F5F9",
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#CBD5E1",
  },
  billChipActive: {
    backgroundColor: "#EFF6FF",
    borderColor: "#2563EB",
  },
  billChipText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#334155",
  },
  billChipTextActive: {
    color: "#1D4ED8",
    fontWeight: "700",
  },
  formBox: {
    backgroundColor: "#F8FAFC",
    borderRadius: 12,
    padding: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginTop: 6,
  },
  twoCol: {
    flexDirection: "row",
    gap: 10,
  },
  inputLabel: {
    fontSize: 11,
    fontWeight: "700",
    color: "#475569",
    marginBottom: 4,
  },
  numericInput: {
    backgroundColor: "#FFFFFF",
    borderWidth: 1.5,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 15,
    fontWeight: "700",
    color: "#0F172A",
  },
  textInput: {
    backgroundColor: "#FFFFFF",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 13,
    color: "#0F172A",
    marginBottom: 6,
  },
  quickPresetRow: {
    flexDirection: "row",
    gap: 6,
    marginVertical: 8,
  },
  presetChip: {
    backgroundColor: "#E2E8F0",
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 6,
  },
  presetChipText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#334155",
  },
  receiptSection: {
    marginTop: 10,
  },
  cameraBtn: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    borderWidth: 1.5,
    borderColor: "#93C5FD",
    borderStyle: "dashed",
    borderRadius: 10,
    paddingVertical: 14,
    backgroundColor: "#EFF6FF",
    gap: 8,
  },
  cameraIcon: {
    fontSize: 18,
  },
  cameraText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#2563EB",
  },
  receiptPreviewBox: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#F1F5F9",
    padding: 8,
    borderRadius: 10,
  },
  receiptThumb: {
    width: 60,
    height: 60,
    borderRadius: 6,
    backgroundColor: "#CBD5E1",
  },
  receiptAttachedText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#059669",
  },
  deletePhotoBtn: {
    marginTop: 6,
  },
  deletePhotoText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#DC2626",
  },
  totalSummaryCard: {
    backgroundColor: "#ECFDF5",
    borderWidth: 1.5,
    borderColor: "#A7F3D0",
    borderRadius: 12,
    padding: 12,
    alignItems: "center",
    marginTop: 14,
    marginBottom: 10,
  },
  totalSummaryLabel: {
    fontSize: 11,
    fontWeight: "800",
    color: "#047857",
  },
  totalSummaryAmount: {
    fontSize: 22,
    fontWeight: "900",
    color: "#065F46",
    marginTop: 2,
  },
  footerBar: {
    flexDirection: "row",
    gap: 8,
    paddingHorizontal: 16,
    paddingTop: 10,
  },
  primaryActionBtn: {
    flex: 1,
    backgroundColor: "#0F172A",
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: "center",
  },
  primaryActionText: {
    fontSize: 14,
    fontWeight: "800",
    color: "#FFFFFF",
  },
  offlineActionBtn: {
    backgroundColor: "#EFF6FF",
    borderWidth: 1.5,
    borderColor: "#93C5FD",
    paddingHorizontal: 14,
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: "center",
  },
  offlineActionText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#1D4ED8",
  },
});

