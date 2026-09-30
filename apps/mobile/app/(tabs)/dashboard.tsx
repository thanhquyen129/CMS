import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import { DashboardSummaryDto, formatMoney } from "@lcms/shared";
import { useAuth } from "../../src/auth/AuthContext";
import { apiRequest } from "../../src/api/client";

const PERSONA_LABELS: Record<string, string> = {
  executive_control: "Điều hành & Kiểm soát Tài chính",
  cost_accountant: "Kế toán Chi phí & Công nợ Phải trả (Cost/AP)",
  revenue_accountant: "Kế toán Doanh thu & Công nợ Phải thu (Rev/AR)",
  field_ops: "Vận hành Hiện trường (Field Ops)",
  master_data: "Quản trị Danh mục (Master Data)",
  viewer: "Tra cứu (Viewer)",
};

export default function DashboardScreen() {
  const router = useRouter();
  const { bootstrap, refreshBootstrap } = useAuth();
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [refreshing, setRefreshing] = useState<boolean>(false);

  const loadData = useCallback(async () => {
    try {
      const [sum] = await Promise.all([
        apiRequest<DashboardSummaryDto>("/api/dashboard/summary?includeBaseCurrencyRollUp=true"),
        refreshBootstrap(),
      ]);
      setSummary(sum);
    } catch {
      // Offline or restricted
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [refreshBootstrap]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const vis = bootstrap?.financialVisibility ?? {
    canViewCost: false,
    canViewRevenue: false,
    canViewMargin: false,
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => {
            setRefreshing(true);
            void loadData();
          }}
        />
      }
    >
      {/* Role & Tenant Identity Banner */}
      <View style={styles.identityCard}>
        <View style={styles.rowBetween}>
          <View style={{ flex: 1 }}>
            <Text style={styles.tenantName}>
              {bootstrap?.tenant.name ?? "Thuê bao LCMS"} ({bootstrap?.tenant.code})
            </Text>
            <Text style={styles.userName}>
              {bootstrap?.user.displayName} •{" "}
              {PERSONA_LABELS[bootstrap?.primaryPersona ?? "viewer"] ??
                bootstrap?.primaryPersona}
            </Text>
          </View>
          <View style={styles.currencyPill}>
            <Text style={styles.currencyPillText}>
              Đồng tiền báo cáo: {bootstrap?.tenant.defaultCurrencyCode ?? "VND"}
            </Text>
          </View>
        </View>

        <View style={styles.sodRow}>
          <View
            style={[
              styles.sodChip,
              vis.canViewCost ? styles.sodChipOn : styles.sodChipOff,
            ]}
          >
            <Text style={styles.sodChipText}>
              Chi phí: {vis.canViewCost ? "Được phép" : "Ẩn (SoD)"}
            </Text>
          </View>
          <View
            style={[
              styles.sodChip,
              vis.canViewRevenue ? styles.sodChipOn : styles.sodChipOff,
            ]}
          >
            <Text style={styles.sodChipText}>
              Doanh thu: {vis.canViewRevenue ? "Được phép" : "Ẩn (SoD)"}
            </Text>
          </View>
          <View
            style={[
              styles.sodChip,
              vis.canViewMargin ? styles.sodChipOn : styles.sodChipOff,
            ]}
          >
            <Text style={styles.sodChipText}>
              Biên LN: {vis.canViewMargin ? "Được phép" : "Ẩn (SoD)"}
            </Text>
          </View>
        </View>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 32 }} />
      ) : (
        <>
          {/* KPI Control Tiles */}
          <View style={styles.kpiGrid}>
            <TouchableOpacity
              style={styles.kpiCard}
              onPress={() => router.push("/(tabs)/bills")}
            >
              <Text style={styles.kpiLabel}>Tổng Vận đơn (Bills)</Text>
              <Text style={styles.kpiValue}>{summary?.billCount ?? 0}</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.kpiCard}
              onPress={() => router.push("/(tabs)/approvals")}
            >
              <Text style={styles.kpiLabel}>Chờ phê duyệt</Text>
              <Text style={[styles.kpiValue, { color: "#D97706" }]}>
                {summary?.pendingApprovalCount ?? bootstrap?.badges.pendingApprovals ?? 0}
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.kpiCard}
              onPress={() => router.push("/(tabs)/control")}
            >
              <Text style={styles.kpiLabel}>Ngoại lệ đang mở</Text>
              <Text style={[styles.kpiValue, { color: "#DC2626" }]}>
                {summary?.openExceptionCount ?? bootstrap?.badges.openExceptions ?? 0}
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.kpiCard}
              onPress={() => router.push("/(tabs)/control")}
            >
              <Text style={styles.kpiLabel}>Chênh lệch & FX</Text>
              <Text style={[styles.kpiValue, { color: "#2563EB" }]}>
                {(summary?.openVarianceCount ?? 0) +
                  (bootstrap?.badges.fxExceptions ?? 0)}
              </Text>
            </TouchableOpacity>
          </View>

          {/* Base Currency Roll-up (only for sides user is permitted to see) */}
          {summary?.baseCurrencyRollUp ? (
            <View style={styles.sectionCard}>
              <Text style={styles.sectionTitle}>
                Tổng hợp Quy đổi Đồng tiền Báo cáo ({summary.baseCurrencyRollUp.baseCurrency})
              </Text>
              <Text style={styles.sectionNote}>
                Nguyên tắc Best Available: Thực tế (Actual) → Đã xác nhận (Confirmed) → Dự kiến (Expected)
              </Text>

              {vis.canViewCost ? (
                <View style={styles.moneyLine}>
                  <Text style={styles.moneyLineLabel}>Tổng Chi phí quy đổi</Text>
                  <Text style={styles.moneyLineValue}>
                    {formatMoney(
                      summary.baseCurrencyRollUp.costBestAvailableBase,
                      summary.baseCurrencyRollUp.baseCurrency
                    )}
                  </Text>
                </View>
              ) : null}

              {vis.canViewRevenue ? (
                <View style={styles.moneyLine}>
                  <Text style={styles.moneyLineLabel}>Tổng Doanh thu quy đổi</Text>
                  <Text style={styles.moneyLineValue}>
                    {formatMoney(
                      summary.baseCurrencyRollUp.revenueBestAvailableBase,
                      summary.baseCurrencyRollUp.baseCurrency
                    )}
                  </Text>
                </View>
              ) : null}

              {vis.canViewMargin ? (
                <View style={[styles.moneyLine, styles.marginLine]}>
                  <Text style={styles.marginLabel}>Lợi nhuận gộp (Margin)</Text>
                  <Text style={styles.marginValue}>
                    {formatMoney(
                      summary.baseCurrencyRollUp.profitBestAvailableBase,
                      summary.baseCurrencyRollUp.baseCurrency
                    )}
                  </Text>
                </View>
              ) : null}

              {summary.baseCurrencyRollUp.fxStubNote ? (
                <Text style={styles.fxNote}>{summary.baseCurrencyRollUp.fxStubNote}</Text>
              ) : null}
            </View>
          ) : null}

          {/* Totals by Original Transaction Currency (never mixed) */}
          {summary?.totalsByCurrency && summary.totalsByCurrency.length > 0 ? (
            <View style={styles.sectionCard}>
              <Text style={styles.sectionTitle}>
                Chi tiết theo Nguyên tệ Giao dịch (Không cộng gộp khác loại tiền)
              </Text>
              {summary.totalsByCurrency.map((row) => (
                <View key={row.currencyCode} style={styles.currencyBlock}>
                  <Text style={styles.currencyCodeHeader}>
                    Nguyên tệ: {row.currencyCode}
                  </Text>
                  {vis.canViewCost ? (
                    <Text style={styles.currencyRowText}>
                      • Chi phí: {formatMoney(row.costBestAvailable, row.currencyCode)}
                    </Text>
                  ) : null}
                  {vis.canViewRevenue ? (
                    <Text style={styles.currencyRowText}>
                      • Doanh thu: {formatMoney(row.revenueBestAvailable, row.currencyCode)}
                    </Text>
                  ) : null}
                  {vis.canViewMargin ? (
                    <Text style={[styles.currencyRowText, { fontWeight: "700" }]}>
                      • Chênh lệch: {formatMoney(row.profitBestAvailable, row.currencyCode)}
                    </Text>
                  ) : null}
                </View>
              ))}
            </View>
          ) : null}

          {/* Maturity Pipeline */}
          {summary?.maturityPipeline ? (
            <View style={styles.sectionCard}>
              <Text style={styles.sectionTitle}>
                Độ trưởng thành Số liệu (Expected → Confirmed → Actual)
              </Text>
              {vis.canViewCost ? (
                <Text style={styles.pipelineText}>
                  Chi phí: Dự kiến ({summary.maturityPipeline.costExpectedOnlyCount}) → Đã xác nhận (
                  {summary.maturityPipeline.costConfirmedOnlyCount}) → Thực tế (
                  {summary.maturityPipeline.costActualCount})
                </Text>
              ) : null}
              {vis.canViewRevenue ? (
                <Text style={styles.pipelineText}>
                  Doanh thu: Dự kiến ({summary.maturityPipeline.revenueExpectedOnlyCount}) → Đã xác nhận (
                  {summary.maturityPipeline.revenueConfirmedOnlyCount}) → Thực tế (
                  {summary.maturityPipeline.revenueActualCount})
                </Text>
              ) : null}
            </View>
          ) : null}
        </>
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
  identityCard: {
    backgroundColor: "#0F172A",
    borderRadius: 14,
    padding: 16,
    marginBottom: 16,
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
  },
  tenantName: {
    color: "#FFFFFF",
    fontSize: 16,
    fontWeight: "800",
  },
  userName: {
    color: "#94A3B8",
    fontSize: 12,
    marginTop: 3,
  },
  currencyPill: {
    backgroundColor: "#1E293B",
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  currencyPillText: {
    color: "#E2E8F0",
    fontSize: 11,
    fontWeight: "600",
  },
  sodRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
    marginTop: 12,
  },
  sodChip: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  sodChipOn: {
    backgroundColor: "#065F46",
  },
  sodChipOff: {
    backgroundColor: "#334155",
  },
  sodChipText: {
    color: "#FFFFFF",
    fontSize: 11,
    fontWeight: "600",
  },
  kpiGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    justifyContent: "space-between",
    marginBottom: 8,
  },
  kpiCard: {
    width: "48%",
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  kpiLabel: {
    fontSize: 12,
    color: "#64748B",
    fontWeight: "600",
  },
  kpiValue: {
    fontSize: 22,
    fontWeight: "800",
    color: "#0F172A",
    marginTop: 4,
  },
  sectionCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 14,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
    marginBottom: 4,
  },
  sectionNote: {
    fontSize: 11,
    color: "#64748B",
    marginBottom: 10,
  },
  moneyLine: {
    flexDirection: "row",
    justifyContent: "space-between",
    paddingVertical: 6,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  moneyLineLabel: {
    fontSize: 13,
    color: "#334155",
  },
  moneyLineValue: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
  },
  marginLine: {
    backgroundColor: "#F8FAFC",
    paddingHorizontal: 8,
    borderRadius: 6,
    marginTop: 4,
  },
  marginLabel: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F766E",
  },
  marginValue: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F766E",
  },
  fxNote: {
    fontSize: 11,
    color: "#D97706",
    marginTop: 8,
  },
  currencyBlock: {
    paddingVertical: 8,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  currencyCodeHeader: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
    marginBottom: 3,
  },
  currencyRowText: {
    fontSize: 12,
    color: "#334155",
    marginTop: 2,
  },
  pipelineText: {
    fontSize: 12,
    color: "#334155",
    marginTop: 6,
    lineHeight: 18,
  },
});

