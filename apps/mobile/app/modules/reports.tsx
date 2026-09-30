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
import { formatMoney } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";

interface AgingBucketRowDto {
  currencyCode?: string;
  current?: number;
  bucket1To30?: number;
  bucket31To60?: number;
  bucket61To90?: number;
  over90?: number;
  total?: number;
  // alternate property names from backend
  days1To30?: number;
  days31To60?: number;
  days61To90?: number;
  daysOver90?: number;
}

interface AgingSideResponseDto {
  currencyCode?: string;
  byCurrency?: AgingBucketRowDto[];
  items?: Array<{
    id?: string;
    documentNo?: string | null;
    partyName?: string | null;
    currencyCode: string;
    remainingBalance: number;
    daysPastDue?: number;
    bucket?: string;
  }>;
  current?: number;
  bucket1To30?: number;
  bucket31To60?: number;
  bucket61To90?: number;
  over90?: number;
  total?: number;
}

interface BillProfitRowDto {
  billId: string;
  billNo: string;
  customerName?: string | null;
  currency?: string;
  currencyCode?: string;
  revenue?: number | null;
  totalRevenue?: number | null;
  cost?: number | null;
  totalCost?: number | null;
  grossProfit?: number | null;
  profit?: number | null;
  marginPercent?: number | null;
}

interface ProfitabilityGroupRowDto {
  groupKey: string;
  groupName: string;
  currency?: string;
  totalRevenue?: number | null;
  totalCost?: number | null;
  profit?: number | null;
  marginPercent?: number | null;
}

interface CashSettlementReportDto {
  currencyCode?: string;
  totalPayments?: number;
  totalCollections?: number;
  unallocatedCash?: number;
  netCashFlow?: number;
  byCurrency?: Array<{
    currencyCode: string;
    totalPayments?: number;
    totalCollections?: number;
    unallocatedCash?: number;
    netCashFlow?: number;
  }>;
}

