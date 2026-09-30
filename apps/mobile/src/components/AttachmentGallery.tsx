import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import * as DocumentPicker from "expo-document-picker";
import * as FileSystem from "expo-file-system/legacy";
import * as ImageManipulator from "expo-image-manipulator";
import { AttachmentDto, AttachmentObjectType } from "@lcms/shared";
import { apiRequest, uploadAttachmentBase64 } from "../api/client";
import { enqueueOfflineMutation } from "../offline/outbox";

export interface AttachmentGalleryProps {
  objectType: AttachmentObjectType;
  objectId: string;
  objectLabelVi: string;
  canWrite?: boolean;
}

/**
 * Displays attachments stored on the Contabo VPS local disk (/opt/cms/attachments),
 * supports compressed image/PDF uploads with SHA-256 verification, offline outbox fallback,
 * and soft-delete with mandatory audit reason.
 */
export const AttachmentGallery: React.FC<AttachmentGalleryProps> = ({
  objectType,
  objectId,
  objectLabelVi,
  canWrite = true,
}) => {
  const [items, setItems] = useState<AttachmentDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [uploading, setUploading] = useState<boolean>(false);
  const [notes, setNotes] = useState<string>("");
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [deleteReason, setDeleteReason] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const loadAttachments = useCallback(async () => {
    setLoading(true);
    try {
      const list = await apiRequest<AttachmentDto[]>(
        `/api/attachments?objectType=${encodeURIComponent(objectType)}&objectId=${encodeURIComponent(objectId)}`
      );
      setItems(list);
    } catch (err) {
      setStatusMessage(
        err instanceof Error ? err.message : "Không thể tải danh sách tệp đính kèm."
      );
    } finally {
      setLoading(false);
    }
  }, [objectType, objectId]);

  useEffect(() => {
    void loadAttachments();
  }, [loadAttachments]);

  const handlePickAndUpload = async () => {
    try {
      const result = await DocumentPicker.getDocumentAsync({
        type: ["image/*", "application/pdf"],
        copyToCacheDirectory: true,
      });

      if (result.canceled || !result.assets || result.assets.length === 0) {
        return;
      }

      const asset = result.assets[0];
      setUploading(true);
      setStatusMessage(null);

      let uri = asset.uri;
      let contentType = asset.mimeType ?? "image/jpeg";
      let fileName = asset.name || `chung-tu-${Date.now()}.jpg`;

      // Compress image before uploading to Contabo VPS local disk
      if (contentType.startsWith("image/") && contentType !== "image/webp") {
        const manipulated = await ImageManipulator.manipulateAsync(
          uri,
          [{ resize: { width: 1600 } }],
          { compress: 0.78, format: ImageManipulator.SaveFormat.JPEG }
        );
        uri = manipulated.uri;
        contentType = "image/jpeg";
        if (!fileName.toLowerCase().endsWith(".jpg") && !fileName.toLowerCase().endsWith(".jpeg")) {
          fileName = `${fileName}.jpg`;
        }
      }

      const base64 = await FileSystem.readAsStringAsync(uri, {
        encoding: FileSystem.EncodingType.Base64,
      });

      try {
        await uploadAttachmentBase64({
          objectType,
          objectId,
          fileName,
          contentType,
          base64,
          notes: notes.trim() || undefined,
        });
        setNotes("");
        setStatusMessage("Đã lưu tệp đính kèm lên ổ đĩa máy chủ Contabo thành công.");
        await loadAttachments();
      } catch {
        // Queue in SQLite Offline Outbox if field operator has no signal
        await enqueueOfflineMutation({
          titleVi: `Tải chứng từ ${fileName} cho ${objectLabelVi}`,
          endpoint: "/api/attachments",
          method: "POST",
          payload: {
            objectType,
            objectId,
            fileName,
            contentType,
            base64,
            notes: notes.trim() || undefined,
          },
          idempotencyPrefix: "att",
        });
        setNotes("");
        setStatusMessage(
          "Không có mạng: Đã lưu tệp vào Hàng đợi Ngoại tuyến (Outbox) để tự động đồng bộ khi có sóng."
        );
      }
    } catch (err) {
      Alert.alert(
        "Lỗi tải tệp",
        err instanceof Error ? err.message : "Không thể đọc tệp chứng từ."
      );
    } finally {
      setUploading(false);
    }
  };

  const handleConfirmSoftDelete = async (attachmentId: string) => {
    if (!deleteReason.trim()) {
      Alert.alert("Thiếu lý do", "Vui lòng nhập lý do xóa mềm tệp đính kèm để ghi nhật ký kiểm toán.");
      return;
    }

    try {
      await apiRequest(`/api/attachments/${attachmentId}`, {
        method: "DELETE",
        body: { reason: deleteReason.trim() },
      });
      setDeletingId(null);
      setDeleteReason("");
      setStatusMessage("Đã xóa mềm tệp đính kèm và ghi nhận AuditEvent.");
      await loadAttachments();
    } catch (err) {
      Alert.alert(
        "Không thể xóa tệp",
        err instanceof Error ? err.message : "Đã xảy ra lỗi khi xóa tệp đính kèm."
      );
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.headerRow}>
        <Text style={styles.sectionTitle}>
          Chứng từ đính kèm ({items.length}) • Lưu trữ VPS Contabo
        </Text>
      </View>

      {statusMessage ? (
        <View style={styles.statusBanner}>
          <Text style={styles.statusBannerText}>{statusMessage}</Text>
        </View>
      ) : null}

      {canWrite ? (
        <View style={styles.uploadBox}>
          <TextInput
            style={styles.input}
            placeholder="Ghi chú chứng từ (VD: Phiếu cân kho Cát Lái, Hóa đơn nâng hạ...)"
            value={notes}
            onChangeText={setNotes}
          />
          <TouchableOpacity
            style={[styles.uploadBtn, uploading && styles.uploadBtnDisabled]}
            onPress={handlePickAndUpload}
            disabled={uploading}
          >
            <Text style={styles.uploadBtnText}>
              {uploading
                ? "Đang nén & tải lên máy chủ..."
                : "📷 Chụp ảnh / Chọn PDF đính kèm"}
            </Text>
          </TouchableOpacity>
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator size="small" color="#0F172A" style={{ marginVertical: 12 }} />
      ) : items.length === 0 ? (
        <Text style={styles.emptyText}>
          Chưa có ảnh hoặc PDF chứng từ nào được đính kèm cho {objectLabelVi}.
        </Text>
      ) : (
        items.map((att) => (
          <View key={att.id} style={styles.itemCard}>
            <View style={styles.itemHeader}>
              <Text style={styles.fileName} numberOfLines={1}>
                {att.contentType === "application/pdf" ? "📄 " : "🖼️ "}
                {att.fileName}
              </Text>
              <Text style={styles.fileSize}>
                {(att.sizeBytes / 1024).toFixed(1)} KB
              </Text>
            </View>
            {att.notes ? <Text style={styles.itemNotes}>{att.notes}</Text> : null}
            <Text style={styles.itemMeta}>
              SHA-256: {att.sha256Hash.slice(0, 16)}… •{" "}
              {new Date(att.uploadedAt).toLocaleString("vi-VN")}
            </Text>

            {canWrite ? (
              deletingId === att.id ? (
                <View style={styles.deleteBox}>
                  <TextInput
                    style={styles.input}
                    placeholder="Nhập lý do xóa chứng từ (bắt buộc cho Audit)..."
                    value={deleteReason}
                    onChangeText={setDeleteReason}
                  />
                  <View style={styles.deleteActions}>
                    <TouchableOpacity
                      style={styles.cancelSmallBtn}
                      onPress={() => {
                        setDeletingId(null);
                        setDeleteReason("");
                      }}
                    >
                      <Text style={styles.cancelSmallText}>Hủy</Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={styles.confirmDeleteBtn}
                      onPress={() => handleConfirmSoftDelete(att.id)}
                    >
                      <Text style={styles.confirmDeleteText}>Xác nhận xóa mềm</Text>
                    </TouchableOpacity>
                  </View>
                </View>
              ) : (
                <TouchableOpacity
                  style={styles.deleteLink}
                  onPress={() => setDeletingId(att.id)}
                >
                  <Text style={styles.deleteLinkText}>Xóa mềm (kèm lý do)</Text>
                </TouchableOpacity>
              )
            ) : null}
          </View>
        ))
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginTop: 12,
  },
  headerRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
  },
  statusBanner: {
    backgroundColor: "#F0FDF4",
    borderColor: "#BBF7D0",
    borderWidth: 1,
    borderRadius: 8,
    padding: 10,
    marginBottom: 10,
  },
  statusBannerText: {
    fontSize: 12,
    color: "#166534",
  },
  uploadBox: {
    marginBottom: 12,
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 13,
    color: "#0F172A",
    backgroundColor: "#F8FAFC",
    marginBottom: 8,
  },
  uploadBtn: {
    backgroundColor: "#0F172A",
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: "center",
  },
  uploadBtnDisabled: {
    opacity: 0.6,
  },
  uploadBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  emptyText: {
    fontSize: 12,
    color: "#64748B",
    fontStyle: "italic",
  },
  itemCard: {
    paddingVertical: 10,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  itemHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  fileName: {
    fontSize: 13,
    fontWeight: "700",
    color: "#1E293B",
    flex: 1,
  },
  fileSize: {
    fontSize: 11,
    color: "#64748B",
    marginLeft: 8,
  },
  itemNotes: {
    fontSize: 12,
    color: "#334155",
    marginTop: 2,
  },
  itemMeta: {
    fontSize: 11,
    color: "#94A3B8",
    marginTop: 3,
  },
  deleteLink: {
    marginTop: 6,
    alignSelf: "flex-start",
  },
  deleteLinkText: {
    fontSize: 11,
    color: "#DC2626",
    fontWeight: "600",
  },
  deleteBox: {
    marginTop: 8,
    backgroundColor: "#FEF2F2",
    padding: 10,
    borderRadius: 8,
  },
  deleteActions: {
    flexDirection: "row",
    justifyContent: "flex-end",
    gap: 8,
  },
  cancelSmallBtn: {
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  cancelSmallText: {
    fontSize: 12,
    color: "#475569",
    fontWeight: "600",
  },
  confirmDeleteBtn: {
    backgroundColor: "#DC2626",
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
  },
  confirmDeleteText: {
    fontSize: 12,
    color: "#FFFFFF",
    fontWeight: "700",
  },
});

