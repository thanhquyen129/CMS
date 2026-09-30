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
  ApprovalQueueItemDto,
  createIdempotencyKey,
  formatMoney,
} from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";
import { BalanceImpactCard } from "../../src/components/BalanceImpactCard";
import { useAuth } from "../../src/auth/AuthContext";

export default function ApprovalsScreen() {
  const { t, refreshBootstrap } = useAuth();
  const [items, setItems] = useState<ApprovalQueueItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [decisionNote, setDecisionNote] = useState<string>("");
  const [submitting, setSubmitting] = useState<boolean>(false);

  const loadQueue = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiRequest<ApprovalQueueItemDto[]>(
        "/api/queues/approvals?status=pending"
      );
      setItems(Array.isArray(res) ? res : []);
    } catch {
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadQueue();
  }, [loadQueue]);

  const handleDecision = async (
    item: ApprovalQueueItemDto,
    decision: "approve" | "reject"
  ) => {
    if (decision === "reject" && !decisionNote.trim()) {
      Alert.alert(
        "Bắt buộc nhập lý do",
        "Vui lòng nhập lý do từ chối phê duyệt để lưu vết kiểm toán (Audit)."
      );
      return;
    }

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi:
        decision === "approve"
          ? `Phê duyệt ${item.actionType}`
          : `Từ chối ${item.actionType}`,
      amountSummaryVi: item.amount
        ? formatMoney(item.amount, item.currencyCode ?? "VND")
        : undefined,
    });

    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/approvals/${item.id}/${decision}`, {
        method: "POST",
        ifMatch: item.rowVersion,
        idempotencyKey: createIdempotencyKey(`apr-${decision}`),
        body: {
          reason: decisionNote.trim() || "Phê duyệt qua ứng dụng di động (Sinh trắc học)",
          note: decisionNote.trim() || undefined,
        },
      });
      setSelectedId(null);
      setDecisionNote("");
      await Promise.all([loadQueue(), refreshBootstrap()]);
      Alert.alert(
        "Thành công",
        decision === "approve"
          ? "Đã phê duyệt yêu cầu và ghi nhận nhật ký kiểm toán."
          : "Đã từ chối yêu cầu phê duyệt."
      );
    } catch (err) {
      Alert.alert(
        "Không thể thực hiện",
        err instanceof Error ? err.message : "Lỗi xử lý phê duyệt."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadQueue()} />
      }
    >
      <Text style={styles.heading}>
        Hàng đợi Phê duyệt ({items.length}) • Bảo vệ bởi Sinh trắc học & If-Match
      </Text>

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : items.length === 0 ? (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyTitle}>Không có yêu cầu nào đang chờ duyệt</Text>
          <Text style={styles.emptySubtitle}>
            Mọi yêu cầu vượt ngưỡng dung sai hoặc điều chỉnh số tiền sẽ hiển thị tại đây.
          </Text>
        </View>
      ) : (
        items.map((item) => {
          const isExpanded = selectedId === item.id;
          const amount = item.amount ?? 0;
          const currency = item.currencyCode ?? "VND";

          return (
            <View key={item.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.cardTitle}>
                  {item.objectDisplayRef ?? `${item.objectType.toUpperCase()}`} • Cấp{" "}
                  {item.requiredLevel}
                </Text>
                <View style={styles.pendingBadge}>
                  <Text style={styles.pendingBadgeText}>
                    {t(`approval.${item.status}`, "Chờ phê duyệt")}
                  </Text>
                </View>
              </View>

              <Text style={styles.actionType}>Thao tác: {item.actionType}</Text>
              {item.reason ? (
                <Text style={styles.reasonText}>Lý do trình: {item.reason}</Text>
              ) : null}

              <BalanceImpactCard
                titleVi="Tác động Số dư Tài chính (Before → After)"
                subtitleVi={
                  item.beforeSummary && item.afterSummary
                    ? `${item.beforeSummary} → ${item.afterSummary}`
                    : "Kiểm tra kỹ số tiền và nguyên tệ trước khi xác thực sinh trắc học"
                }
                currencyCode={currency}
                beforeAmount={0}
                afterAmount={amount}
                rowVersion={item.rowVersion}
                confirmLabelVi="Duyệt ngay (FaceID / Vân tay)"
                isSubmitting={submitting}
                onConfirmWithBiometric={() => handleDecision(item, "approve")}
                onCancel={() =>
                  setSelectedId(isExpanded ? null : item.id)
                }
              />

              <TextInput
                style={styles.input}
                placeholder="Ghi chú phê duyệt hoặc lý do từ chối (bắt buộc nếu từ chối)..."
                value={selectedId === item.id ? decisionNote : ""}
                onFocus={() => setSelectedId(item.id)}
                onChangeText={(val) => {
                  setSelectedId(item.id);
                  setDecisionNote(val);
                }}
              />

              <View style={styles.rejectRow}>
                <TouchableOpacity
                  style={styles.rejectBtn}
                  onPress={() => handleDecision(item, "reject")}
                  disabled={submitting}
                >
                  <Text style={styles.rejectBtnText}>Từ chối yêu cầu</Text>
                </TouchableOpacity>
              </View>
            </View>
          );
        })
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
  heading: {
    fontSize: 14,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 12,
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
  emptySubtitle: {
    fontSize: 12,
    color: "#64748B",
    textAlign: "center",
    marginTop: 6,
  },
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    marginBottom: 14,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  cardTitle: {
    fontSize: 15,
    fontWeight: "700",
    color: "#0F172A",
  },
  pendingBadge: {
    backgroundColor: "#FFFBEB",
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  pendingBadgeText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#B45309",
  },
  actionType: {
    fontSize: 13,
    fontWeight: "600",
    color: "#334155",
    marginTop: 4,
  },
  reasonText: {
    fontSize: 12,
    color: "#475569",
    marginTop: 3,
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
    marginTop: 6,
  },
  rejectRow: {
    flexDirection: "row",
    justifyContent: "flex-end",
    marginTop: 8,
  },
  rejectBtn: {
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 6,
    backgroundColor: "#FEF2F2",
    borderWidth: 1,
    borderColor: "#FECACA",
  },
  rejectBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#DC2626",
  },
});

