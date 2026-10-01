import React, { useCallback, useEffect, useMemo, useState } from "react";
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

type PerspectiveKey = "executive" | "ops" | "ar" | "ap" | "audit";

interface PerspectiveOption {
  key: PerspectiveKey;
  label: string;
  icon: string;
  subtitle: string;
}

const PERSPECTIVES: PerspectiveOption[] = [
  {
    key: "executive",
    label: "Ban Giám đốc",
    icon: "🌟",
    subtitle: "Tổng quan Tài chính & Hiệu quả Điều hành",
  },
  {
    key: "ops",
    label: "Vận hành",
    icon: "🚚",
    subtitle: "Vận đơn, Chuyến hàng & Chi phí Hiện trường",
  },
  {
    key: "ar",
    label: "Phải thu (AR)",
    icon: "💰",
    subtitle: "Doanh thu Khách hàng & Thu hồi Công nợ",
  },
  {
    key: "ap",
    label: "Phải trả (AP)",
    icon: "🧾",
    subtitle: "Chi phí Nhà xe & Kế hoạch Thanh toán",
  },
  {
    key: "audit",
    label: "Kiểm soát",
    icon: "🛡️",
    subtitle: "Bất thường, Cân đối & Tuân thủ Kế toán",
  },
];

export default function DashboardScreen() {
  const router = useRouter();
  const { bootstrap, refreshBootstrap } = useAuth();
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [refreshing, setRefreshing] = useState<boolean>(false);

  // Financial visibility rules based on business authorization
  const vis = bootstrap?.financialVisibility ?? {
    canViewCost: false,
    canViewRevenue: false,
    canViewMargin: false,
  };

  // Determine initial perspective by primary persona
  const initialPerspective: PerspectiveKey = useMemo(() => {
    const p = bootstrap?.primaryPersona;
    if (p === "cost_accountant") return "ap";
    if (p === "revenue_accountant") return "ar";
    if (p === "field_ops") return "ops";
    if (p === "viewer") return "audit";
    return "executive";
  }, [bootstrap?.primaryPersona]);

  const [activePerspective, setActivePerspective] =
    useState<PerspectiveKey>(initialPerspective);

  // Filter allowed perspectives according to Segregation of Duties (SoD)
  const allowedPerspectives = useMemo(() => {
    return PERSPECTIVES.filter((p) => {
      if (p.key === "executive") return vis.canViewCost && vis.canViewRevenue;
      if (p.key === "ar") return vis.canViewRevenue;
      if (p.key === "ap") return vis.canViewCost;
      if (p.key === "ops") return true;
      if (p.key === "audit") return true;
      return true;
    });
  }, [vis.canViewCost, vis.canViewRevenue]);

  // Ensure active perspective remains valid if permissions refresh
  useEffect(() => {
    if (!allowedPerspectives.some((p) => p.key === activePerspective)) {
      setActivePerspective(allowedPerspectives[0]?.key ?? "ops");
    }
  }, [allowedPerspectives, activePerspective]);

  const loadData = useCallback(async () => {
    try {
      const [sum] = await Promise.all([
        apiRequest<DashboardSummaryDto>(
          "/api/dashboard/summary?includeBaseCurrencyRollUp=true"
        ),
        refreshBootstrap(),
      ]);
      setSummary(sum);
    } catch {
      // Offline fallback or network retry
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [refreshBootstrap]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  // Commercial financial calculations
  const rollUp = summary?.baseCurrencyRollUp;
  const baseCurrency =
    rollUp?.baseCurrency ?? bootstrap?.tenant.defaultCurrencyCode ?? "VND";
  const revenueTotal = rollUp?.revenueBestAvailableBase ?? 0;
  const costTotal = rollUp?.costBestAvailableBase ?? 0;
  const grossProfit = rollUp?.profitBestAvailableBase ?? revenueTotal - costTotal;

  const profitMarginPercent = useMemo(() => {
    if (revenueTotal <= 0) return 0;
    return Math.round((grossProfit / revenueTotal) * 1000) / 10;
  }, [revenueTotal, grossProfit]);

  // Accounting data confidence metrics
  const pipe = summary?.maturityPipeline;
  const actualCount = (pipe?.costActualCount ?? 0) + (pipe?.revenueActualCount ?? 0);
  const totalRecords =
    actualCount +
    (pipe?.costConfirmedOnlyCount ?? 0) +
    (pipe?.costExpectedOnlyCount ?? 0) +
    (pipe?.revenueConfirmedOnlyCount ?? 0) +
    (pipe?.revenueExpectedOnlyCount ?? 0);

  const confidencePercent = useMemo(() => {
    if (totalRecords <= 0) return 100;
    return Math.min(100, Math.round((actualCount / totalRecords) * 100));
  }, [actualCount, totalRecords]);

  // Role display label
  const roleLabel = useMemo(() => {
    const p = bootstrap?.primaryPersona;
    if (p === "executive_control") return "Ban Giám đốc & Điều hành Cấp cao";
    if (p === "cost_accountant") return "Kế toán Chi phí & Công nợ Phải trả";
    if (p === "revenue_accountant") return "Kế toán Doanh thu & Công nợ Phải thu";
    if (p === "field_ops") return "Chuyên viên Vận hành Hiện trường";
    if (p === "master_data") return "Quản trị Định mức & Danh mục";
    return "Kiểm soát & Kiểm toán Tài chính";
  }, [bootstrap?.primaryPersona]);

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
      {/* 1. Header: Enterprise Identity & User Profile */}
      <View style={styles.identityCard}>
        <View style={styles.rowBetween}>
          <View style={{ flex: 1 }}>
            <Text style={styles.tenantName}>
              {bootstrap?.tenant.name ?? "HỆ THỐNG QUẢN TRỊ TÀI CHÍNH DOANH NGHIỆP"}
            </Text>
            <Text style={styles.userProfile}>
              {bootstrap?.user.displayName} • {roleLabel}
            </Text>
          </View>
          <View style={styles.liveBadge}>
            <View style={styles.liveDot} />
            <Text style={styles.liveText}>Trực tuyến</Text>
          </View>
        </View>

        <View style={styles.tenantMetaRow}>
          <Text style={styles.tenantMetaText}>
            Mã đơn vị: <Text style={styles.metaBold}>{bootstrap?.tenant.code}</Text>
          </Text>
          <Text style={styles.tenantMetaDivider}>|</Text>
          <Text style={styles.tenantMetaText}>
            Đồng tiền hạch toán: <Text style={styles.metaBold}>{baseCurrency}</Text>
          </Text>
        </View>
      </View>

      {/* 2. Role-specific Perspective Selector (If user has multiple privileges) */}
      {allowedPerspectives.length > 1 && (
        <View style={styles.perspectiveContainer}>
          <Text style={styles.perspectiveSectionLabel}>GÓC NHÌN CHUYÊN MÔN</Text>
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={styles.perspectiveTabs}
          >
            {allowedPerspectives.map((opt) => {
              const active = opt.key === activePerspective;
              return (
                <TouchableOpacity
                  key={opt.key}
                  style={[styles.perspectiveTab, active && styles.perspectiveTabActive]}
                  onPress={() => setActivePerspective(opt.key)}
                >
                  <Text style={styles.perspectiveIcon}>{opt.icon}</Text>
                  <Text
                    style={[
                      styles.perspectiveText,
                      active && styles.perspectiveTextActive,
                    ]}
                  >
                    {opt.label}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </ScrollView>
        </View>
      )}

      {loading ? (
        <View style={styles.loadingWrap}>
          <ActivityIndicator size="large" color="#0F172A" />
          <Text style={styles.loadingText}>Đang tải dữ liệu tài chính thời gian thực...</Text>
        </View>
      ) : (
        <>
          {/* =========================================================================
              VIEW 1: BAN GIÁM ĐỐC / CHỦ DOANH NGHIỆP (EXECUTIVE VIEW)
             ========================================================================= */}
          {activePerspective === "executive" && (
            <>
              {/* Executive Financial Health Overview */}
              <View style={styles.executiveCard}>
                <View style={styles.rowBetween}>
                  <Text style={styles.cardHeaderTitle}>SỨC KHỎE TÀI CHÍNH TỔNG QUAN</Text>
                  <Text style={styles.currencyBadge}>{baseCurrency}</Text>
                </View>

                {/* Gross Profit & Margin Gauge */}
                <View style={styles.marginHighlightBox}>
                  <View style={styles.rowBetween}>
                    <View>
                      <Text style={styles.marginSubLabel}>LỢI NHUẬN GỘP KINH DOANH</Text>
                      <Text
                        style={[
                          styles.marginBigValue,
                          grossProfit < 0 ? { color: "#E11D48" } : { color: "#047857" },
                        ]}
                      >
                        {formatMoney(grossProfit, baseCurrency)}
                      </Text>
                    </View>
                    <View
                      style={[
                        styles.marginPill,
                        profitMarginPercent >= 20
                          ? styles.marginPillHealthy
                          : profitMarginPercent >= 10
                          ? styles.marginPillWarning
                          : styles.marginPillAlert,
                      ]}
                    >
                      <Text style={styles.marginPillText}>
                        Biên LN: {profitMarginPercent}%
                      </Text>
                    </View>
                  </View>

                  {/* Visual Margin Bar */}
                  <View style={styles.marginTrack}>
                    <View
                      style={[
                        styles.marginFill,
                        {
                          width: `${Math.max(
                            4,
                            Math.min(100, Math.max(0, profitMarginPercent))
                          )}%`,
                          backgroundColor:
                            profitMarginPercent >= 20
                              ? "#10B981"
                              : profitMarginPercent >= 10
                              ? "#F59E0B"
                              : "#EF4444",
                        },
                      ]}
                    />
                  </View>
                  <Text style={styles.marginNote}>
                    {profitMarginPercent >= 20
                      ? "✓ Đạt mục tiêu biên lợi nhuận kỳ vọng của doanh nghiệp"
                      : profitMarginPercent >= 10
                      ? "⚠️ Cần rà soát tối ưu chi phí vận chuyển & thầu phụ"
                      : "🚨 Cảnh báo biên lợi nhuận mỏng dưới mức an toàn"}
                  </Text>
                </View>

                {/* Revenue vs Cost Grid */}
                <View style={styles.metricGrid}>
                  <View style={styles.metricCol}>
                    <Text style={styles.metricColLabel}>Tổng Doanh thu</Text>
                    <Text style={[styles.metricColVal, { color: "#1D4ED8" }]}>
                      {formatMoney(revenueTotal, baseCurrency)}
                    </Text>
                  </View>
                  <View style={styles.metricColDivider} />
                  <View style={styles.metricCol}>
                    <Text style={styles.metricColLabel}>Tổng Chi phí Vận hành</Text>
                    <Text style={[styles.metricColVal, { color: "#B91C1C" }]}>
                      {formatMoney(costTotal, baseCurrency)}
                    </Text>
                  </View>
                </View>
              </View>

              {/* Data Maturity & Confidence Gauge */}
              <View style={styles.sectionCard}>
                <View style={styles.rowBetween}>
                  <Text style={styles.sectionTitle}>Chất lượng Dữ liệu & Tiến độ Sổ sách</Text>
                  <Text style={styles.confidenceScore}>Độ tin cậy: {confidencePercent}%</Text>
                </View>
                <View style={styles.confidenceTrack}>
                  <View
                    style={[styles.confidenceFill, { width: `${confidencePercent}%` }]}
                  />
                </View>
                <Text style={styles.confidenceDesc}>
                  Đã ghi nhận chứng từ thực tế đầy đủ cho{" "}
                  <Text style={{ fontWeight: "700", color: "#0F172A" }}>
                    {actualCount} khoản
                  </Text>{" "}
                  trong tổng số {totalRecords} khoản giao dịch phát sinh.
                </Text>
              </View>

              {/* Executive Priority Action Center */}
              <View style={styles.actionCenterCard}>
                <Text style={styles.actionCenterHeader}>TÁC VỤ ĐIỀU HÀNH CẤP THIẾT</Text>

                <TouchableOpacity
                  style={styles.actionRowItem}
                  onPress={() => router.push("/(tabs)/approvals")}
                >
                  <View style={styles.actionBadgeIconWarning}>
                    <Text style={styles.actionBadgeText}>✓</Text>
                  </View>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.actionTitle}>Phê duyệt Hạn mức & Khoản chi</Text>
                    <Text style={styles.actionSubtitle}>
                      {summary?.pendingApprovalCount ?? 0} khoản chi phí vượt hạn mức đang chờ ký duyệt
                    </Text>
                  </View>
                  <Text style={styles.actionChevron}>Duyệt ngay →</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.actionRowItem}
                  onPress={() => router.push("/(tabs)/control")}
                >
                  <View style={styles.actionBadgeIconDanger}>
                    <Text style={styles.actionBadgeText}>⚠️</Text>
                  </View>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.actionTitle}>Bất thường Vận hành & Tài chính</Text>
                    <Text style={styles.actionSubtitle}>
                      {summary?.openExceptionCount ?? 0} sự vụ phát sinh ngoài định mức cần xử lý
                    </Text>
                  </View>
                  <Text style={styles.actionChevron}>Kiểm tra →</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.actionRowItem}
                  onPress={() => router.push("/modules/closes")}
                >
                  <View style={styles.actionBadgeIconPrimary}>
                    <Text style={styles.actionBadgeText}>🔒</Text>
                  </View>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.actionTitle}>Khóa sổ & Đóng kỳ Kế toán</Text>
                    <Text style={styles.actionSubtitle}>
                      Kiểm tra tính sẵn sàng & niêm phong số liệu kỳ tài chính
                    </Text>
                  </View>
                  <Text style={styles.actionChevron}>Chi tiết →</Text>
                </TouchableOpacity>
              </View>
            </>
          )}

          {/* =========================================================================
              VIEW 2: ĐIỀU HÀNH VẬN HÀNH & HIỆN TRƯỜNG (FIELD OPS VIEW)
             ========================================================================= */}
          {activePerspective === "ops" && (
            <>
              {/* Ops Fleet Pulse */}
              <View style={styles.kpiGrid}>
                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/(tabs)/bills")}
                >
                  <Text style={styles.kpiEmoji}>📦</Text>
                  <Text style={styles.kpiValue}>{summary?.billCount ?? 0}</Text>
                  <Text style={styles.kpiLabel}>Vận đơn đang quản lý</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/(tabs)/bills")}
                >
                  <Text style={styles.kpiEmoji}>🚚</Text>
                  <Text style={styles.kpiValue}>Hoạt động</Text>
                  <Text style={styles.kpiLabel}>Chuyến xe & Tuyến vận chuyển</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/(tabs)/control")}
                >
                  <Text style={styles.kpiEmoji}>⚠️</Text>
                  <Text style={[styles.kpiValue, { color: "#DC2626" }]}>
                    {summary?.openExceptionCount ?? 0}
                  </Text>
                  <Text style={styles.kpiLabel}>Sự vụ chi phí hiện trường</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/(tabs)/offline")}
                >
                  <Text style={styles.kpiEmoji}>📡</Text>
                  <Text style={[styles.kpiValue, { color: "#2563EB" }]}>Tự động</Text>
                  <Text style={styles.kpiLabel}>Đồng bộ khi có sóng mạng</Text>
                </TouchableOpacity>
              </View>

              {/* Ops Quick Actions Bar */}
              <View style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>Tác vụ Vận hành Nhanh</Text>
                <View style={styles.quickOpsGrid}>
                  <TouchableOpacity
                    style={styles.quickOpsBtn}
                    onPress={() => router.push("/(tabs)/scanner")}
                  >
                    <Text style={styles.quickOpsIcon}>📷</Text>
                    <Text style={styles.quickOpsLabel}>Quét HAWB/MAWB</Text>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={styles.quickOpsBtn}
                    onPress={() => router.push("/modules/rates")}
                  >
                    <Text style={styles.quickOpsIcon}>📋</Text>
                    <Text style={styles.quickOpsLabel}>Tra Biểu cước</Text>
                  </TouchableOpacity>

                  {vis.canViewCost && (
                    <TouchableOpacity
                      style={styles.quickOpsBtn}
                      onPress={() => router.push("/(tabs)/costs")}
                    >
                      <Text style={styles.quickOpsIcon}>💸</Text>
                      <Text style={styles.quickOpsLabel}>Khai báo Chi phí</Text>
                    </TouchableOpacity>
                  )}

                  <TouchableOpacity
                    style={styles.quickOpsBtn}
                    onPress={() => router.push("/(tabs)/offline")}
                  >
                    <Text style={styles.quickOpsIcon}>📡</Text>
                    <Text style={styles.quickOpsLabel}>Hàng đợi Ngoại tuyến</Text>
                  </TouchableOpacity>
                </View>
              </View>
            </>
          )}

          {/* =========================================================================
              VIEW 3: KẾ TOÁN DOANH THU & PHẢI THU (AR VIEW)
             ========================================================================= */}
          {activePerspective === "ar" && (
            <>
              {/* Revenue & Receivables Position */}
              <View style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>CÔNG NỢ PHẢI THU KHÁCH HÀNG (AR)</Text>
                <Text style={styles.metricHugeValue}>
                  {formatMoney(revenueTotal, baseCurrency)}
                </Text>
                <Text style={styles.sectionSubtitle}>
                  Bao gồm các hóa đơn bán ra đã xác nhận và khoản chờ thu tiền
                </Text>

                <View style={styles.arStatusRow}>
                  <View style={styles.arStatusBox}>
                    <Text style={styles.arStatusNum}>
                      {summary?.unmatchedBankFeedCount ?? 0}
                    </Text>
                    <Text style={styles.arStatusLabel}>Báo có ngân hàng chưa khớp</Text>
                  </View>
                  <View style={styles.arStatusBox}>
                    <Text style={styles.arStatusNum}>{summary?.billCount ?? 0}</Text>
                    <Text style={styles.arStatusLabel}>Vận đơn đang phát sinh doanh thu</Text>
                  </View>
                </View>
              </View>

              {/* AR Actions */}
              <View style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>Nghiệp vụ Thu tiền & Công nợ</Text>
                <View style={styles.actionList}>
                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/modules/settlements")}
                  >
                    <Text style={styles.actionListEmoji}>💳</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Lập Phiếu thu & Khớp Báo có</Text>
                      <Text style={styles.actionListDesc}>
                        Ghi nhận tiền khách trả và gạch nợ tự động vào hóa đơn
                      </Text>
                    </View>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/(tabs)/ar")}
                  >
                    <Text style={styles.actionListEmoji}>🏦</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Sổ cái Chi tiết Công nợ Khách hàng</Text>
                      <Text style={styles.actionListDesc}>
                        Tra cứu số dư, hạn mức tín dụng và lịch sử đối soát
                      </Text>
                    </View>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/modules/reports")}
                  >
                    <Text style={styles.actionListEmoji}>📈</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Báo cáo Phân tích Tuổi nợ AR</Text>
                      <Text style={styles.actionListDesc}>
                        Theo dõi công nợ theo thời gian (0-30, 31-60, 61-90, 90+ ngày)
                      </Text>
                    </View>
                  </TouchableOpacity>
                </View>
              </View>
            </>
          )}

          {/* =========================================================================
              VIEW 4: KẾ TOÁN CHI PHÍ & PHẢI TRẢ (AP VIEW)
             ========================================================================= */}
          {activePerspective === "ap" && (
            <>
              {/* Cost & Payables Position */}
              <View style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>NGHĨA VỤ PHẢI TRẢ NHÀ CUNG CẤP (AP)</Text>
                <Text style={[styles.metricHugeValue, { color: "#B91C1C" }]}>
                  {formatMoney(costTotal, baseCurrency)}
                </Text>
                <Text style={styles.sectionSubtitle}>
                  Chi phí mua vào, giá vốn vận chuyển và công nợ đối tác nhà xe
                </Text>

                <View style={styles.arStatusRow}>
                  <View style={styles.arStatusBox}>
                    <Text style={[styles.arStatusNum, { color: "#D97706" }]}>
                      {summary?.pendingApprovalCount ?? 0}
                    </Text>
                    <Text style={styles.arStatusLabel}>Khoản chi đang chờ ký duyệt</Text>
                  </View>
                  <View style={styles.arStatusBox}>
                    <Text style={styles.arStatusNum}>
                      {summary?.openVarianceCount ?? 0}
                    </Text>
                    <Text style={styles.arStatusLabel}>Chênh lệch & Phụ phí cần rà soát</Text>
                  </View>
                </View>
              </View>

              {/* AP Actions */}
              <View style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>Nghiệp vụ Chi phí & Thanh toán</Text>
                <View style={styles.actionList}>
                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/modules/settlements")}
                  >
                    <Text style={styles.actionListEmoji}>💸</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Lập Đề nghị Thanh toán & Phiếu chi</Text>
                      <Text style={styles.actionListDesc}>
                        Tạo ủy nhiệm chi thanh toán cho nhà xe và đối tác
                      </Text>
                    </View>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/(tabs)/costs")}
                  >
                    <Text style={styles.actionListEmoji}>🔀</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Phân bổ Chi phí Dùng chung</Text>
                      <Text style={styles.actionListDesc}>
                        Phân bổ chi phí cố định, chi phí chuyến theo trọng lượng CW
                      </Text>
                    </View>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/(tabs)/ap")}
                  >
                    <Text style={styles.actionListEmoji}>🧾</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Sổ cái Chi tiết Công nợ Nhà cung cấp</Text>
                      <Text style={styles.actionListDesc}>
                        Kiểm soát hóa đơn đầu vào, chứng từ kế toán và đối chiếu số dư
                      </Text>
                    </View>
                  </TouchableOpacity>
                </View>
              </View>
            </>
          )}

          {/* =========================================================================
              VIEW 5: BAN KIỂM SOÁT & KIỂM TOÁN TÀI CHÍNH (AUDIT VIEW)
             ========================================================================= */}
          {activePerspective === "audit" && (
            <>
              {/* Compliance & Audit Metrics */}
              <View style={styles.kpiGrid}>
                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/(tabs)/control")}
                >
                  <Text style={styles.kpiEmoji}>🚨</Text>
                  <Text style={[styles.kpiValue, { color: "#DC2626" }]}>
                    {summary?.openExceptionCount ?? 0}
                  </Text>
                  <Text style={styles.kpiLabel}>Bất thường cần giải quyết</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/(tabs)/control")}
                >
                  <Text style={styles.kpiEmoji}>⚖️</Text>
                  <Text style={[styles.kpiValue, { color: "#2563EB" }]}>
                    {(summary?.openVarianceCount ?? 0) +
                      (summary?.openReconciliationCount ?? 0)}
                  </Text>
                  <Text style={styles.kpiLabel}>Chênh lệch & Cân đối số dư</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/modules/closes")}
                >
                  <Text style={styles.kpiEmoji}>🔒</Text>
                  <Text style={[styles.kpiValue, { color: "#059669" }]}>Chuẩn hóa</Text>
                  <Text style={styles.kpiLabel}>Tiêu chuẩn khóa sổ kế toán</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={styles.kpiCard}
                  onPress={() => router.push("/modules/settings")}
                >
                  <Text style={styles.kpiEmoji}>📜</Text>
                  <Text style={styles.kpiValue}>Toàn vẹn</Text>
                  <Text style={styles.kpiLabel}>Nhật ký kiểm toán bất biến</Text>
                </TouchableOpacity>
              </View>

              {/* Audit Control Operations */}
              <View style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>Công cụ Kiểm toán & Giám sát Tuân thủ</Text>
                <View style={styles.actionList}>
                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/(tabs)/control")}
                  >
                    <Text style={styles.actionListEmoji}>🛡️</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Trung tâm Xử lý Bất thường & Sai lệch</Text>
                      <Text style={styles.actionListDesc}>
                        Giải trình sai lệch định mức, tỷ giá ngoại tệ và điều chỉnh kế toán
                      </Text>
                    </View>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={styles.actionListItem}
                    onPress={() => router.push("/modules/settings")}
                  >
                    <Text style={styles.actionListEmoji}>📜</Text>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.actionListTitle}>Tra cứu Nhật ký Kiểm soát Bất biến</Text>
                      <Text style={styles.actionListDesc}>
                        Ghi nhận toàn bộ thao tác thêm, sửa, xóa, duyệt và dòng tiền
                      </Text>
                    </View>
                  </TouchableOpacity>
                </View>
              </View>
            </>
          )}

          {/* =========================================================================
              ORIGINAL CURRENCY PORTFOLIO (NEVER MIXED - VAS / IFRS COMPLIANT)
             ========================================================================= */}
          {summary?.totalsByCurrency && summary.totalsByCurrency.length > 0 && (
            <View style={styles.sectionCard}>
              <View style={styles.rowBetween}>
                <Text style={styles.sectionTitle}>Cơ cấu theo Nguyên tệ Giao dịch</Text>
                <Text style={styles.tagSubtitle}>Nguyên tắc không cộng gộp ngoại tệ</Text>
              </View>
              <Text style={styles.sectionNote}>
                Các khoản công nợ và chi phí được bảo lưu đúng loại tiền ban đầu
              </Text>

              {summary.totalsByCurrency.map((row) => (
                <View key={row.currencyCode} style={styles.currencyBlock}>
                  <View style={styles.currencyHeaderRow}>
                    <Text style={styles.currencyCodeText}>{row.currencyCode}</Text>
                    {vis.canViewMargin && (
                      <Text
                        style={[
                          styles.currencyProfitBadge,
                          (row.profitBestAvailable ?? 0) < 0
                            ? { color: "#DC2626" }
                            : { color: "#047857" },
                        ]}
                      >
                        Chênh lệch: {formatMoney(row.profitBestAvailable ?? 0, row.currencyCode)}
                      </Text>
                    )}
                  </View>

                  <View style={styles.currencyMetricsRow}>
                    {vis.canViewRevenue && (
                      <Text style={styles.currencyMetricItem}>
                        Doanh thu:{" "}
                        <Text style={{ fontWeight: "700", color: "#1D4ED8" }}>
                          {formatMoney(row.revenueBestAvailable, row.currencyCode)}
                        </Text>
                      </Text>
                    )}
                    {vis.canViewCost && (
                      <Text style={styles.currencyMetricItem}>
                        Chi phí:{" "}
                        <Text style={{ fontWeight: "700", color: "#B91C1C" }}>
                          {formatMoney(row.costBestAvailable, row.currencyCode)}
                        </Text>
                      </Text>
                    )}
                  </View>
                </View>
              ))}
            </View>
          )}

          {/* =========================================================================
              COMPLETE COMMERCIAL APPLICATION MODULES DIRECTORY (14 TILES)
             ========================================================================= */}
          <View style={styles.sectionCard}>
            <View style={styles.rowBetween}>
              <Text style={styles.sectionTitle}>Danh mục Phân hệ Quản trị</Text>
              <TouchableOpacity onPress={() => router.push("/(tabs)/modules")}>
                <Text style={styles.linkText}>Xem chi tiết →</Text>
              </TouchableOpacity>
            </View>
            <View style={styles.menuGrid}>
              {[
                { emoji: "📦", label: "Vận đơn & Chuyến hàng", route: "/(tabs)/bills" },
                { emoji: "📷", label: "Quét mã & Chụp ảnh", route: "/(tabs)/scanner" },
                { emoji: "📋", label: "Biểu cước & Báo giá", route: "/modules/rates" },
                ...(vis.canViewCost
                  ? [
                      { emoji: "💸", label: "Chi phí & Phân bổ", route: "/(tabs)/costs" },
                      { emoji: "🧾", label: "Công nợ Phải trả (AP)", route: "/(tabs)/ap" },
                    ]
                  : []),
                ...(vis.canViewRevenue
                  ? [
                      { emoji: "💰", label: "Doanh thu Khách hàng", route: "/(tabs)/revenues" },
                      { emoji: "🏦", label: "Công nợ Phải thu (AR)", route: "/(tabs)/ar" },
                    ]
                  : []),
                { emoji: "📑", label: "Hóa đơn & Chứng từ", route: "/(tabs)/documents" },
                { emoji: "💳", label: "Thu chi & Dòng tiền", route: "/modules/settlements" },
                { emoji: "✅", label: "Phê duyệt Điện tử", route: "/(tabs)/approvals" },
                { emoji: "🛡️", label: "Kiểm soát & Cân đối", route: "/(tabs)/control" },
                { emoji: "🔒", label: "Khóa sổ Kế toán", route: "/modules/closes" },
                { emoji: "📈", label: "Báo cáo Tài chính", route: "/modules/reports" },
                { emoji: "🏢", label: "Đối tác & Tỷ giá", route: "/modules/master" },
                { emoji: "⚙️", label: "Thiết lập & Nhật ký", route: "/modules/settings" },
                { emoji: "📡", label: "Lưu trữ Ngoại tuyến", route: "/(tabs)/offline" },
              ].map((item) => (
                <TouchableOpacity
                  key={item.route + item.label}
                  style={styles.menuTile}
                  onPress={() => router.push(item.route as never)}
                >
                  <Text style={styles.menuTileEmoji}>{item.emoji}</Text>
                  <Text style={styles.menuTileLabel} numberOfLines={2}>
                    {item.label}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>

          <View style={{ height: 28 }} />
        </>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F1F5F9",
    padding: 14,
  },
  loadingWrap: {
    paddingVertical: 48,
    alignItems: "center",
    justifyContent: "center",
  },
  loadingText: {
    marginTop: 12,
    fontSize: 13,
    color: "#64748B",
    fontWeight: "500",
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },

  // 1. Identity Card
  identityCard: {
    backgroundColor: "#0F172A",
    borderRadius: 16,
    padding: 16,
    marginBottom: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  tenantName: {
    color: "#F8FAFC",
    fontSize: 16,
    fontWeight: "800",
    letterSpacing: 0.3,
  },
  userProfile: {
    color: "#94A3B8",
    fontSize: 13,
    marginTop: 4,
    fontWeight: "500",
  },
  liveBadge: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "rgba(16, 185, 129, 0.15)",
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "rgba(16, 185, 129, 0.3)",
  },
  liveDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: "#10B981",
    marginRight: 5,
  },
  liveText: {
    color: "#10B981",
    fontSize: 11,
    fontWeight: "700",
  },
  tenantMetaRow: {
    flexDirection: "row",
    alignItems: "center",
    marginTop: 12,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: "#1E293B",
  },
  tenantMetaText: {
    color: "#CBD5E1",
    fontSize: 11,
  },
  tenantMetaDivider: {
    color: "#475569",
    marginHorizontal: 8,
    fontSize: 11,
  },
  metaBold: {
    fontWeight: "700",
    color: "#FFFFFF",
  },

  // 2. Perspective Selector Tabs
  perspectiveContainer: {
    marginBottom: 12,
  },
  perspectiveSectionLabel: {
    fontSize: 10,
    fontWeight: "800",
    color: "#64748B",
    letterSpacing: 0.8,
    marginBottom: 6,
    marginLeft: 2,
  },
  perspectiveTabs: {
    flexDirection: "row",
    gap: 8,
  },
  perspectiveTab: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#FFFFFF",
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  perspectiveTabActive: {
    backgroundColor: "#0F172A",
    borderColor: "#0F172A",
  },
  perspectiveIcon: {
    fontSize: 14,
    marginRight: 6,
  },
  perspectiveText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#475569",
  },
  perspectiveTextActive: {
    color: "#FFFFFF",
  },

  // 3. Executive Dashboard Card
  executiveCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 16,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  cardHeaderTitle: {
    fontSize: 11,
    fontWeight: "800",
    color: "#64748B",
    letterSpacing: 0.6,
  },
  currencyBadge: {
    fontSize: 11,
    fontWeight: "700",
    color: "#0F172A",
    backgroundColor: "#F1F5F9",
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 6,
  },
  marginHighlightBox: {
    backgroundColor: "#F8FAFC",
    borderRadius: 12,
    padding: 14,
    marginTop: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  marginSubLabel: {
    fontSize: 11,
    fontWeight: "700",
    color: "#64748B",
  },
  marginBigValue: {
    fontSize: 22,
    fontWeight: "900",
    marginTop: 2,
  },
  marginPill: {
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 8,
  },
  marginPillHealthy: {
    backgroundColor: "#D1FAE5",
  },
  marginPillWarning: {
    backgroundColor: "#FEF3C7",
  },
  marginPillAlert: {
    backgroundColor: "#FEE2E2",
  },
  marginPillText: {
    fontSize: 12,
    fontWeight: "800",
    color: "#0F172A",
  },
  marginTrack: {
    height: 8,
    backgroundColor: "#E2E8F0",
    borderRadius: 4,
    marginTop: 12,
    overflow: "hidden",
  },
  marginFill: {
    height: "100%",
    borderRadius: 4,
  },
  marginNote: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 8,
    fontWeight: "500",
  },
  metricGrid: {
    flexDirection: "row",
    marginTop: 14,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  metricCol: {
    flex: 1,
  },
  metricColLabel: {
    fontSize: 11,
    color: "#64748B",
    fontWeight: "600",
  },
  metricColVal: {
    fontSize: 16,
    fontWeight: "800",
    marginTop: 2,
  },
  metricColDivider: {
    width: 1,
    backgroundColor: "#E2E8F0",
    marginHorizontal: 12,
  },

  // 4. Section Card & Confidence Gauge
  sectionCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 14,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
  },
  sectionSubtitle: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 4,
  },
  sectionNote: {
    fontSize: 11,
    color: "#64748B",
    marginBottom: 8,
    marginTop: 2,
  },
  tagSubtitle: {
    fontSize: 10,
    fontWeight: "600",
    color: "#64748B",
  },
  confidenceScore: {
    fontSize: 11,
    fontWeight: "700",
    color: "#047857",
  },
  confidenceTrack: {
    height: 6,
    backgroundColor: "#E2E8F0",
    borderRadius: 3,
    marginTop: 8,
    overflow: "hidden",
  },
  confidenceFill: {
    height: "100%",
    backgroundColor: "#10B981",
    borderRadius: 3,
  },
  confidenceDesc: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 8,
    lineHeight: 16,
  },

  // 5. Action Center
  actionCenterCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 14,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  actionCenterHeader: {
    fontSize: 11,
    fontWeight: "800",
    color: "#64748B",
    letterSpacing: 0.6,
    marginBottom: 10,
  },
  actionRowItem: {
    flexDirection: "row",
    alignItems: "center",
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: "#F1F5F9",
  },
  actionBadgeIconWarning: {
    width: 32,
    height: 32,
    borderRadius: 8,
    backgroundColor: "#FEF3C7",
    alignItems: "center",
    justifyContent: "center",
    marginRight: 10,
  },
  actionBadgeIconDanger: {
    width: 32,
    height: 32,
    borderRadius: 8,
    backgroundColor: "#FEE2E2",
    alignItems: "center",
    justifyContent: "center",
    marginRight: 10,
  },
  actionBadgeIconPrimary: {
    width: 32,
    height: 32,
    borderRadius: 8,
    backgroundColor: "#EFF6FF",
    alignItems: "center",
    justifyContent: "center",
    marginRight: 10,
  },
  actionBadgeText: {
    fontSize: 14,
  },
  actionTitle: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  actionSubtitle: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 2,
  },
  actionChevron: {
    fontSize: 11,
    fontWeight: "700",
    color: "#2563EB",
    marginLeft: 6,
  },

  // 6. Generic KPI Grid
  kpiGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    justifyContent: "space-between",
    marginBottom: 4,
  },
  kpiCard: {
    width: "48.5%",
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 12,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  kpiEmoji: {
    fontSize: 16,
    marginBottom: 4,
  },
  kpiValue: {
    fontSize: 18,
    fontWeight: "800",
    color: "#0F172A",
  },
  kpiLabel: {
    fontSize: 11,
    color: "#64748B",
    fontWeight: "600",
    marginTop: 2,
  },

  // 7. Ops Quick Action Buttons
  quickOpsGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    justifyContent: "space-between",
    marginTop: 10,
  },
  quickOpsBtn: {
    width: "48.5%",
    backgroundColor: "#F8FAFC",
    borderRadius: 10,
    padding: 12,
    marginBottom: 8,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  quickOpsIcon: {
    fontSize: 20,
    marginBottom: 4,
  },
  quickOpsLabel: {
    fontSize: 12,
    fontWeight: "700",
    color: "#0F172A",
  },

  // 8. AR & AP Position Box
  metricHugeValue: {
    fontSize: 26,
    fontWeight: "900",
    color: "#1D4ED8",
    marginTop: 4,
  },
  arStatusRow: {
    flexDirection: "row",
    marginTop: 12,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
    gap: 12,
  },
  arStatusBox: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  arStatusNum: {
    fontSize: 18,
    fontWeight: "800",
    color: "#0F172A",
  },
  arStatusLabel: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 2,
  },

  // 9. Action List
  actionList: {
    marginTop: 8,
  },
  actionListItem: {
    flexDirection: "row",
    alignItems: "center",
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: "#F1F5F9",
  },
  actionListEmoji: {
    fontSize: 18,
    marginRight: 10,
  },
  actionListTitle: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  actionListDesc: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 2,
  },

  // 10. Currency Portfolio
  currencyBlock: {
    backgroundColor: "#F8FAFC",
    padding: 10,
    borderRadius: 8,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  currencyHeaderRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 4,
  },
  currencyCodeText: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
  },
  currencyProfitBadge: {
    fontSize: 12,
    fontWeight: "800",
  },
  currencyMetricsRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 14,
  },
  currencyMetricItem: {
    fontSize: 11,
    color: "#475569",
  },

  // 11. 14-Tile Enterprise Directory
  menuGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    justifyContent: "space-between",
    marginTop: 10,
  },
  menuTile: {
    width: "31.5%",
    backgroundColor: "#F8FAFC",
    borderRadius: 10,
    paddingVertical: 10,
    paddingHorizontal: 6,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    alignItems: "center",
  },
  menuTileEmoji: {
    fontSize: 18,
    marginBottom: 4,
  },
  menuTileLabel: {
    fontSize: 11,
    fontWeight: "700",
    color: "#0F172A",
    textAlign: "center",
  },
  linkText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#2563EB",
  },
});