export default function ReportsModuleScreen() {
  const router = useRouter();
  const { bootstrap } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;
  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? false;
  const canViewMargin = bootstrap?.financialVisibility.canViewMargin ?? false;

  const [subTab, setSubTab] = useState<"aging" | "profitability" | "cash">(
    "aging"
  );
  const [viewMode, setViewMode] = useState<"best" | "actual" | "expected">(
    "best"
  );
  const [groupBy, setGroupBy] = useState<"bill" | "customer" | "route">("bill");
  const [currencyFilter, setCurrencyFilter] = useState<"ALL" | "VND" | "USD">(
    "ALL"
  );

  const [loading, setLoading] = useState<boolean>(true);
  const [apAging, setApAging] = useState<AgingSideResponseDto | null>(null);
  const [arAging, setArAging] = useState<AgingSideResponseDto | null>(null);
  const [billProfits, setBillProfits] = useState<BillProfitRowDto[]>([]);
  const [groupProfits, setGroupProfits] = useState<ProfitabilityGroupRowDto[]>(
    []
  );
  const [cashReport, setCashReport] = useState<CashSettlementReportDto | null>(
    null
  );

  const loadReports = useCallback(async () => {
    setLoading(true);
    try {
      const curParam =
        currencyFilter === "ALL" ? "" : `?currencyCode=${currencyFilter}`;

      const [apRes, arRes, billProfRes, grpProfRes, cashRes] =
        await Promise.all([
          canViewCost
            ? apiRequest<AgingSideResponseDto>(
                `/api/accounts-payable/aging${curParam}`
              ).catch(() => null)
            : Promise.resolve(null),
          canViewRevenue
            ? apiRequest<AgingSideResponseDto>(
                `/api/accounts-receivable/aging${curParam}`
              ).catch(() => null)
            : Promise.resolve(null),
          apiRequest<
            BillProfitRowDto[] | { items: BillProfitRowDto[] }
          >(`/api/profitability/bills?view=${viewMode}`).catch(
            () => [] as BillProfitRowDto[]
          ),
          groupBy !== "bill"
            ? apiRequest<
                ProfitabilityGroupRowDto[] | { items: ProfitabilityGroupRowDto[] }
              >(
                `/api/profitability/groups?groupBy=${groupBy}&view=${viewMode}`
              ).catch(() => [] as ProfitabilityGroupRowDto[])
            : Promise.resolve([] as ProfitabilityGroupRowDto[]),
          apiRequest<CashSettlementReportDto>(
            "/api/reports/cash-settlement"
          ).catch(() => null),
        ]);

      setApAging(apRes);
      setArAging(arRes);
      setBillProfits(
        Array.isArray(billProfRes) ? billProfRes : billProfRes?.items ?? []
      );
      setGroupProfits(
        Array.isArray(grpProfRes) ? grpProfRes : grpProfRes?.items ?? []
      );
      setCashReport(cashRes);
    } finally {
      setLoading(false);
    }
  }, [canViewCost, canViewRevenue, currencyFilter, viewMode, groupBy]);

  useEffect(() => {
    void loadReports();
  }, [loadReports]);

  const renderAgingCard = (
    title: string,
    data: AgingSideResponseDto | null,
    toneColor: string
  ) => {
    const cur =
      currencyFilter === "ALL"
        ? data?.currencyCode ?? "VND"
        : currencyFilter;
    const rows: AgingBucketRowDto[] =
      data?.byCurrency && data.byCurrency.length > 0
        ? data.byCurrency
        : [
            {
              currencyCode: cur,
              current: data?.current ?? 0,
              bucket1To30: data?.bucket1To30 ?? 0,
              bucket31To60: data?.bucket31To60 ?? 0,
              bucket61To90: data?.bucket61To90 ?? 0,
              over90: data?.over90 ?? 0,
              total: data?.total ?? 0,
            },
          ];

    return (
      <View style={styles.card}>
        <Text style={[styles.cardTitle, { color: toneColor }]}>{title}</Text>
        <Text style={styles.metaText}>
          Phân tổ theo từng Nguyên tệ gốc (Không trộn lẫn VND &amp; USD)
        </Text>

        {rows.map((r, idx) => {
          const cCode = r.currencyCode ?? cur;
          const b0 = r.current ?? 0;
          const b30 = r.bucket1To30 ?? r.days1To30 ?? 0;
          const b60 = r.bucket31To60 ?? r.days31To60 ?? 0;
          const b90 = r.bucket61To90 ?? r.days61To90 ?? 0;
          const bOver = r.over90 ?? r.daysOver90 ?? 0;
          const total = r.total ?? b0 + b30 + b60 + b90 + bOver;

          return (
            <View key={idx} style={styles.currencyBlock}>
              <View style={styles.rowBetween}>
                <Text style={styles.currencyHeader}>Nguyên tệ: {cCode}</Text>
                <Text style={styles.totalValue}>
                  Tổng: {formatMoney(total, cCode)}
                </Text>
              </View>

              <View style={styles.bucketGrid}>
                <View style={styles.bucketCell}>
                  <Text style={styles.bucketLabel}>Trong hạn</Text>
                  <Text style={styles.bucketVal}>{formatMoney(b0, cCode)}</Text>
                </View>
                <View style={styles.bucketCell}>
                  <Text style={styles.bucketLabel}>1–30 ngày</Text>
                  <Text style={styles.bucketVal}>
                    {formatMoney(b30, cCode)}
                  </Text>
                </View>
                <View style={styles.bucketCell}>
                  <Text style={styles.bucketLabel}>31–60 ngày</Text>
                  <Text style={styles.bucketVal}>
                    {formatMoney(b60, cCode)}
                  </Text>
                </View>
                <View style={styles.bucketCell}>
                  <Text style={styles.bucketLabel}>61–90 ngày</Text>
                  <Text style={[styles.bucketVal, { color: "#D97706" }]}>
                    {formatMoney(b90, cCode)}
                  </Text>
                </View>
                <View style={styles.bucketCell}>
                  <Text style={styles.bucketLabel}>&gt; 90 ngày</Text>
                  <Text style={[styles.bucketVal, { color: "#DC2626" }]}>
                    {formatMoney(bOver, cCode)}
                  </Text>
                </View>
              </View>
            </View>
          );
        })}

        {data?.items && data.items.length > 0 ? (
          <View style={{ marginTop: 10 }}>
            <Text style={styles.subHeader}>
              Chi tiết khoản công nợ quá hạn ({data.items.length}):
            </Text>
            {data.items.slice(0, 10).map((it, idx) => (
              <View key={it.id ?? idx} style={styles.itemRow}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.itemCode}>
                    {it.documentNo ?? "Công nợ"} • {it.partyName ?? "Đối tác"}
                  </Text>
                  <Text style={styles.metaText}>
                    Quá hạn: {it.daysPastDue ?? 0} ngày ({it.bucket ?? "current"})
                  </Text>
                </View>
                <Text style={styles.itemAmt}>
                  {formatMoney(it.remainingBalance, it.currencyCode)}
                </Text>
              </View>
            ))}
          </View>
        ) : null}
      </View>
    );
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={loading}
          onRefresh={() => void loadReports()}
        />
      }
    >
      <View style={styles.tabRow}>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "aging" && styles.tabBtnActive]}
          onPress={() => setSubTab("aging")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "aging" && styles.tabBtnTextActive,
            ]}
          >
            ⏳ Tuổi nợ AP/AR
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.tabBtn,
            subTab === "profitability" && styles.tabBtnActive,
          ]}
          onPress={() => setSubTab("profitability")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "profitability" && styles.tabBtnTextActive,
            ]}
          >
            📈 Lợi nhuận Bill
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "cash" && styles.tabBtnActive]}
          onPress={() => setSubTab("cash")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "cash" && styles.tabBtnTextActive,
            ]}
          >
            💵 Dòng tiền
          </Text>
        </TouchableOpacity>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : subTab === "aging" ? (
        <>
          <View style={styles.chipRow}>
            {(["ALL", "VND", "USD"] as const).map((c) => (
              <TouchableOpacity
                key={c}
                style={[
                  styles.chip,
                  currencyFilter === c && styles.chipActive,
                ]}
                onPress={() => setCurrencyFilter(c)}
              >
                <Text
                  style={[
                    styles.chipText,
                    currencyFilter === c && styles.chipTextActive,
                  ]}
                >
                  {c === "ALL" ? "Mọi nguyên tệ" : `Nguyên tệ ${c}`}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {canViewCost
            ? renderAgingCard(
                "Tuổi nợ Phải trả Nhà cung cấp (AP Aging)",
                apAging,
                "#B91C1C"
              )
            : null}

          {canViewRevenue
            ? renderAgingCard(
                "Tuổi nợ Phải thu Khách hàng (AR Aging)",
                arAging,
                "#1D4ED8"
              )
            : null}
        </>
      ) : subTab === "profitability" ? (
        <>
          <View style={styles.filterBlock}>
            <Text style={styles.label}>Độ chín số liệu (Maturity View):</Text>
            <View style={styles.chipRow}>
              {(
                [
                  { k: "best", l: "Best Available (Tốt nhất)" },
                  { k: "actual", l: "Actual (Thực tế)" },
                  { k: "expected", l: "Expected (Dự kiến)" },
                ] as const
              ).map((v) => (
                <TouchableOpacity
                  key={v.k}
                  style={[styles.chip, viewMode === v.k && styles.chipActive]}
                  onPress={() => setViewMode(v.k)}
                >
                  <Text
                    style={[
                      styles.chipText,
                      viewMode === v.k && styles.chipTextActive,
                    ]}
                  >
                    {v.l}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>

            <Text style={styles.label}>Chiều phân tích:</Text>
            <View style={styles.chipRow}>
              {(
                [
                  { k: "bill", l: "Theo từng Vận đơn (Bill)" },
                  { k: "customer", l: "Theo Khách hàng" },
                  { k: "route", l: "Theo Tuyến đường" },
                ] as const
              ).map((g) => (
                <TouchableOpacity
                  key={g.k}
                  style={[styles.chip, groupBy === g.k && styles.chipActive]}
                  onPress={() => setGroupBy(g.k)}
                >
                  <Text
                    style={[
                      styles.chipText,
                      groupBy === g.k && styles.chipTextActive,
                    ]}
                  >
                    {g.l}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>

          {groupBy === "bill" ? (
            billProfits.length === 0 ? (
              <View style={styles.emptyCard}>
                <Text style={styles.emptyTitle}>
                  Chưa có dữ liệu báo cáo theo Vận đơn
                </Text>
              </View>
            ) : (
              billProfits.map((row) => {
                const cur = row.currency ?? row.currencyCode ?? "VND";
                const rev = row.revenue ?? row.totalRevenue ?? 0;
                const cost = row.cost ?? row.totalCost ?? 0;
                const prof = row.grossProfit ?? row.profit ?? rev - cost;

                return (
                  <TouchableOpacity
                    key={row.billId}
                    style={styles.card}
                    onPress={() => router.push(`/bills/${row.billId}`)}
                  >
                    <View style={styles.rowBetween}>
                      <Text style={styles.cardTitle}>{row.billNo}</Text>
                      <Text style={styles.metaText}>
                        {row.customerName ?? "Khách hàng"}
                      </Text>
                    </View>

                    <View style={styles.bucketGrid}>
                      {canViewCost ? (
                        <View style={styles.bucketCell}>
                          <Text style={styles.bucketLabel}>Chi phí</Text>
                          <Text style={styles.bucketVal}>
                            {formatMoney(cost, cur)}
                          </Text>
                        </View>
                      ) : null}
                      {canViewRevenue ? (
                        <View style={styles.bucketCell}>
                          <Text style={styles.bucketLabel}>Doanh thu</Text>
                          <Text style={styles.bucketVal}>
                            {formatMoney(rev, cur)}
                          </Text>
                        </View>
                      ) : null}
                      {canViewMargin ? (
                        <View style={styles.bucketCell}>
                          <Text style={styles.bucketLabel}>Lãi gộp (Biên)</Text>
                          <Text
                            style={[
                              styles.bucketVal,
                              { color: prof >= 0 ? "#0F766E" : "#DC2626" },
                            ]}
                          >
                            {formatMoney(prof, cur)} (
                            {(row.marginPercent ?? 0).toFixed(1)}%)
                          </Text>
                        </View>
                      ) : null}
                    </View>
                  </TouchableOpacity>
                );
              })
            )
          ) : groupProfits.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>
                Chưa có dữ liệu nhóm lợi nhuận
              </Text>
            </View>
          ) : (
            groupProfits.map((grp, idx) => {
              const cur = grp.currency ?? "VND";
              return (
                <View key={grp.groupKey || idx} style={styles.card}>
                  <Text style={styles.cardTitle}>
                    {grp.groupName || grp.groupKey}
                  </Text>
                  <View style={styles.bucketGrid}>
                    {canViewCost ? (
                      <View style={styles.bucketCell}>
                        <Text style={styles.bucketLabel}>Tổng Chi phí</Text>
                        <Text style={styles.bucketVal}>
                          {formatMoney(grp.totalCost ?? 0, cur)}
                        </Text>
                      </View>
                    ) : null}
                    {canViewRevenue ? (
                      <View style={styles.bucketCell}>
                        <Text style={styles.bucketLabel}>Tổng Doanh thu</Text>
                        <Text style={styles.bucketVal}>
                          {formatMoney(grp.totalRevenue ?? 0, cur)}
                        </Text>
                      </View>
                    ) : null}
                    {canViewMargin ? (
                      <View style={styles.bucketCell}>
                        <Text style={styles.bucketLabel}>Lợi nhuận gộp</Text>
                        <Text style={[styles.bucketVal, { color: "#0F766E" }]}>
                          {formatMoney(grp.profit ?? 0, cur)} (
                          {(grp.marginPercent ?? 0).toFixed(1)}%)
                        </Text>
                      </View>
                    ) : null}
                  </View>
                </View>
              );
            })
          )}
        </>
      ) : (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>
            Tổng hợp Dòng tiền Thanh toán &amp; Thu tiền (Cash Settlement)
          </Text>
          <View style={styles.bucketGrid}>
            {canViewCost ? (
              <View style={styles.bucketCell}>
                <Text style={styles.bucketLabel}>Tổng Đã Chi (Payments)</Text>
                <Text style={styles.bucketVal}>
                  {formatMoney(
                    cashReport?.totalPayments ?? 0,
                    cashReport?.currencyCode ?? "VND"
                  )}
                </Text>
              </View>
            ) : null}
            {canViewRevenue ? (
              <View style={styles.bucketCell}>
                <Text style={styles.bucketLabel}>
                  Tổng Đã Thu (Collections)
                </Text>
                <Text style={styles.bucketVal}>
                  {formatMoney(
                    cashReport?.totalCollections ?? 0,
                    cashReport?.currencyCode ?? "VND"
                  )}
                </Text>
              </View>
            ) : null}
            <View style={styles.bucketCell}>
              <Text style={styles.bucketLabel}>Tiền chưa phân bổ</Text>
              <Text style={styles.bucketVal}>
                {formatMoney(
                  cashReport?.unallocatedCash ?? 0,
                  cashReport?.currencyCode ?? "VND"
                )}
              </Text>
            </View>
            {canViewMargin ? (
              <View style={styles.bucketCell}>
                <Text style={styles.bucketLabel}>Dòng tiền thuần (Net Cash)</Text>
                <Text style={[styles.bucketVal, { color: "#0F766E" }]}>
                  {formatMoney(
                    cashReport?.netCashFlow ?? 0,
                    cashReport?.currencyCode ?? "VND"
                  )}
                </Text>
              </View>
            ) : null}
          </View>
        </View>
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
  tabRow: {
    flexDirection: "row",
    gap: 6,
    marginBottom: 12,
  },
  tabBtn: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: 10,
    backgroundColor: "#E2E8F0",
    alignItems: "center",
  },
  tabBtnActive: {
    backgroundColor: "#0F172A",
  },
  tabBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
  },
  tabBtnTextActive: {
    color: "#FFFFFF",
  },
  filterBlock: {
    backgroundColor: "#FFFFFF",
    padding: 12,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 12,
  },
  label: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 4,
    marginTop: 4,
  },
  chipRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginBottom: 8,
  },
  chip: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 6,
    backgroundColor: "#FFFFFF",
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
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  cardTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
  },
  subHeader: {
    fontSize: 13,
    fontWeight: "700",
    color: "#1E293B",
    marginBottom: 6,
  },
  metaText: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 2,
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  currencyBlock: {
    backgroundColor: "#F8FAFC",
    borderRadius: 10,
    padding: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginTop: 10,
  },
  currencyHeader: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
  },
  totalValue: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F766E",
  },
  bucketGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10,
    marginTop: 8,
  },
  bucketCell: {
    minWidth: 95,
    backgroundColor: "#FFFFFF",
    padding: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  bucketLabel: {
    fontSize: 11,
    color: "#64748B",
  },
  bucketVal: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
    marginTop: 2,
  },
  itemRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingVertical: 7,
    borderBottomWidth: 1,
    borderBottomColor: "#F1F5F9",
  },
  itemCode: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  itemAmt: {
    fontSize: 13,
    fontWeight: "700",
    color: "#DC2626",
  },
  emptyCard: {
    backgroundColor: "#FFFFFF",
    padding: 20,
    borderRadius: 12,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  emptyTitle: {
    fontSize: 14,
    fontWeight: "600",
    color: "#475569",
  },
});
