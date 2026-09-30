import React, { useState } from "react";
import {
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { CameraView, useCameraPermissions } from "expo-camera";
import { useRouter } from "expo-router";
import { BillListItemDto } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";

export default function ScannerScreen() {
  const router = useRouter();
  const [permission, requestPermission] = useCameraPermissions();
  const [scannerActive, setScannerActive] = useState<boolean>(true);
  const [manualCode, setManualCode] = useState<string>("");
  const [lastScanned, setLastScanned] = useState<string | null>(null);

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

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.heading}>
        Quét Mã Vạch / QR Vận đơn (HAWB / MAWB / Bill No)
      </Text>
      <Text style={styles.subheading}>
        Hướng camera vào mã vạch trên phiếu giao nhận kho/cảng để mở ngay hồ sơ vận đơn và chụp chứng từ đính kèm.
      </Text>

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
              barcodeScannerSettings={{
                barcodeTypes: ["qr", "code128", "code39", "ean13", "pdf417"],
              }}
              onBarcodeScanned={({ data }) => {
                if (data && scannerActive) {
                  void lookupBillByCode(data);
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
    marginBottom: 14,
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
    marginBottom: 16,
  },
  camera: {
    flex: 1,
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
  manualCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#E2E8F0",
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

