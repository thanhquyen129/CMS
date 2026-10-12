import React, { useState } from "react";
import {
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  Vibration,
  View,
} from "react-native";
import { CameraView, useCameraPermissions } from "expo-camera";
import { useRouter } from "expo-router";
import { BillListItemDto } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";

interface BatchScanItem {
  code: string;
  scannedAt: string;
}

export default function ScannerScreen() {
  const router = useRouter();
  const [permission, requestPermission] = useCameraPermissions();
  const [scannerActive, setScannerActive] = useState<boolean>(true);
  const [manualCode, setManualCode] = useState<string>("");
  const [lastScanned, setLastScanned] = useState<string | null>(null);
  const [lastScannedTime, setLastScannedTime] = useState<number>(0);

  // Tính năng mới cho kho tối/thùng xe & quét theo lô
  const [isTorchOn, setIsTorchOn] = useState<boolean>(false);
  const [isBatchMode, setIsBatchMode] = useState<boolean>(false);
  const [batchScans, setBatchScans] = useState<BatchScanItem[]>([]);

  const lookupBillByCode = async (rawCode: string) => {
    const code = rawCode.trim();
    if (!code) return;
    setLastScanned(code);
    setScannerActive(false);

    try {
      const res = await apiRequest<
        BillListItemDto[] | { items: BillListItemDto[] }
      >("/api/bills");
      const list = Array.isArray(res) ? res : res?.items ?? [];
      const match = list.find(
        (b) =>
          b.billNo.toLowerCase() === code.toLowerCase() ||
          (b.masterBillNo ?? "").toLowerCase() === code.toLowerCase() ||
          (b.customerReference ?? "").toLowerCase() === code.toLowerCase()
      );

      if (match) {
        router.push(`/bills/${match.id}`);
      } else {
        Alert.alert(
          "Chưa tìm thấy Vận đơn",
          `Không tìm thấy Bill khớp với mã "${code}". Bạn có muốn thử quét lại không?`,
          [{ text: "Quét lại", onPress: () => setScannerActive(true) }]
        );
      }
    } catch (err) {
      Alert.alert(
        "Lỗi tra cứu mã vận đơn",
        err instanceof Error ? err.message : "Không thể kết nối máy chủ.",
        [{ text: "Thử lại", onPress: () => setScannerActive(true) }]
      );
    }
  };

  const handleBarcodeScanned = (data: string) => {
    if (!data || !scannerActive) return;

    const now = Date.now();
    // Chống quét trùng liên tục cùng 1 mã trong vòng 2 giây
    if (data === lastScanned && now - lastScannedTime < 2000) {
      return;
    }

    setLastScanned(data);
    setLastScannedTime(now);

    try {
      Vibration.vibrate(80);
    } catch {
      // Bỏ qua nếu thiết bị không hỗ trợ rung
    }

    if (isBatchMode) {
      // Chế độ theo lô: Thêm vào danh sách và tiếp tục mở camera quét liên tục
      setBatchScans((prev) => {
        if (prev.some((item) => item.code.toLowerCase() === data.toLowerCase())) {
          return prev;
        }
        return [
          {
            code: data,
            scannedAt: new Date().toLocaleTimeString("vi-VN"),
          },
          ...prev,
        ];
      });
    } else {
      // Chế độ đơn lẻ: Tra cứu và mở chi tiết vận đơn ngay
      void lookupBillByCode(data);
    }
  };

  const handleClearBatch = () => {
    Alert.alert("Xóa danh sách lô", "Bạn có chắc chắn muốn xóa toàn bộ kiện hàng đã quét?", [
      { text: "Hủy", style: "cancel" },
      { text: "Xóa hết", style: "destructive", onPress: () => setBatchScans([]) },
    ]);
  };

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.heading}>
        Quét Mã Vạch / QR Vận đơn (HAWB / MAWB / Bill No)
      </Text>
      <Text style={styles.subheading}>
        Hướng camera vào mã vạch trên phiếu giao nhận kho/cảng để mở hồ sơ hoặc quét liên tục theo lô.
      </Text>

      {/* Control Action Bar: Bật/Tắt Flash & Chế độ Quét */}
      <View style={styles.controlsBar}>
        <TouchableOpacity
          style={[styles.controlBtn, isTorchOn && styles.controlBtnTorchActive]}
          onPress={() => setIsTorchOn((prev) => !prev)}
          activeOpacity={0.8}
        >
          <Text style={styles.controlBtnIcon}>{isTorchOn ? "🔦" : "💡"}</Text>
          <Text
            style={[
              styles.controlBtnText,
              isTorchOn && styles.controlBtnTextActive,
            ]}
          >
            Flash: {isTorchOn ? "BẬT" : "TẮT"}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.controlBtn, isBatchMode && styles.controlBtnBatchActive]}
          onPress={() => {
            setIsBatchMode((prev) => !prev);
            setScannerActive(true);
          }}
          activeOpacity={0.8}
        >
          <Text style={styles.controlBtnIcon}>{isBatchMode ? "📦" : "🔍"}</Text>
          <Text
            style={[
              styles.controlBtnText,
              isBatchMode && styles.controlBtnTextActive,
            ]}
          >
            Chế độ: {isBatchMode ? "Theo lô (Batch)" : "Từng đơn"}
          </Text>
        </TouchableOpacity>
      </View>

      {!permission?.granted ? (
        <View style={styles.permCard}>
          <Text style={styles.permText}>
            Ứng dụng cần quyền truy cập Camera để quét mã vận đơn và chụp ảnh chứng từ hiện trường.
          </Text>
          <TouchableOpacity style={styles.permBtn} onPress={requestPermission}>
            <Text style={styles.permBtnText}>Cấp quyền Camera</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <View style={styles.cameraBox}>
          {scannerActive ? (
            <CameraView
              style={styles.camera}
              enableTorch={isTorchOn}
              barcodeScannerSettings={{
                barcodeTypes: ["qr", "code128", "code39", "ean13", "pdf417"],
              }}
              onBarcodeScanned={({ data }) => {
                if (data) {
                  handleBarcodeScanned(data);
                }
              }}
            />
          ) : (
            <View style={styles.pausedOverlay}>
              <Text style={styles.pausedText}>
                Đã quét mã: {lastScanned}
              </Text>
              <TouchableOpacity
                style={styles.resumeBtn}
                onPress={() => setScannerActive(true)}
              >
                <Text style={styles.resumeBtnText}>Quét mã tiếp theo</Text>
              </TouchableOpacity>
            </View>
          )}

          {/* Badge hiển thị góc camera khi bật chế độ theo lô */}
          {isBatchMode && (
            <View style={styles.batchOverlayBadge}>
              <Text style={styles.batchOverlayBadgeText}>
                Đang quét lô: {batchScans.length} kiện
              </Text>
            </View>
          )}
        </View>
      )}

      {/* Hiển thị danh sách kiện hàng quét theo lô */}
      {isBatchMode && (
        <View style={styles.batchCard}>
          <View style={styles.batchHeaderRow}>
            <View>
              <Text style={styles.batchTitle}>Danh sách Quét theo Lô</Text>
              <Text style={styles.batchSubtitle}>
                Tổng cộng: {batchScans.length} mã đã nhận diện
              </Text>
            </View>
            {batchScans.length > 0 && (
              <TouchableOpacity
                style={styles.clearBatchBtn}
                onPress={handleClearBatch}
              >
                <Text style={styles.clearBatchText}>Xóa lô</Text>
              </TouchableOpacity>
            )}
          </View>

          {batchScans.length === 0 ? (
            <Text style={styles.emptyBatchText}>
              Chưa có kiện hàng nào. Hướng camera vào mã vạch để tự động thêm vào lô.
            </Text>
          ) : (
            <View style={styles.batchListWrap}>
              {batchScans.slice(0, 10).map((item, idx) => (
                <View key={`${item.code}-${idx}`} style={styles.batchItemRow}>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.batchItemCode}>
                      #{batchScans.length - idx} • {item.code}
                    </Text>
                    <Text style={styles.batchItemTime}>
                      Quét lúc: {item.scannedAt}
                    </Text>
                  </View>
                  <TouchableOpacity
                    style={styles.openBillMiniBtn}
                    onPress={() => void lookupBillByCode(item.code)}
                  >
                    <Text style={styles.openBillMiniText}>Mở Bill</Text>
                  </TouchableOpacity>
                </View>
              ))}
            </View>
          )}
        </View>
      )}

      <View style={styles.manualCard}>
        <Text style={styles.manualLabel}>
          Hoặc nhập tay số Bill / HAWB / MAWB khi tem bị mờ:
        </Text>
        <View style={styles.manualRow}>
          <TextInput
            style={styles.input}
            placeholder="Nhập số Bill / HAWB..."
            value={manualCode}
            onChangeText={setManualCode}
            autoCapitalize="characters"
          />
          <TouchableOpacity
            style={styles.searchBtn}
            onPress={() => void lookupBillByCode(manualCode)}
          >
            <Text style={styles.searchBtnText}>Mở Bill</Text>
          </TouchableOpacity>
        </View>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 16,
  },
  heading: {
    fontSize: 16,
    fontWeight: "800",
    color: "#0F172A",
  },
  subheading: {
    fontSize: 12,
    color: "#475569",
    marginTop: 4,
    marginBottom: 12,
  },
  controlsBar: {
    flexDirection: "row",
    gap: 8,
    marginBottom: 12,
  },
  controlBtn: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#FFFFFF",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingVertical: 9,
    paddingHorizontal: 10,
    gap: 6,
  },
  controlBtnTorchActive: {
    backgroundColor: "#FEF3C7",
    borderColor: "#F59E0B",
  },
  controlBtnBatchActive: {
    backgroundColor: "#EFF6FF",
    borderColor: "#3B82F6",
  },
  controlBtnIcon: {
    fontSize: 14,
  },
  controlBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
  },
  controlBtnTextActive: {
    color: "#0F172A",
  },
  permCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 20,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    alignItems: "center",
    marginBottom: 16,
  },
  permText: {
    fontSize: 13,
    color: "#334155",
    textAlign: "center",
    marginBottom: 12,
  },
  permBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 18,
    paddingVertical: 10,
    borderRadius: 8,
  },
  permBtnText: {
    color: "#FFFFFF",
    fontWeight: "700",
    fontSize: 13,
  },
  cameraBox: {
    height: 280,
    borderRadius: 14,
    overflow: "hidden",
    backgroundColor: "#0F172A",
    marginBottom: 14,
    position: "relative",
  },
  camera: {
    flex: 1,
  },
  batchOverlayBadge: {
    position: "absolute",
    top: 10,
    left: 10,
    backgroundColor: "rgba(15, 23, 42, 0.8)",
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: "#60A5FA",
  },
  batchOverlayBadgeText: {
    color: "#60A5FA",
    fontSize: 11,
    fontWeight: "700",
  },
  pausedOverlay: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 20,
  },
  pausedText: {
    color: "#FFFFFF",
    fontSize: 15,
    fontWeight: "700",
    marginBottom: 12,
  },
  resumeBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 8,
  },
  resumeBtnText: {
    color: "#FFFFFF",
    fontWeight: "700",
    fontSize: 13,
  },
  batchCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#BFDBFE",
    marginBottom: 14,
  },
  batchHeaderRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 10,
  },
  batchTitle: {
    fontSize: 14,
    fontWeight: "800",
    color: "#1E40AF",
  },
  batchSubtitle: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 2,
  },
  clearBatchBtn: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
    backgroundColor: "#FEE2E2",
  },
  clearBatchText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#DC2626",
  },
  emptyBatchText: {
    fontSize: 12,
    color: "#64748B",
    fontStyle: "italic",
    textAlign: "center",
    paddingVertical: 8,
  },
  batchListWrap: {
    gap: 6,
  },
  batchItemRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    backgroundColor: "#F8FAFC",
    paddingHorizontal: 10,
    paddingVertical: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  batchItemCode: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  batchItemTime: {
    fontSize: 10,
    color: "#64748B",
    marginTop: 2,
  },
  openBillMiniBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 6,
  },
  openBillMiniText: {
    color: "#FFFFFF",
    fontSize: 11,
    fontWeight: "700",
  },
  manualCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 24,
  },
  manualLabel: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
    marginBottom: 8,
  },
  manualRow: {
    flexDirection: "row",
    gap: 8,
  },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 9,
    fontSize: 14,
    backgroundColor: "#F8FAFC",
  },
  searchBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 16,
    justifyContent: "center",
    borderRadius: 8,
  },
  searchBtnText: {
    color: "#FFFFFF",
    fontWeight: "700",
    fontSize: 13,
  },
});
