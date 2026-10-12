import React, { useEffect, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  Image,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import * as Location from "expo-location";
import * as DocumentPicker from "expo-document-picker";
import * as ImageManipulator from "expo-image-manipulator";
import { PoDAdapter, PoDGeostamp } from "../../data/adapters/PoDAdapter";
import { OutboxEngine } from "../../core/offline";
import { SignatureCanvas } from "../components/SignatureCanvas";

export interface PoDSignatureScreenProps {
  billId: string;
  billNo: string;
  customerName?: string;
  onSuccess?: () => void;
  onCancel?: () => void;
}

export const PoDSignatureScreen: React.FC<PoDSignatureScreenProps> = ({
  billId,
  billNo,
  customerName,
  onSuccess,
  onCancel,
}) => {
  const [recipientName, setRecipientName] = useState<string>("");
  const [recipientPhone, setRecipientPhone] = useState<string>("");
  const [customNotes, setCustomNotes] = useState<string>("");

  // Chữ ký
  const [signatureLocalUri, setSignatureLocalUri] = useState<string | null>(null);
  const [signatureSvg, setSignatureSvg] = useState<string | null>(null);

  // Ảnh hàng hóa hiện trường (1 - 2 ảnh)
  const [cargoPhotos, setCargoPhotos] = useState<string[]>([]);
  const [isCompressingPhoto, setIsCompressingPhoto] = useState<boolean>(false);

  // GPS Geostamp & Timestamp
  const [geostamp, setGeostamp] = useState<PoDGeostamp>({
    latitude: null,
    longitude: null,
    accuracy: null,
    status: "GPS_UNAVAILABLE",
  });
  const [isLoadingGps, setIsLoadingGps] = useState<boolean>(false);
  const [deliveredAt, setDeliveredAt] = useState<string>("");

  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  // Khởi tạo thời gian giao hàng và tự động lấy GPS
  useEffect(() => {
    const now = new Date();
    setDeliveredAt(
      now.toLocaleDateString("vi-VN") +
        " " +
        now.toLocaleTimeString("vi-VN", { hour12: false })
    );
    void fetchGpsLocation();
  }, []);

  const fetchGpsLocation = async () => {
    setIsLoadingGps(true);
    try {
      const { status } = await Location.requestForegroundPermissionsAsync();
      if (status !== "granted") {
        setGeostamp({
          latitude: null,
          longitude: null,
          accuracy: null,
          status: "PERMISSION_DENIED",
        });
        Alert.alert(
          "Chưa cấp quyền GPS",
          "Ứng dụng chưa được cấp quyền vị trí. Trạng thái 'GPS_UNAVAILABLE' sẽ được ghi nhận để kiểm toán."
        );
        return;
      }

      const isLocationEnabled = await Location.hasServicesEnabledAsync();
      if (!isLocationEnabled) {
        setGeostamp({
          latitude: null,
          longitude: null,
          accuracy: null,
          status: "GPS_UNAVAILABLE",
        });
        Alert.alert(
          "GPS đang tắt",
          "Vui lòng bật Định vị vị trí trên thiết bị để đóng dấu tọa độ bàn giao hàng hóa."
        );
        return;
      }

      const pos = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.Balanced,
      });

      setGeostamp({
        latitude: pos.coords.latitude,
        longitude: pos.coords.longitude,
        accuracy: pos.coords.accuracy,
        status: "GPS_ACQUIRED",
      });
    } catch {
      setGeostamp({
        latitude: null,
        longitude: null,
        accuracy: null,
        status: "GPS_UNAVAILABLE",
      });
    } finally {
      setIsLoadingGps(false);
    }
  };

  const handlePickCargoPhoto = async () => {
    if (cargoPhotos.length >= 2) {
      Alert.alert("Giới hạn ảnh", "Tối đa đính kèm 2 ảnh chụp hàng hóa tại điểm giao.");
      return;
    }

    try {
      const res = await DocumentPicker.getDocumentAsync({
        type: ["image/*"],
        copyToCacheDirectory: true,
      });

      if (res.canceled || !res.assets || res.assets.length === 0) return;

      setIsCompressingPhoto(true);
      const asset = res.assets[0];

      // Nén ảnh tự động
      const manipulated = await ImageManipulator.manipulateAsync(
        asset.uri,
        [{ resize: { width: 1400 } }],
        { compress: 0.75, format: ImageManipulator.SaveFormat.JPEG }
      );

      // Lưu vào FileSystem
      const outbox = OutboxEngine.getInstance();
      const savedUri = await outbox.saveLocalAttachmentFile(
        manipulated.uri,
        `pod-cargo-${Date.now()}.jpg`
      );

      setCargoPhotos((prev) => [...prev, savedUri]);
    } catch (err) {
      Alert.alert("Lỗi chụp ảnh", err instanceof Error ? err.message : "Không thể xử lý ảnh.");
    } finally {
      setIsCompressingPhoto(false);
    }
  };

  const handleRemoveCargoPhoto = async (index: number) => {
    const targetUri = cargoPhotos[index];
    if (targetUri) {
      await OutboxEngine.getInstance().cleanupLocalFile(targetUri);
      setCargoPhotos((prev) => prev.filter((_, idx) => idx !== index));
    }
  };

  const handleSubmit = async (forceOffline = false) => {
    if (!recipientName.trim()) {
      Alert.alert("Thiếu thông tin", "Vui lòng nhập Họ tên người ký nhận hàng.");
      return;
    }
    if (!recipientPhone.trim() || recipientPhone.trim().length < 9) {
      Alert.alert("Thiếu thông tin", "Vui lòng nhập Số điện thoại người nhận hàng hợp lệ.");
      return;
    }
    if (!signatureLocalUri) {
      Alert.alert("Chưa có chữ ký", "Vui lòng ký tên và nhấn '✓ Chốt chữ ký' trong khung vẽ.");
      return;
    }

    try {
      setIsSubmitting(true);
      const res = await PoDAdapter.submitPoD(
        {
          billId,
          billNo,
          recipientName: recipientName.trim(),
          recipientPhone: recipientPhone.trim(),
          signatureLocalUri,
          signatureSvgContent: signatureSvg || undefined,
          cargoImageLocalUris: cargoPhotos,
          geostamp,
          deliveredAt,
          customNotes: customNotes.trim() || undefined,
        },
        forceOffline
      );

      Alert.alert(res.isOffline ? "Đã lưu Ngoại tuyến" : "Thành công", res.messageVi);
      onSuccess?.();
    } catch (err) {
      Alert.alert("Lỗi gửi e-PoD", err instanceof Error ? err.message : "Không thể ghi nhận bàn giao.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <ScrollView style={styles.container} showsVerticalScrollIndicator={false}>
      {/* Header vận đơn */}
      <View style={styles.headerBox}>
        <View style={styles.headerLeft}>
          <Text style={styles.headerTitle}>Ký nhận Giao hàng (e-PoD)</Text>
          <Text style={styles.headerBillNo}>Vận đơn: {billNo}</Text>
          {customerName ? <Text style={styles.headerCustomer}>Khách hàng: {customerName}</Text> : null}
        </View>
        {onCancel && (
          <TouchableOpacity style={styles.cancelBtn} onPress={onCancel}>
            <Text style={styles.cancelBtnText}>✕ Đóng</Text>
          </TouchableOpacity>
        )}
      </View>

      {/* Geostamp & Timestamp Box */}
      <View style={styles.infoBox}>
        <View style={styles.infoRow}>
          <Text style={styles.infoLabel}>🕒 Thời gian giao thực tế:</Text>
          <Text style={styles.infoValue}>{deliveredAt}</Text>
        </View>

        <View style={[styles.infoRow, { marginTop: 6 }]}>
          <Text style={styles.infoLabel}>📍 Tọa độ GPS bàn giao:</Text>
          {isLoadingGps ? (
            <ActivityIndicator size="small" color="#2563EB" />
          ) : geostamp.status === "GPS_ACQUIRED" ? (
            <Text style={styles.gpsSuccessText}>
              ✓ {geostamp.latitude?.toFixed(5)}, {geostamp.longitude?.toFixed(5)} (±{Math.round(geostamp.accuracy || 0)}m)
            </Text>
          ) : (
            <View style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
              <Text style={styles.gpsWarningText}>⚠️ {geostamp.status}</Text>
              <TouchableOpacity style={styles.gpsRetryBtn} onPress={fetchGpsLocation}>
                <Text style={styles.gpsRetryText}>Quét lại</Text>
              </TouchableOpacity>
            </View>
          )}
        </View>
      </View>

      {/* Thông tin người nhận */}
      <View style={styles.card}>
        <Text style={styles.cardTitle}>1. Thông tin người nhận hàng</Text>
        <Text style={styles.fieldLabel}>Họ và tên người nhận (*)</Text>
        <TextInput
          style={styles.textInput}
          value={recipientName}
          onChangeText={setRecipientName}
          placeholder="VD: Nguyễn Văn Hùng (Thủ kho A)"
        />

        <Text style={styles.fieldLabel}>Số điện thoại nhận hàng (*)</Text>
        <TextInput
          style={styles.textInput}
          keyboardType="phone-pad"
          value={recipientPhone}
          onChangeText={setRecipientPhone}
          placeholder="VD: 0908123456"
        />

        <Text style={styles.fieldLabel}>Ghi chú biên bản giao nhận</Text>
        <TextInput
          style={styles.textInput}
          value={customNotes}
          onChangeText={setCustomNotes}
          placeholder="VD: Đã kiểm đếm đủ 10 kiện, bao bì nguyên vẹn..."
        />
      </View>

      {/* Khung ký nhận điện tử */}
      <View style={styles.card}>
        <Text style={styles.cardTitle}>2. Chữ ký số người nhận hàng (Vector &lt; 50KB)</Text>
        <SignatureCanvas
          height={170}
          onSignatureCaptured={({ localUri, svgContent }) => {
            setSignatureLocalUri(localUri);
            setSignatureSvg(svgContent);
          }}
        />
      </View>

      {/* Ảnh chụp hiện trường trước cửa kho */}
      <View style={styles.card}>
        <View style={styles.rowBetween}>
          <Text style={styles.cardTitle}>3. Ảnh hàng hóa tại hiện trường (Tối đa 2 ảnh)</Text>
          <TouchableOpacity
            style={styles.addPhotoBtn}
            onPress={handlePickCargoPhoto}
            disabled={isCompressingPhoto || cargoPhotos.length >= 2}
          >
            <Text style={styles.addPhotoText}>+ Chụp ảnh</Text>
          </TouchableOpacity>
        </View>

        {cargoPhotos.length === 0 ? (
          <Text style={styles.noPhotoText}>Chưa có ảnh hiện trường bàn giao (tùy chọn).</Text>
        ) : (
          <View style={styles.photoGrid}>
            {cargoPhotos.map((uri, idx) => (
              <View key={uri} style={styles.photoItem}>
                <Image source={{ uri }} style={styles.photoThumb} />
                <TouchableOpacity
                  style={styles.removePhotoBadge}
                  onPress={() => void handleRemoveCargoPhoto(idx)}
                >
                  <Text style={styles.removePhotoText}>✕</Text>
                </TouchableOpacity>
              </View>
            ))}
          </View>
        )}
      </View>

      {/* Action Footer */}
      <View style={styles.footerRow}>
        <TouchableOpacity
          style={styles.submitBtn}
          onPress={() => void handleSubmit(false)}
          disabled={isSubmitting}
        >
          {isSubmitting ? (
            <ActivityIndicator color="#FFFFFF" />
          ) : (
            <Text style={styles.submitBtnText}>✓ Xác Nhận Bàn Giao (e-PoD)</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.offlineBtn}
          onPress={() => void handleSubmit(true)}
          disabled={isSubmitting}
        >
          <Text style={styles.offlineBtnText}>📡 Lưu Ngoại Tuyến</Text>
        </TouchableOpacity>
      </View>

      <View style={{ height: 40 }} />
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#F8FAFC", padding: 16 },
  headerBox: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
    backgroundColor: "#FFFFFF",
    padding: 14,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginBottom: 12,
  },
  headerLeft: { flex: 1 },
  headerTitle: { fontSize: 16, fontWeight: "800", color: "#0F172A" },
  headerBillNo: { fontSize: 13, fontWeight: "700", color: "#2563EB", marginTop: 2 },
  headerCustomer: { fontSize: 12, color: "#64748B", marginTop: 2 },
  cancelBtn: { paddingHorizontal: 10, paddingVertical: 6, backgroundColor: "#F1F5F9", borderRadius: 8 },
  cancelBtnText: { fontSize: 12, fontWeight: "700", color: "#475569" },
  infoBox: {
    backgroundColor: "#EFF6FF",
    borderWidth: 1,
    borderColor: "#BFDBFE",
    padding: 12,
    borderRadius: 12,
    marginBottom: 14,
  },
  infoRow: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
  infoLabel: { fontSize: 12, fontWeight: "700", color: "#1E40AF" },
  infoValue: { fontSize: 12, fontWeight: "600", color: "#1E293B" },
  gpsSuccessText: { fontSize: 12, fontWeight: "700", color: "#059669" },
  gpsWarningText: { fontSize: 11, fontWeight: "700", color: "#D97706" },
  gpsRetryBtn: { backgroundColor: "#DBEAFE", paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  gpsRetryText: { fontSize: 11, fontWeight: "700", color: "#1D4ED8" },
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 14,
  },
  cardTitle: { fontSize: 13, fontWeight: "800", color: "#0F172A", marginBottom: 8 },
  fieldLabel: { fontSize: 11, fontWeight: "700", color: "#475569", marginTop: 6, marginBottom: 3 },
  textInput: {
    backgroundColor: "#F8FAFC",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 13,
    color: "#0F172A",
    marginBottom: 4,
  },
  rowBetween: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
  addPhotoBtn: { backgroundColor: "#EFF6FF", borderWidth: 1, borderColor: "#93C5FD", paddingHorizontal: 10, paddingVertical: 5, borderRadius: 8 },
  addPhotoText: { fontSize: 11, fontWeight: "700", color: "#2563EB" },
  noPhotoText: { fontSize: 12, color: "#94A3B8", fontStyle: "italic", marginVertical: 6 },
  photoGrid: { flexDirection: "row", gap: 10, marginTop: 8 },
  photoItem: { position: "relative" },
  photoThumb: { width: 90, height: 90, borderRadius: 8, backgroundColor: "#E2E8F0" },
  removePhotoBadge: {
    position: "absolute",
    top: -6,
    right: -6,
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: "#DC2626",
    alignItems: "center",
    justifyContent: "center",
  },
  removePhotoText: { color: "#FFFFFF", fontSize: 11, fontWeight: "800" },
  footerRow: { flexDirection: "row", gap: 8, marginTop: 4 },
  submitBtn: {
    flex: 1,
    backgroundColor: "#0F172A",
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: "center",
  },
  submitBtnText: { fontSize: 13, fontWeight: "800", color: "#FFFFFF" },
  offlineBtn: {
    backgroundColor: "#EFF6FF",
    borderWidth: 1.5,
    borderColor: "#93C5FD",
    paddingHorizontal: 14,
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: "center",
  },
  offlineBtnText: { fontSize: 12, fontWeight: "700", color: "#1D4ED8" },
});

