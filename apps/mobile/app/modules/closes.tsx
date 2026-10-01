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
import { createIdempotencyKey, formatMoney } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";

interface FinancialCloseDto {
  id: string;
  closeNo?: string | null;
  status: string;
  scopeType?: string | null;
  periodFrom?: string | null;
  periodTo?: string | null;
  baseCurrency?: string | null;
  closedAt?: string | null;
  closedBy?: string | null;
  notes?: string | null;
  rowVersion?: string | null;
}

interface FinancialCloseEligibilityDto {
  isEligible: boolean;
  blockers?: string[];
  pendingApprovalsCount?: number;
  unreconciledCount?: number;
  openExceptionsCount?: number;
  unallocatedCostsCount?: number;
  unmappedRevenuesCount?: number;
}

interface FinancialClosePnlDto {
  totalRevenue?: number | null;
  totalCost?: number | null;
  grossProfit?: number | null;
  marginPercent?: number | null;
  baseCurrency?: string | null;
}

interface FinancialCloseSnapshotDto {
  id: string;
  snapshotHash?: string | null;
  sha256Hash?: string | null;
  createdAt?: string | null;
  createdBy?: string | null;
  policyVersion?: string | null;
}

export default function FinancialClosesModuleScreen() {
  const { bootstrap } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;
  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? false;
  const canViewMargin = bootstrap?.financialVisibility.canViewMargin ?? false;

  const [closes, setCloses] = useState<FinancialCloseDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Create Close Form
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [periodFrom, setPeriodFrom] = useState<string>("2026-09-01");
  const [periodTo, setPeriodTo] = useState<string>("2026-09-30");
  const [baseCurrency, setBaseCurrency] = useState<"VND" | "USD">("VND");
  const [notes, setNotes] = useState<string>("");

  // Selected Close Detail
  const [selectedClose, setSelectedClose] = useState<FinancialCloseDto | null>(
    null
  );
  const [eligibility, setEligibility] =
    useState<FinancialCloseEligibilityDto | null>(null);
  const [pnl, setPnl] = useState<FinancialClosePnlDto | null>(null);
  const [snapshots, setSnapshots] = useState<FinancialCloseSnapshotDto[]>([]);
  const [reopenReason, setReopenReason] = useState<string>("");

  const loadCloses = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiRequest<
        FinancialCloseDto[] | { items: FinancialCloseDto[] }
      >("/api/financial-closes");
      const list = Array.isArray(res) ? res : res?.items ?? [];
      setCloses(list);
    } catch {
      setCloses([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadCloses();
  }, [loadCloses]);

  const loadCloseDetails = async (close: FinancialCloseDto) => {
    setSelectedClose(close);
    setEligibility(null);
    setPnl(null);
    setSnapshots([]);
    try {
      const [eligRes, pnlRes, snapRes] = await Promise.all([
        apiRequest<FinancialCloseEligibilityDto>(
          `/api/financial-closes/${close.id}/eligibility`
        ).catch(() => null),
        apiRequest<FinancialClosePnlDto>(
          `/api/financial-closes/${close.id}/pnl`
        ).catch(() => null),
        apiRequest<
          FinancialCloseSnapshotDto[] | { items: FinancialCloseSnapshotDto[] }
        >(`/api/financial-closes/${close.id}/snapshots`).catch(
          () => [] as FinancialCloseSnapshotDto[]
        ),
      ]);
      setEligibility(eligRes);
      setPnl(pnlRes);
      setSnapshots(Array.isArray(snapRes) ? snapRes : snapRes?.items ?? []);
    } catch {
      // handled per promise
    }
  };

  const handleStartClose = async () => {
    if (!periodFrom.trim() || !periodTo.trim()) {
      Alert.alert("Thiếu kỳ kế toán", "Vui lòng nhập Từ ngày và Đến ngày.");
      return;
    }
    setSubmitting(true);
    try {
      await apiRequest("/api/financial-closes", {
        method: "POST",
        idempotencyKey: createIdempotencyKey("close-start"),
        body: {
          scopeType: "tenant",
          periodFrom: periodFrom.trim(),
          periodTo: periodTo.trim(),
          baseCurrency,
          policyVersion: "ADR-0035-v1",
          notes:
            notes.trim() ||
            `Kỳ chốt tài chính ${periodFrom} → ${periodTo} từ Mobile`,
        },
      });
      setShowCreate(false);
      setNotes("");
      await loadCloses();
      Alert.alert(
        "Đã khởi tạo Kỳ chốt",
        `Kỳ tài chính ${periodFrom} → ${periodTo} đã được mở để kiểm tra điều kiện khóa sổ.`
      );
    } catch (err) {
      Alert.alert(
        "Không thể tạo kỳ chốt",
        err instanceof Error ? err.message : "Lỗi khi khởi tạo kỳ chốt sổ."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleExecuteSnapshot = async (close: FinancialCloseDto) => {
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Chốt kỳ Tài chính & Ký Bản chụp Bất biến SHA-256",
      amountSummaryVi: `${close.periodFrom ?? ""} → ${close.periodTo ?? ""}`,
    });
    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/financial-closes/${close.id}/snapshot`, {
        method: "POST",
        ifMatch: close.rowVersion,
        idempotencyKey: createIdempotencyKey("close-snap"),
        body: {},
      });
      await loadCloses();
      await loadCloseDetails(close);
      Alert.alert(
        "Đã khóa sổ & tạo Snapshot",
        "Bản chụp P&L bất biến (SHA-256) đã được ghi nhận thành công."
      );
    } catch (err) {
      Alert.alert(
        "Không thể chốt sổ",
        err instanceof Error ? err.message : "Kỳ tài chính chưa đủ điều kiện khóa sổ."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleReopenClose = async (close: FinancialCloseDto) => {
    if (!reopenReason.trim()) {
      Alert.alert(
        "Thiếu lý do kiểm toán",
        "Mở lại kỳ tài chính đã khóa bắt buộc phải nhập lý do giải trình."
      );
      return;
    }

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Mở lại Kỳ Tài chính Đã khóa (Reopen Close)",
      amountSummaryVi: `Kỳ ${close.periodFrom ?? ""} → ${close.periodTo ?? ""}`,
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      await apiRequest(`/api/financial-closes/${close.id}/reopen`, {
        method: "POST",
        ifMatch: close.rowVersion,
        body: { reason: reopenReason.trim() },
      });
      setReopenReason("");
      await loadCloses();
      await loadCloseDetails(close);
      Alert.alert(
        "Đã mở lại kỳ tài chính",
        "Kỳ kế toán đã được mở lại và ghi nhận AuditEvent."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi mở lại kỳ",
        err instanceof Error ? err.message : "Không thể mở lại kỳ tài chính."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={loading}
          onRefresh={() => void loadCloses()}
        />
      }
    >
      <View style={styles.headerRow}>
        <Text style={styles.sectionTitle}>
          Kỳ Chốt Sổ Tài chính ({closes.length})
        </Text>
        <TouchableOpacity
          style={styles.primaryBtn}
          onPress={() => setShowCreate((v) => !v)}
        >
          <Text style={styles.primaryBtnText}>
            {showCreate ? "✕ Đóng" : "+ Mở Kỳ Chốt Mới"}
          </Text>
        </TouchableOpacity>
      </View>

      {showCreate ? (
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>Khởi tạo Kỳ Chốt Sổ Tài chính</Text>
          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Từ ngày (YYYY-MM-DD)</Text>
              <TextInput
                style={styles.input}
                value={periodFrom}
                onChangeText={setPeriodFrom}
                placeholder="2026-09-01"
              />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Đến ngày (YYYY-MM-DD)</Text>
              <TextInput
                style={styles.input}
                value={periodTo}
                onChangeText={setPeriodTo}
                placeholder="2026-09-30"
              />
            </View>
          </View>

          <Text style={styles.label}>Đồng tiền Báo cáo Cơ sở</Text>
          <View style={styles.chipRow}>
            {(["VND", "USD"] as const).map((cur) => (
              <TouchableOpacity
                key={cur}
                style={[styles.chip, baseCurrency === cur && styles.chipActive]}
                onPress={() => setBaseCurrency(cur)}
              >
                <Text
                  style={[
                    styles.chipText,
                    baseCurrency === cur && styles.chipTextActive,
                  ]}
                >
                  {cur}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <Text style={styles.label}>Ghi chú kỳ chốt sổ</Text>
          <TextInput
            style={styles.input}
            value={notes}
            onChangeText={setNotes}
            placeholder="VD: Chốt sổ tháng 09/2026 toàn công ty"
          />

          <TouchableOpacity
            style={styles.submitBtn}
            onPress={handleStartClose}
            disabled={submitting}
          >
            <Text style={styles.submitBtnText}>+ Tạo Kỳ Chốt Sổ</Text>
          </TouchableOpacity>
        </View>
      ) : null}

      {selectedClose ? (
        <View style={styles.detailCard}>
          <View style={styles.rowBetween}>
            <Text style={styles.formTitle}>
              Kỳ: {selectedClose.periodFrom ?? "—"} →{" "}
              {selectedClose.periodTo ?? "—"} ({selectedClose.status})
            </Text>
            <TouchableOpacity onPress={() => setSelectedClose(null)}>
              <Text style={styles.closeLink}>Đóng ✕</Text>
            </TouchableOpacity>
          </View>

          {eligibility ? (
            <View style={styles.eligBox}>
              <Text style={styles.subHeader}>
                1. Kiểm tra Điều kiện Khóa sổ (Eligibility Gate):
              </Text>
              <Text
                style={[
                  styles.eligStatus,
                  { color: eligibility.isEligible ? "#166534" : "#B91C1C" },
                ]}
              >
                {eligibility.isEligible
                  ? "✓ Đủ điều kiện chốt sổ & tạo bản chụp SHA-256"
                  : "⚠️ Còn tồn đọng cần xử lý trước khi chốt sổ"}
              </Text>
              <Text style={styles.metaText}>
                • Chờ phê duyệt: {eligibility.pendingApprovalsCount ?? 0} | Ngoại lệ mở:{" "}
                {eligibility.openExceptionsCount ?? 0} | Chưa đối soát:{" "}
                {eligibility.unreconciledCount ?? 0}
              </Text>
              <Text style={styles.metaText}>
                • Chi phí chung chưa phân bổ:{" "}
                {eligibility.unallocatedCostsCount ?? 0} | Doanh thu chưa gán Bill:{" "}
                {eligibility.unmappedRevenuesCount ?? 0}
              </Text>
              {eligibility.blockers && eligibility.blockers.length > 0 ? (
                <View style={{ marginTop: 6 }}>
                  {eligibility.blockers.map((b, i) => (
                    <Text key={i} style={styles.blockerText}>
                      ⛔ {b}
                    </Text>
                  ))}
                </View>
              ) : null}
            </View>
          ) : null}

          {pnl ? (
            <View style={styles.pnlBox}>
              <Text style={styles.subHeader}>
                2. Tổng hợp Kết quả Kinh doanh Kỳ ({pnl.baseCurrency ?? "VND"}):
              </Text>
              <View style={styles.pnlGrid}>
                {canViewCost ? (
                  <View style={styles.pnlCell}>
                    <Text style={styles.pnlLabel}>Tổng Chi phí</Text>
                    <Text style={styles.pnlValue}>
                      {formatMoney(pnl.totalCost ?? 0, pnl.baseCurrency ?? "VND")}
                    </Text>
                  </View>
                ) : null}
                {canViewRevenue ? (
                  <View style={styles.pnlCell}>
                    <Text style={styles.pnlLabel}>Tổng Doanh thu</Text>
                    <Text style={styles.pnlValue}>
                      {formatMoney(
                        pnl.totalRevenue ?? 0,
                        pnl.baseCurrency ?? "VND"
                      )}
                    </Text>
                  </View>
                ) : null}
                {canViewMargin ? (
                  <View style={styles.pnlCell}>
                    <Text style={styles.pnlLabel}>Lợi nhuận gộp</Text>
                    <Text style={[styles.pnlValue, { color: "#0F766E" }]}>
                      {formatMoney(
                        pnl.grossProfit ?? 0,
                        pnl.baseCurrency ?? "VND"
                      )}{" "}
                      ({(pnl.marginPercent ?? 0).toFixed(1)}%)
                    </Text>
                  </View>
                ) : null}
              </View>
            </View>
          ) : null}

          <View style={{ marginTop: 10 }}>
            <Text style={styles.subHeader}>
              3. Bản chụp Bất biến SHA-256 ({snapshots.length}):
            </Text>
            {snapshots.length === 0 ? (
              <Text style={styles.metaText}>
                Chưa có bản chụp khóa sổ nào cho kỳ này.
              </Text>
            ) : (
              snapshots.map((snap) => (
                <View key={snap.id} style={styles.snapRow}>
                  <Text style={styles.snapHash}>
                    🔐 SHA-256:{" "}
                    {(snap.snapshotHash ?? snap.sha256Hash ?? snap.id).slice(
                      0,
                      24
                    )}
                    …
                  </Text>
                  <Text style={styles.metaText}>
                    Lúc: {snap.createdAt ? snap.createdAt.slice(0, 16) : "—"} •
                    Quy chuẩn: Chuẩn mực Kế toán Doanh nghiệp
                  </Text>
                </View>
              ))
            )}
          </View>

          <View style={styles.actionZone}>
            <TouchableOpacity
              style={styles.submitBtn}
              onPress={() => handleExecuteSnapshot(selectedClose)}
              disabled={submitting}
            >
              <Text style={styles.submitBtnText}>
                🔒 Xác nhận Sinh trắc học &amp; Khóa sổ Kế toán
              </Text>
            </TouchableOpacity>

            <View style={{ marginTop: 10 }}>
              <TextInput
                style={styles.input}
                placeholder="Nhập lý do mở lại kỳ tài chính (bắt buộc khi Reopen)..."
                value={reopenReason}
                onChangeText={setReopenReason}
              />
              <TouchableOpacity
                style={styles.reopenBtn}
                onPress={() => handleReopenClose(selectedClose)}
                disabled={submitting}
              >
                <Text style={styles.reopenBtnText}>
                  🔓 Yêu cầu Mở lại Kỳ (Reopen Close có Audit)
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : closes.length === 0 ? (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyTitle}>Chưa có kỳ chốt sổ tài chính</Text>
          <Text style={styles.emptySubtitle}>
            Nhấn &quot;+ Mở Kỳ Chốt Mới&quot; để khởi tạo quy trình kiểm tra điều kiện khóa sổ.
          </Text>
        </View>
      ) : (
        closes.map((c) => (
          <TouchableOpacity
            key={c.id}
            style={styles.card}
            onPress={() => void loadCloseDetails(c)}
          >
            <View style={styles.rowBetween}>
              <Text style={styles.cardTitle}>
                {c.closeNo ?? `Kỳ ${c.periodFrom ?? ""} → ${c.periodTo ?? ""}`}
              </Text>
              <View style={styles.badge}>
                <Text style={styles.badgeText}>{c.status}</Text>
              </View>
            </View>
            <Text style={styles.metaText}>
              Từ {c.periodFrom ?? "—"} đến {c.periodTo ?? "—"} • Tiền tệ cơ sở:{" "}
              {c.baseCurrency ?? "VND"}
            </Text>
            {c.notes ? <Text style={styles.metaText}>{c.notes}</Text> : null}
          </TouchableOpacity>
        ))
      )}

      <View style={{ height: 36 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 16,
  },
  headerRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
  },
  primaryBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 8,
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  formCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#BFDBFE",
    marginBottom: 14,
  },
  detailCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#93C5FD",
    marginBottom: 14,
  },
  formTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 6,
  },
  subHeader: {
    fontSize: 13,
    fontWeight: "800",
    color: "#1E293B",
    marginBottom: 4,
  },
  label: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 4,
    marginTop: 6,
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 13,
    backgroundColor: "#F8FAFC",
    color: "#0F172A",
  },
  twoCol: {
    flexDirection: "row",
    gap: 10,
  },
  chipRow: {
    flexDirection: "row",
    gap: 6,
    marginVertical: 4,
  },
  chip: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 6,
    backgroundColor: "#F8FAFC",
  },
  chipActive: {
    borderColor: "#2563EB",
    backgroundColor: "#EFF6FF",
  },
  chipText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#475569",
  },
  chipTextActive: {
    color: "#1D4ED8",
    fontWeight: "700",
  },
  submitBtn: {
    backgroundColor: "#0F172A",
    borderRadius: 8,
    paddingVertical: 11,
    alignItems: "center",
    marginTop: 10,
  },
  submitBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  reopenBtn: {
    backgroundColor: "#FFFBEB",
    borderWidth: 1,
    borderColor: "#FDE68A",
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: "center",
    marginTop: 6,
  },
  reopenBtnText: {
    color: "#B45309",
    fontSize: 12,
    fontWeight: "700",
  },
  eligBox: {
    backgroundColor: "#F8FAFC",
    borderRadius: 8,
    padding: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginTop: 6,
  },
  eligStatus: {
    fontSize: 13,
    fontWeight: "700",
    marginBottom: 4,
  },
  blockerText: {
    fontSize: 12,
    color: "#DC2626",
    fontWeight: "600",
    marginTop: 2,
  },
  pnlBox: {
    backgroundColor: "#F0FDF4",
    borderRadius: 8,
    padding: 10,
    borderWidth: 1,
    borderColor: "#BBF7D0",
    marginTop: 10,
  },
  pnlGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 12,
    marginTop: 4,
  },
  pnlCell: {
    minWidth: 120,
  },
  pnlLabel: {
    fontSize: 11,
    color: "#475569",
  },
  pnlValue: {
    fontSize: 14,
    fontWeight: "800",
    color: "#0F172A",
    marginTop: 2,
  },
  snapRow: {
    backgroundColor: "#F8FAFC",
    padding: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginTop: 4,
  },
  snapHash: {
    fontSize: 12,
    fontWeight: "700",
    color: "#0F172A",
  },
  actionZone: {
    marginTop: 10,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: "#E2E8F0",
  },
  card: {
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
  cardTitle: {
    fontSize: 14,
    fontWeight: "800",
    color: "#0F172A",
  },
  badge: {
    backgroundColor: "#EFF6FF",
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#1D4ED8",
  },
  metaText: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 4,
  },
  closeLink: {
    fontSize: 12,
    fontWeight: "700",
    color: "#64748B",
  },
  emptyCard: {
    backgroundColor: "#FFFFFF",
    padding: 22,
    borderRadius: 12,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  emptyTitle: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
  },
  emptySubtitle: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 4,
    textAlign: "center",
  },
});
