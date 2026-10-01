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
  createIdempotencyKey,
  ExceptionQueueItemDto,
  formatMoney,
} from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";

interface VarianceItem {
  id: string;
  varianceType?: string;
  status: string;
  expectedAmount?: number;
  actualAmount?: number;
  varianceAmount?: number;
  currencyCode?: string;
  explanation?: string | null;
}

interface ReconciliationItem {
  id: string;
  reconciliationType?: string;
  ruleCode?: string;
  status: string;
  notes?: string | null;
  createdAt?: string;
}

interface BalanceDiscrepancyItem {
  id?: string;
  accountsPayableId?: string;
  accountsReceivableId?: string;
  documentNo?: string | null;
  partyName?: string | null;
  currencyCode?: string;
  recordedSettledAmount?: number;
  allocatedSumAmount?: number;
  discrepancyAmount?: number;
  rowVersion?: string | null;
}

interface ApArBalanceReconciliationDto {
  reconciliationDate?: string;
  isBalanced?: boolean;
  totalPayableExposure?: number;
  totalApRecognized?: number;
  totalApSettled?: number;
  totalReceivableExposure?: number;
  totalArRecognized?: number;
  totalArSettled?: number;
  apDiscrepancies?: BalanceDiscrepancyItem[];
  arDiscrepancies?: BalanceDiscrepancyItem[];
}

