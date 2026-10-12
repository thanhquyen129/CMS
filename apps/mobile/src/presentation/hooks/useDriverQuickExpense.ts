import { useMemo, useState } from "react";
import { Alert } from "react-native";
import * as DocumentPicker from "expo-document-picker";
import * as ImageManipulator from "expo-image-manipulator";
import { formatMoney } from "@lcms/shared";
import { apiRequest } from "../../api/client";
import {
  DriverExpenseAdapter,
  DriverExpenseCategory,
  DriverExpenseInput,
} from "../../data/adapters/DriverExpenseAdapter";
import { enqueueOfflineMutation, OutboxEngine } from "../../core/offline";

export interface UseDriverQuickExpenseProps {
  initialBillId?: string;
  onSuccess?: () => void;
}

export function useDriverQuickExpense({
  initialBillId = "",
  onSuccess,
}: UseDriverQuickExpenseProps = {}) {
  const [category, setCategory] = useState<DriverExpenseCategory>("fuel");
  const [selectedBillId, setSelectedBillId] = useState<string>(initialBillId);

  // Dữ liệu cho Xăng dầu
  const [liters, setLiters] = useState<string>("");
  const [pricePerLiter, setPricePerLiter] = useState<string>("23500");

  // Dữ liệu cho Vé BOT / Sửa chữa / Bến bãi
  const [directAmount, setDirectAmount] = useState<string>("");

  // Thông tin phụ trợ
  const [stationName, setStationName] = useState<string>("");
  const [odometerKm, setOdometerKm] = useState<string>("");
  const [customNotes, setCustomNotes] = useState<string>("");

  // Ảnh hóa đơn
  const [receiptLocalUri, setReceiptLocalUri] = useState<string | null>(null);
  const [isCompressing, setIsCompressing] = useState<boolean>(false);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  // Tự động tính thành tiền theo thời gian thực
  const calculatedTotalAmount = useMemo(() => {
    if (category === "fuel") {
      const l = parseFloat(liters) || 0;
      const p = parseFloat(pricePerLiter) || 0;
      return Math.round(l * p);
    }
    return Math.round(parseFloat(directAmount) || 0);
  }, [category, liters, pricePerLiter, directAmount]);

  /**
   * Chụp / chọn ảnh hóa đơn và tự động nén kích thước tối ưu trước khi lưu FileSystem
   */
  const handlePickAndCompressReceipt = async () => {
    try {
      const result = await DocumentPicker.getDocumentAsync({
        type: ["image/*", "application/pdf"],
        copyToCacheDirectory: true,
      });

      if (result.canceled || !result.assets || result.assets.length === 0) {
        return;
      }

      const asset = result.assets[0];
      setIsCompressing(true);

      let processedUri = asset.uri;

      // Nén ảnh tự động để giảm tải dung lượng ổ đĩa và băng thông
      if (asset.mimeType?.startsWith("image/")) {
        const manipulated = await ImageManipulator.manipulateAsync(
          asset.uri,
          [{ resize: { width: 1400 } }],
          { compress: 0.75, format: ImageManipulator.SaveFormat.JPEG }
        );
        processedUri = manipulated.uri;
      }

      // Lưu file vật lý vào FileSystem.documentDirectory (Anti-Blob-Bloat)
      const outbox = OutboxEngine.getInstance();
      const savedLocalUri = await outbox.saveLocalAttachmentFile(
        processedUri,
        asset.name || `hoa-don-${Date.now()}.jpg`
      );

      setReceiptLocalUri(savedLocalUri);
    } catch (err) {
      Alert.alert(
        "Lỗi xử lý ảnh",
        err instanceof Error ? err.message : "Không thể xử lý ảnh hóa đơn."
      );
    } finally {
      setIsCompressing(false);
    }
  };

  const handleClearReceipt = async () => {
    if (receiptLocalUri) {
      await OutboxEngine.getInstance().cleanupLocalFile(receiptLocalUri);
      setReceiptLocalUri(null);
    }
  };

  const resetForm = () => {
    setLiters("");
    setDirectAmount("");
    setStationName("");
    setCustomNotes("");
    setReceiptLocalUri(null);
  };

  /**
   * Gửi chi phí siêu tốc (Offline-First, hoàn tất < 15 giây)
   */
  const handleSubmitExpense = async (forceOffline = false) => {
    if (!selectedBillId) {
      Alert.alert("Thiếu Vận đơn", "Vui lòng chọn Vận đơn (Bill) liên kết.");
      return;
    }

    try {
      setIsSubmitting(true);

      // 1. Chuẩn bị input theo nghiệp vụ của tài xế
      let input: DriverExpenseInput;
      if (category === "fuel") {
        input = {
          category: "fuel",
          billId: selectedBillId,
          liters: parseFloat(liters) || 0,
          pricePerLiter: parseFloat(pricePerLiter) || 0,
          odometerKm: odometerKm ? parseInt(odometerKm, 10) : null,
          gasStationName: stationName.trim() || null,
          receiptImageUri: receiptLocalUri,
          customNotes: customNotes.trim() || null,
        };
      } else {
        input = {
          category,
          billId: selectedBillId,
          amount: parseFloat(directAmount) || 0,
          stationOrShopName: stationName.trim() || null,
          odometerKm: odometerKm ? parseInt(odometerKm, 10) : null,
          receiptImageUri: receiptLocalUri,
          customNotes: customNotes.trim() || null,
        };
      }

      // 2. Chuyển đổi qua Client Adapter Pattern -> DTO tiêu chuẩn của /api/costs
      const adaptedPayload = DriverExpenseAdapter.adaptToCostPayload(input);

      const titleVi = `Chi phí ${
        category === "fuel"
          ? "Xăng dầu"
          : category === "toll_bot"
          ? "Vé BOT"
          : category === "repair"
          ? "Sửa xe"
          : "Bến bãi"
      } (${formatMoney(adaptedPayload.amount, "VND")})`;

      if (forceOffline) {
        // Lưu trực tiếp vào hàng đợi ngoại tuyến Outbox
        await enqueueOfflineMutation({
          titleVi,
          endpoint: "/api/costs",
          method: "POST",
          payload: {
            billId: adaptedPayload.billId,
            amount: adaptedPayload.amount,
            currencyCode: adaptedPayload.currencyCode,
            costTypeCode: adaptedPayload.costTypeCode,
            attributionType: adaptedPayload.attributionType,
            sourceType: adaptedPayload.sourceType,
            notes: adaptedPayload.notes,
          },
          localFileUri: adaptedPayload.localReceiptUri,
          fileContentType: "image/jpeg",
          fileName: "hoa-don-chi-phi.jpg",
          idempotencyPrefix: "drv-cost",
        });

        Alert.alert("Đã lưu Ngoại tuyến", "Khoản chi phí đã được lưu vào Outbox và sẽ tự động gửi khi có mạng.");
        resetForm();
        onSuccess?.();
        return;
      }

      // 3. Thử gửi trực tiếp qua API, nếu mất mạng tự động fallback vào Outbox
      try {
        await apiRequest("/api/costs", {
          method: "POST",
          body: {
            billId: adaptedPayload.billId,
            amount: adaptedPayload.amount,
            currencyCode: adaptedPayload.currencyCode,
            costTypeCode: adaptedPayload.costTypeCode,
            attributionType: adaptedPayload.attributionType,
            sourceType: adaptedPayload.sourceType,
            notes: adaptedPayload.notes,
          },
        });

        Alert.alert("Thành công", `Đã ghi nhận ${titleVi}.`);
        resetForm();
        onSuccess?.();
      } catch {
        // Fallback tự động vào Outbox khi mạng chập chờn
        await enqueueOfflineMutation({
          titleVi,
          endpoint: "/api/costs",
          method: "POST",
          payload: {
            billId: adaptedPayload.billId,
            amount: adaptedPayload.amount,
            currencyCode: adaptedPayload.currencyCode,
            costTypeCode: adaptedPayload.costTypeCode,
            attributionType: adaptedPayload.attributionType,
            sourceType: adaptedPayload.sourceType,
            notes: adaptedPayload.notes,
          },
          localFileUri: adaptedPayload.localReceiptUri,
          fileContentType: "image/jpeg",
          fileName: "hoa-don-chi-phi.jpg",
          idempotencyPrefix: "drv-cost",
        });

        Alert.alert(
          "Đã lưu Outbox",
          "Mạng gián đoạn: Chi phí đã được đưa vào hàng đợi tự động đồng bộ khi có kết nối."
        );
        resetForm();
        onSuccess?.();
      }
    } catch (err) {
      Alert.alert(
        "Lỗi nhập liệu",
        err instanceof Error ? err.message : "Vui lòng kiểm tra lại thông tin."
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return {
    category,
    setCategory,
    selectedBillId,
    setSelectedBillId,
    liters,
    setLiters,
    pricePerLiter,
    setPricePerLiter,
    directAmount,
    setDirectAmount,
    stationName,
    setStationName,
    odometerKm,
    setOdometerKm,
    customNotes,
    setCustomNotes,
    receiptLocalUri,
    isCompressing,
    isSubmitting,
    calculatedTotalAmount,
    handlePickAndCompressReceipt,
    handleClearReceipt,
    handleSubmitExpense,
    resetForm,
  };
}
