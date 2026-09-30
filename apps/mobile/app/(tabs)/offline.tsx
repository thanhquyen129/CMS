import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import {
  listOfflineOutbox,
  OfflineOutboxItem,
  removeOfflineOutboxItem,
  replayOfflineOutbox,
} from "../../src/offline/outbox";

export default function OfflineOutboxScreen() {
  const [items, setItems] = useState<OfflineOutboxItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [syncing, setSyncing] = useState<boolean>(false);

  const loadOutbox = useCallback(async () => {
    setLoading(true);
    try {
      const list = await listOfflineOutbox();
      setItems(list);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadOutbox();
  }, [loadOutbox]);

  const handleSyncAll = async () => {
    setSyncing(true);
    try {
      const summary = await replayOfflineOutbox();
      await loadOutbox();
      Alert.alert(
        "Kết quả Đồng bộ Ngoại tuyến",
        `• Đã đồng bộ thành công: ${summary.syncedCount}\n• Xung đột phiên bản (409 If-Match): ${summary.conflictCount}\n• Chưa gửi được (Mất sóng): ${summary.failedCount}`
      );
    } finally {
      setSyncing(false);
    }
  };

  const handleDiscard = async (id: string) => {
    await removeOfflineOutboxItem(id);
    await loadOutbox();
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={loading}
          onRefresh={() => void loadOutbox()}
        />
      }
    >
      <View style={styles.banner}>
        <Text style={styles.bannerTitle}>
          Hàng đợi Ngoại tuyến (SQLite Offline Outbox • {items.length})
        </Text>
        <Text style={styles.bannerDesc}>
          Mọi thao tác chụp ảnh chứng từ, cập nhật CW tại cảng/kho khi mất sóng được gắn khóa chống trùng (Idempotency-Key) và kiểm tra xung đột phiên bản (If-Match).
        </Text>
        <TouchableOpacity
          style={[styles.syncBtn, syncing && styles.syncBtnDisabled]}
          onPress={handleSyncAll}
          disabled={syncing || items.length === 0}
        >
          {syncing ? (
            <ActivityIndicator color="#FFFFFF" />
          ) : (
            <Text style={styles.syncBtnText}>
              🔄 Đồng bộ ngay ({items.length} mục chờ)
            </Text>
          )}
        </TouchableOpacity>
      </View>

      {items.length === 0 ? (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyTitle}>Tất cả dữ liệu đã đồng bộ</Text>
          <Text style={styles.emptyDesc}>
            Không có thao tác ngoại tuyến nào đang chờ gửi lên máy chủ Contabo.
          </Text>
        </View>
      ) : (
        items.map((item) => (
          <View key={item.id} style={styles.itemCard}>
            <View style={styles.rowBetween}>
              <Text style={styles.itemTitle}>{item.titleVi}</Text>
              <View
                style={[
                  styles.statusBadge,
                  item.status === "conflict"
                    ? styles.badgeConflict
                    : item.status === "failed"
                    ? styles.badgeFailed
                    : styles.badgePending,
                ]}
              >
                <Text style={styles.statusText}>
                  {item.status === "conflict"
                    ? "Xung đột 409 (If-Match)"
                    : item.status === "failed"
                    ? `Chờ thử lại (#${item.retryCount})`
                    : "Đang chờ mạng"}
                </Text>
              </View>
            </View>

            <Text style={styles.metaText}>
              {item.method} {item.endpoint} • Idempotency: {item.idempotencyKey.slice(0, 14)}…
            </Text>

            {item.errorMessageVi ? (
              <Text style={styles.errorText}>{item.errorMessageVi}</Text>
            ) : null}

            <View style={styles.actionsRow}>
              <TouchableOpacity
                style={styles.discardBtn}
                onPress={() => handleDiscard(item.id)}
              >
                <Text style={styles.discardBtnText}>
                  Hủy mục chờ này
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        ))
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 16,
  },
  banner: {
    backgroundColor: "#0F172A",
    borderRadius: 14,
    padding: 16,
    marginBottom: 14,
  },
  bannerTitle: {
    color: "#FFFFFF",
    fontSize: 15,
    fontWeight: "800",
  },
  bannerDesc: {
    color: "#CBD5E1",
    fontSize: 12,
    marginTop: 6,
    lineHeight: 18,
  },
  syncBtn: {
    backgroundColor: "#2563EB",
    paddingVertical: 11,
    borderRadius: 8,
    alignItems: "center",
    marginTop: 12,
  },
  syncBtnDisabled: {
    opacity: 0.5,
  },
  syncBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  emptyCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 24,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  emptyTitle: {
    fontSize: 15,
    fontWeight: "700",
    color: "#0F172A",
  },
  emptyDesc: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 4,
    textAlign: "center",
  },
  itemCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  itemTitle: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
    flex: 1,
    marginRight: 8,
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  badgePending: {
    backgroundColor: "#EFF6FF",
  },
  badgeFailed: {
    backgroundColor: "#FFFBEB",
  },
  badgeConflict: {
    backgroundColor: "#FEF2F2",
  },
  statusText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#1E293B",
  },
  metaText: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 4,
  },
  errorText: {
    fontSize: 12,
    color: "#DC2626",
    marginTop: 6,
    fontWeight: "600",
  },
  actionsRow: {
    flexDirection: "row",
    justifyContent: "flex-end",
    marginTop: 8,
  },
  discardBtn: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 6,
    backgroundColor: "#FEF2F2",
  },
  discardBtnText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#DC2626",
  },
});