export default function ControlQueueScreen() {
  const { bootstrap, t, refreshBootstrap } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;
  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? false;

  const [subTab, setSubTab] = useState<
    "exceptions" | "variances" | "reconciliations" | "balance"
  >("exceptions");
  const [exceptions, setExceptions] = useState<ExceptionQueueItemDto[]>([]);
  const [variances, setVariances] = useState<VarianceItem[]>([]);
  const [reconciliations, setReconciliations] = useState<ReconciliationItem[]>([]);
  const [balanceRec, setBalanceRec] =
    useState<ApArBalanceReconciliationDto | null>(null);
  const [recType, setRecType] = useState<
    | "cost_vs_document"
    | "revenue_vs_document"
    | "ap_vs_payment"
    | "ar_vs_collection"
  >(canViewCost ? "cost_vs_document" : "revenue_vs_document");
  const [loading, setLoading] = useState<boolean>(true);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [resolutionReason, setResolutionReason] = useState<string>("");

  const loadAllControlQueues = useCallback(async () => {
    setLoading(true);
    try {
      const [excRes, varRes, recRes, balRes] = await Promise.all([
        apiRequest<ExceptionQueueItemDto[]>("/api/queues/exceptions").catch(() => []),
        apiRequest<VarianceItem[] | { items: VarianceItem[] }>("/api/variances").catch(
          () => [] as VarianceItem[]
        ),
        apiRequest<ReconciliationItem[] | { items: ReconciliationItem[] }>(
          "/api/reconciliations"
        ).catch(() => [] as ReconciliationItem[]),
        apiRequest<ApArBalanceReconciliationDto>(
          "/api/ap-ar/balance-reconciliation"
        ).catch(() => null),
      ]);
      setExceptions(Array.isArray(excRes) ? excRes : []);
      setVariances(Array.isArray(varRes) ? varRes : varRes?.items ?? []);
      setReconciliations(Array.isArray(recRes) ? recRes : recRes?.items ?? []);
      setBalanceRec(balRes);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAllControlQueues();
  }, [loadAllControlQueues]);

  const handleExceptionAction = async (
    item: ExceptionQueueItemDto,
    action: "resolve" | "escalate" | "waive"
  ) => {
    if (!resolutionReason.trim()) {
      Alert.alert(
        "Thiếu lý do kiểm soát",
        "Mọi thao tác xử lý, leo thang hoặc miễn trừ ngoại lệ đều bắt buộc nhập lý do để ghi Audit."
      );
      return;
    }

    try {
      const body =
        action === "resolve"
          ? { resolutionNotes: resolutionReason.trim() }
          : action === "escalate"
          ? { escalationReason: resolutionReason.trim() }
          : { reason: resolutionReason.trim() };

      await apiRequest(`/api/exceptions/${item.id}/${action}`, {
        method: "POST",
        ifMatch: item.rowVersion,
        idempotencyKey: createIdempotencyKey(`exc-${action}`),
        body,
      });
      setActiveId(null);
      setResolutionReason("");
      await Promise.all([loadAllControlQueues(), refreshBootstrap()]);
      Alert.alert("Đã cập nhật", "Đã ghi nhận xử lý ngoại lệ tài chính.");
    } catch (err) {
      Alert.alert(
        "Lỗi xử lý ngoại lệ",
        err instanceof Error ? err.message : "Không thể cập nhật ngoại lệ."
      );
    }
  };

  const handleVarianceAction = async (
    v: VarianceItem,
    action: "accept" | "clear" | "write-off"
  ) => {
    if (!resolutionReason.trim()) {
      Alert.alert(
        "Thiếu giải trình chênh lệch",
        "Vui lòng nhập giải trình cho chênh lệch này để lưu vết kiểm toán."
      );
      return;
    }

    if (action === "write-off") {
      const bio = await verifyBiometricForMoneyAction({
        actionLabelVi: "Xóa sổ Chênh lệch Tài chính (Variance Write-off)",
        amountSummaryVi: formatMoney(v.varianceAmount ?? 0, v.currencyCode ?? "VND"),
      });
      if (!bio.verified) return;
    }

    try {
      await apiRequest(`/api/variances/${v.id}/${action}`, {
        method: "POST",
        body: { explanation: resolutionReason.trim() },
      });
      setActiveId(null);
      setResolutionReason("");
      await Promise.all([loadAllControlQueues(), refreshBootstrap()]);
      Alert.alert("Đã xử lý chênh lệch", "Đã cập nhật trạng thái chênh lệch.");
    } catch (err) {
      Alert.alert(
        "Lỗi xử lý chênh lệch",
        err instanceof Error ? err.message : "Không thể cập nhật chênh lệch."
      );
    }
  };

  const handleReconciliationAction = async (
    rec: ReconciliationItem,
    action: "complete" | "replay"
  ) => {
    try {
      await apiRequest(`/api/reconciliations/${rec.id}/${action}`, {
        method: "POST",
        body: {},
      });
      await loadAllControlQueues();
      Alert.alert(
        "Đã cập nhật Đối soát",
        action === "complete"
          ? "Phiên đối soát đã được hoàn tất."
          : "Đã tạo phiên chạy lại đối soát (Replay)."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi đối soát",
        err instanceof Error ? err.message : "Không thể thực hiện thao tác đối soát."
      );
    }
  };

  const handleStartNewReconciliation = async () => {
    try {
      await apiRequest("/api/reconciliations", {
        method: "POST",
        body: {
          reconciliationType: recType,
          ruleCode: "STD_TOLERANCE",
          notes: `Khởi tạo đối soát (${recType}) từ ứng dụng di động LCMS`,
        },
      });
      await loadAllControlQueues();
      Alert.alert("Thành công", `Đã khởi tạo phiên đối soát ${recType}.`);
    } catch (err) {
      Alert.alert(
        "Lỗi tạo phiên đối soát",
        err instanceof Error ? err.message : "Không thể khởi tạo đối soát."
      );
    }
  };

  const handleFixDiscrepancy = async (
    side: "ap" | "ar",
    item: BalanceDiscrepancyItem
  ) => {
    if (!resolutionReason.trim()) {
      Alert.alert(
        "Thiếu lý do hiệu chỉnh",
        "Vui lòng nhập lý do giải trình hiệu chỉnh lệch số dư kế toán."
      );
      return;
    }
    const targetId =
      item.id ??
      (side === "ap" ? item.accountsPayableId : item.accountsReceivableId);
    if (!targetId) return;

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi:
        side === "ap"
          ? "Hiệu chỉnh Lệch Số dư Công nợ Phải trả (AP)"
          : "Hiệu chỉnh Lệch Số dư Công nợ Phải thu (AR)",
      amountSummaryVi: formatMoney(
        item.discrepancyAmount ?? 0,
        item.currencyCode ?? "VND"
      ),
    });
    if (!bio.verified) return;

    try {
      const endpoint =
        side === "ap"
          ? `/api/accounts-payable/${targetId}/settlement-correction`
          : `/api/accounts-receivable/${targetId}/settlement-correction`;
      await apiRequest(endpoint, {
        method: "POST",
        ifMatch: item.rowVersion,
        body: { reason: resolutionReason.trim() },
      });
      setResolutionReason("");
      await loadAllControlQueues();
      Alert.alert(
        "Đã đồng bộ số dư",
        "Đã hiệu chỉnh số dư kế toán khớp với tổng phân bổ thanh toán."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi hiệu chỉnh",
        err instanceof Error ? err.message : "Không thể hiệu chỉnh số dư."
      );
    }
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={loading}
          onRefresh={() => void loadAllControlQueues()}
        />
      }
    >
      <View style={styles.tabRow}>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "exceptions" && styles.tabBtnActive]}
          onPress={() => setSubTab("exceptions")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "exceptions" && styles.tabBtnTextActive,
            ]}
          >
            ⚠️ Ngoại lệ ({exceptions.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "variances" && styles.tabBtnActive]}
          onPress={() => setSubTab("variances")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "variances" && styles.tabBtnTextActive,
            ]}
          >
            📉 Chênh lệch ({variances.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.tabBtn,
            subTab === "reconciliations" && styles.tabBtnActive,
          ]}
          onPress={() => setSubTab("reconciliations")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "reconciliations" && styles.tabBtnTextActive,
            ]}
          >
            🔄 Đối soát ({reconciliations.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "balance" && styles.tabBtnActive]}
          onPress={() => setSubTab("balance")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "balance" && styles.tabBtnTextActive,
            ]}
          >
            ⚖️ Cân đối AP/AR
          </Text>
        </TouchableOpacity>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : subTab === "exceptions" ? (
        exceptions.length === 0 ? (
          <View style={styles.emptyCard}>
            <Text style={styles.emptyTitle}>Không có ngoại lệ tài chính đang mở</Text>
            <Text style={styles.emptySubtitle}>
              Hệ thống đang cân bằng và không phát sinh cảnh báo vượt ngưỡng.
            </Text>
          </View>
        ) : (
          exceptions.map((exc) => (
            <View key={exc.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.typeText}>{exc.exceptionType}</Text>
                <View
                  style={[
                    styles.sevBadge,
                    exc.severity === "critical" || exc.severity === "high"
                      ? styles.sevDanger
                      : styles.sevWarn,
                  ]}
                >
                  <Text style={styles.sevText}>
                    {t(`severity.${exc.severity}`, exc.severity)} •{" "}
                    {t(`exception.${exc.status}`, exc.status)}
                  </Text>
                </View>
              </View>

              <Text style={styles.messageText}>{exc.message}</Text>
              {exc.isOverdue ? (
                <Text style={styles.overdueText}>⚠️ Quá hạn SLA xử lý</Text>
              ) : null}

              <TextInput
                style={styles.input}
                placeholder="Nhập lý do xử lý / giải trình kiểm soát (bắt buộc)..."
                value={activeId === exc.id ? resolutionReason : ""}
                onFocus={() => setActiveId(exc.id)}
                onChangeText={(v) => {
                  setActiveId(exc.id);
                  setResolutionReason(v);
                }}
              />

              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={styles.resolveBtn}
                  onPress={() => handleExceptionAction(exc, "resolve")}
                >
                  <Text style={styles.resolveBtnText}>Xử lý dứt điểm</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.escalateBtn}
                  onPress={() => handleExceptionAction(exc, "escalate")}
                >
                  <Text style={styles.escalateBtnText}>Leo thang</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.ignoreBtn}
                  onPress={() => handleExceptionAction(exc, "waive")}
                >
                  <Text style={styles.ignoreBtnText}>Miễn trừ có lý do</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))
        )
      ) : subTab === "variances" ? (
        variances.length === 0 ? (
          <View style={styles.emptyCard}>
            <Text style={styles.emptyTitle}>Không có chênh lệch đang mở</Text>
            <Text style={styles.emptySubtitle}>
              Số liệu Dự kiến / Xác nhận / Thực tế đang nằm trong ngưỡng cho phép.
            </Text>
          </View>
        ) : (
          variances.map((v) => (
            <View key={v.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.typeText}>
                  {v.varianceType ?? "Chênh lệch Số liệu"}
                </Text>
                <Text style={styles.overdueText}>
                  {formatMoney(v.varianceAmount ?? 0, v.currencyCode ?? "VND")}
                </Text>
              </View>
              <Text style={styles.messageText}>
                Dự kiến: {formatMoney(v.expectedAmount ?? 0, v.currencyCode ?? "VND")} → Thực tế:{" "}
                {formatMoney(v.actualAmount ?? 0, v.currencyCode ?? "VND")} • Trạng thái:{" "}
                {v.status}
              </Text>

              <TextInput
                style={styles.input}
                placeholder="Nhập giải trình chênh lệch (bắt buộc)..."
                value={activeId === v.id ? resolutionReason : ""}
                onFocus={() => setActiveId(v.id)}
                onChangeText={(val) => {
                  setActiveId(v.id);
                  setResolutionReason(val);
                }}
              />

              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={styles.resolveBtn}
                  onPress={() => handleVarianceAction(v, "accept")}
                >
                  <Text style={styles.resolveBtnText}>Chấp nhận</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.escalateBtn}
                  onPress={() => handleVarianceAction(v, "clear")}
                >
                  <Text style={styles.escalateBtnText}>Xóa chênh lệch</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.ignoreBtn}
                  onPress={() => handleVarianceAction(v, "write-off")}
                >
                  <Text style={styles.ignoreBtnText}>Xóa sổ (Write-off)</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))
        )
      ) : subTab === "reconciliations" ? (
        <>
          <View style={styles.card}>
            <Text style={styles.typeText}>
              Chọn loại hình đối soát cần khởi tạo:
            </Text>
            <View style={styles.actionRow}>
              {(
                [
                  { k: "cost_vs_document", l: "Chi phí ↔ Hóa đơn" },
                  { k: "revenue_vs_document", l: "Doanh thu ↔ Hóa đơn" },
                  { k: "ap_vs_payment", l: "AP ↔ Phiếu chi" },
                  { k: "ar_vs_collection", l: "AR ↔ Phiếu thu" },
                ] as const
              ).map((opt) => (
                <TouchableOpacity
                  key={opt.k}
                  style={[
                    styles.ignoreBtn,
                    recType === opt.k && styles.resolveBtn,
                  ]}
                  onPress={() => setRecType(opt.k)}
                >
                  <Text
                    style={
                      recType === opt.k
                        ? styles.resolveBtnText
                        : styles.ignoreBtnText
                    }
                  >
                    {opt.l}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
            <TouchableOpacity
              style={[styles.resolveBtn, { marginTop: 10, alignItems: "center" }]}
              onPress={handleStartNewReconciliation}
            >
              <Text style={styles.resolveBtnText}>
                + Khởi tạo Phiên Đối soát ({recType})
              </Text>
            </TouchableOpacity>
          </View>

          {reconciliations.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>Chưa có phiên đối soát</Text>
            </View>
          ) : (
            reconciliations.map((rec) => (
              <View key={rec.id} style={styles.card}>
                <View style={styles.rowBetween}>
                  <Text style={styles.typeText}>
                    {rec.reconciliationType ?? "Đối soát"} • {rec.ruleCode ?? "STD"}
                  </Text>
                  <Text style={styles.sevText}>{rec.status}</Text>
                </View>
                <Text style={styles.messageText}>
                  {rec.notes ?? "Phiên đối soát tự động chứng từ & số dư"}
                </Text>
                <View style={styles.actionRow}>
                  {rec.status !== "completed" ? (
                    <TouchableOpacity
                      style={styles.resolveBtn}
                      onPress={() => handleReconciliationAction(rec, "complete")}
                    >
                      <Text style={styles.resolveBtnText}>✓ Hoàn tất Đối soát</Text>
                    </TouchableOpacity>
                  ) : null}
                  <TouchableOpacity
                    style={styles.escalateBtn}
                    onPress={() => handleReconciliationAction(rec, "replay")}
                  >
                    <Text style={styles.escalateBtnText}>🔄 Chạy lại (Replay)</Text>
                  </TouchableOpacity>
                </View>
              </View>
            ))
          )}
        </>
      ) : (
        <View style={styles.card}>
          <View style={styles.rowBetween}>
            <Text style={styles.typeText}>
              Cân đối &amp; Đối soát Số dư Công nợ Kế toán (AP/AR)
            </Text>
            <View
              style={[
                styles.sevBadge,
                balanceRec?.isBalanced !== false
                  ? { backgroundColor: "#DCFCE7" }
                  : styles.sevDanger,
              ]}
            >
              <Text
                style={[
                  styles.sevText,
                  balanceRec?.isBalanced !== false && { color: "#166534" },
                ]}
              >
                {balanceRec?.isBalanced !== false ? "✓ Cân bằng" : "⚠️ Có lệch"}
              </Text>
            </View>
          </View>

          {canViewCost ? (
            <Text style={styles.messageText}>
              • Phải trả (AP): Ghi nhận{" "}
              {formatMoney(balanceRec?.totalApRecognized ?? 0, "VND")} | Đã trả{" "}
              {formatMoney(balanceRec?.totalApSettled ?? 0, "VND")}
            </Text>
          ) : null}
          {canViewRevenue ? (
            <Text style={styles.messageText}>
              • Phải thu (AR): Ghi nhận{" "}
              {formatMoney(balanceRec?.totalArRecognized ?? 0, "VND")} | Đã thu{" "}
              {formatMoney(balanceRec?.totalArSettled ?? 0, "VND")}
            </Text>
          ) : null}

          <TextInput
            style={styles.input}
            placeholder="Nhập lý do giải trình hiệu chỉnh lệch số dư kế toán..."
            value={resolutionReason}
            onChangeText={setResolutionReason}
          />

          {(balanceRec?.apDiscrepancies ?? []).map((d, i) => (
            <View key={`ap-${i}`} style={styles.discrepancyRow}>
              <View style={{ flex: 1 }}>
                <Text style={styles.typeText}>
                  Lệch AP: {d.documentNo ?? d.accountsPayableId?.slice(0, 8)}
                </Text>
                <Text style={styles.messageText}>
                  Chênh lệch:{" "}
                  {formatMoney(d.discrepancyAmount ?? 0, d.currencyCode ?? "VND")}
                </Text>
              </View>
              <TouchableOpacity
                style={styles.resolveBtn}
                onPress={() => handleFixDiscrepancy("ap", d)}
              >
                <Text style={styles.resolveBtnText}>⚖️ Hiệu chỉnh AP</Text>
              </TouchableOpacity>
            </View>
          ))}

          {(balanceRec?.arDiscrepancies ?? []).map((d, i) => (
            <View key={`ar-${i}`} style={styles.discrepancyRow}>
              <View style={{ flex: 1 }}>
                <Text style={styles.typeText}>
                  Lệch AR: {d.documentNo ?? d.accountsReceivableId?.slice(0, 8)}
                </Text>
                <Text style={styles.messageText}>
                  Chênh lệch:{" "}
                  {formatMoney(d.discrepancyAmount ?? 0, d.currencyCode ?? "VND")}
                </Text>
              </View>
              <TouchableOpacity
                style={styles.resolveBtn}
                onPress={() => handleFixDiscrepancy("ar", d)}
              >
                <Text style={styles.resolveBtnText}>⚖️ Hiệu chỉnh AR</Text>
              </TouchableOpacity>
            </View>
          ))}
        </View>
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
  tabRow: {
    flexDirection: "row",
    gap: 5,
    marginBottom: 14,
  },
  tabBtn: {
    flex: 1,
    paddingVertical: 9,
    borderRadius: 10,
    backgroundColor: "#E2E8F0",
    alignItems: "center",
  },
  tabBtnActive: {
    backgroundColor: "#0F172A",
  },
  tabBtnText: {
    fontSize: 10,
    fontWeight: "700",
    color: "#334155",
  },
  tabBtnTextActive: {
    color: "#FFFFFF",
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
    marginTop: 4,
    textAlign: "center",
  },
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  typeText: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
  },
  sevBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  sevDanger: {
    backgroundColor: "#FEF2F2",
  },
  sevWarn: {
    backgroundColor: "#FFFBEB",
  },
  sevText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#991B1B",
  },
  messageText: {
    fontSize: 13,
    color: "#334155",
    marginTop: 6,
  },
  overdueText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#DC2626",
    marginTop: 4,
  },
  discrepancyRow: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingTop: 10,
    marginTop: 10,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 12,
    color: "#0F172A",
    backgroundColor: "#F8FAFC",
    marginTop: 10,
  },
  actionRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
    marginTop: 10,
  },
  resolveBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 10,
    paddingVertical: 8,
    borderRadius: 6,
  },
  resolveBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  escalateBtn: {
    backgroundColor: "#EFF6FF",
    paddingHorizontal: 10,
    paddingVertical: 8,
    borderRadius: 6,
  },
  escalateBtnText: {
    color: "#1D4ED8",
    fontSize: 12,
    fontWeight: "700",
  },
  ignoreBtn: {
    backgroundColor: "#F1F5F9",
    paddingHorizontal: 10,
    paddingVertical: 8,
    borderRadius: 6,
  },
  ignoreBtnText: {
    color: "#475569",
    fontSize: 12,
    fontWeight: "600",
  },
});
