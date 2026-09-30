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
  ApArItemDto,
  createIdempotencyKey,
  formatMoney,
} from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { BalanceImpactCard } from "../../src/components/BalanceImpactCard";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";

interface CashSettlementDto {
  id: string;
  paymentNo?: string | null;
  collectionNo?: string | null;
  amount: number;
  currencyCode: string;
  valueDate?: string | null;
  counterpartyId?: string | null;
  counterpartyName?: string | null;
  status: string;
  allocatedAmount?: number;
  unallocatedAmount?: number;
  referenceNo?: string | null;
  notes?: string | null;
  rowVersion?: string | null;
}

interface AllocationItemDto {
  id: string;
  accountsPayableId?: string | null;
  accountsReceivableId?: string | null;
  documentNo?: string | null;
  amount: number;
  status: string;
  notes?: string | null;
  rowVersion?: string | null;
}

interface CashSettlementDetailDto extends CashSettlementDto {
  allocations?: AllocationItemDto[];
}

interface BankFeedLineDto {
  id: string;
  valueDate: string;
  amount: number;
  currencyCode: string;
  direction?: string | null;
  bankReference?: string | null;
  counterpartyName?: string | null;
  description?: string | null;
  status: string;
  ignoreReason?: string | null;
}

export default function SettlementsModuleScreen() {
  const { bootstrap } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;
  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? false;

  const defaultTab: "payments" | "collections" | "bank" = canViewCost
    ? "payments"
    : canViewRevenue
    ? "collections"
    : "bank";

  const [subTab, setSubTab] = useState<"payments" | "collections" | "bank">(defaultTab);
  const [loading, setLoading] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState<boolean>(false);

  const [payments, setPayments] = useState<CashSettlementDto[]>([]);
  const [collections, setCollections] = useState<CashSettlementDto[]>([]);
  const [bankLines, setBankLines] = useState<BankFeedLineDto[]>([]);
  const [openApList, setOpenApList] = useState<ApArItemDto[]>([]);
  const [openArList, setOpenArList] = useState<ApArItemDto[]>([]);

  // Create Payment / Collection form
  const [showCreateCash, setShowCreateCash] = useState<boolean>(false);
  const [cashAmount, setCashAmount] = useState<string>("");
  const [cashCurrency, setCashCurrency] = useState<"VND" | "USD">("VND");
  const [cashRefNo, setCashRefNo] = useState<string>("");
  const [cashBillRef, setCashBillRef] = useState<string>("");
  const [cashNotes, setCashNotes] = useState<string>("");

  // Selected Payment / Collection for Allocation or Cancel
  const [selectedCash, setSelectedCash] = useState<CashSettlementDetailDto | null>(null);
  const [selectedTargetApAr, setSelectedTargetApAr] = useState<ApArItemDto | null>(null);
  const [allocateAmount, setAllocateAmount] = useState<string>("");
  const [actionReason, setActionReason] = useState<string>("");

  // Create Bank Feed line
  const [showCreateBank, setShowCreateBank] = useState<boolean>(false);
  const [bankAmount, setBankAmount] = useState<string>("");
  const [bankDirection, setBankDirection] = useState<"credit" | "debit">("credit");
  const [bankRef, setBankRef] = useState<string>("");
  const [bankCounterparty, setBankCounterparty] = useState<string>("");
  const [bankDesc, setBankDesc] = useState<string>("");
  const [ignoreLineId, setIgnoreLineId] = useState<string | null>(null);
  const [ignoreReason, setIgnoreReason] = useState<string>("");

  const loadAll = useCallback(async () => {
    setLoading(true);
    try {
      const [payRes, colRes, bankRes, apRes, arRes] = await Promise.all([
        canViewCost
          ? apiRequest<CashSettlementDto[] | { items: CashSettlementDto[] }>("/api/payments").catch(
              () => [] as CashSettlementDto[]
            )
          : Promise.resolve([] as CashSettlementDto[]),
        canViewRevenue
          ? apiRequest<CashSettlementDto[] | { items: CashSettlementDto[] }>(
              "/api/collections"
            ).catch(() => [] as CashSettlementDto[])
          : Promise.resolve([] as CashSettlementDto[]),
        apiRequest<BankFeedLineDto[] | { items: BankFeedLineDto[] }>(
          "/api/bank-feed/lines"
        ).catch(() => [] as BankFeedLineDto[]),
        canViewCost
          ? apiRequest<ApArItemDto[] | { items: ApArItemDto[] }>(
              "/api/accounts-payable"
            ).catch(() => [] as ApArItemDto[])
          : Promise.resolve([] as ApArItemDto[]),
        canViewRevenue
          ? apiRequest<ApArItemDto[] | { items: ApArItemDto[] }>(
              "/api/accounts-receivable"
            ).catch(() => [] as ApArItemDto[])
          : Promise.resolve([] as ApArItemDto[]),
      ]);

      setPayments(Array.isArray(payRes) ? payRes : payRes?.items ?? []);
      setCollections(Array.isArray(colRes) ? colRes : colRes?.items ?? []);
      setBankLines(Array.isArray(bankRes) ? bankRes : bankRes?.items ?? []);

      const rawAp = Array.isArray(apRes) ? apRes : apRes?.items ?? [];
      setOpenApList(rawAp.filter((x) => x.remainingBalance > 0));

      const rawAr = Array.isArray(arRes) ? arRes : arRes?.items ?? [];
      setOpenArList(rawAr.filter((x) => x.remainingBalance > 0));
    } finally {
      setLoading(false);
    }
  }, [canViewCost, canViewRevenue]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const loadCashDetail = async (id: string, kind: "payments" | "collections") => {
    try {
      const detail = await apiRequest<CashSettlementDetailDto>(`/api/${kind}/${id}`);
      setSelectedCash(detail);
      setSelectedTargetApAr(null);
      const unalloc =
        detail.unallocatedAmount ??
        Math.max(0, detail.amount - (detail.allocatedAmount ?? 0));
      setAllocateAmount(unalloc > 0 ? String(unalloc) : "");
    } catch (err) {
      Alert.alert(
        "Không thể tải chi tiết phiếu",
        err instanceof Error ? err.message : "Lỗi kết nối máy chủ."
      );
    }
  };

  const handleCreateCash = async () => {
    const amt = Number(cashAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Số tiền không hợp lệ", "Vui lòng nhập số tiền lớn hơn 0.");
      return;
    }

    const isPayment = subTab === "payments";
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: isPayment
        ? "Lập Phiếu chi Thanh toán (Create Payment)"
        : "Lập Phiếu thu Tiền hàng (Create Collection)",
      amountSummaryVi: formatMoney(amt, cashCurrency),
    });
    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      const endpoint = isPayment ? "/api/payments" : "/api/collections";
      await apiRequest(endpoint, {
        method: "POST",
        idempotencyKey: createIdempotencyKey(isPayment ? "pay" : "col"),
        body: {
          amount: amt,
          currencyCode: cashCurrency,
          valueDate: new Date().toISOString().slice(0, 10),
          billId: cashBillRef.trim() || null,
          referenceNo:
            cashRefNo.trim() ||
            `${isPayment ? "PAY" : "COL"}-${new Date()
              .toISOString()
              .slice(2, 10)
              .replace(/-/g, "")}-${Math.floor(100 + Math.random() * 900)}`,
          notes: cashNotes.trim() || null,
        },
      });
      setShowCreateCash(false);
      setCashAmount("");
      setCashRefNo("");
      setCashBillRef("");
      setCashNotes("");
      await loadAll();
      Alert.alert(
        "Thành công",
        isPayment
          ? "Đã lập Phiếu chi mới và lưu vết kiểm toán."
          : "Đã lập Phiếu thu mới và lưu vết kiểm toán."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi lập phiếu",
        err instanceof Error ? err.message : "Không thể tạo phiếu thu/chi."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAllocateToApAr = async () => {
    if (!selectedCash || !selectedTargetApAr) return;
    const amt = Number(allocateAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Số tiền phân bổ không hợp lệ", "Vui lòng nhập số tiền phân bổ > 0.");
      return;
    }

    const isPayment = subTab === "payments";
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: isPayment
        ? "Phân bổ Phiếu chi gạch nợ AP"
        : "Phân bổ Phiếu thu gạch nợ AR",
      amountSummaryVi: formatMoney(amt, selectedCash.currencyCode),
    });
    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      const endpoint = isPayment
        ? `/api/payments/${selectedCash.id}/allocations`
        : `/api/collections/${selectedCash.id}/allocations`;

      await apiRequest(endpoint, {
        method: "POST",
        ifMatch: selectedCash.rowVersion ?? selectedTargetApAr.rowVersion,
        idempotencyKey: createIdempotencyKey(isPayment ? "pay-alloc" : "col-alloc"),
        body: isPayment
          ? {
              accountsPayableId: selectedTargetApAr.id,
              amount: amt,
              notes: actionReason.trim() || "Phân bổ thanh toán AP trên di động",
            }
          : {
              accountsReceivableId: selectedTargetApAr.id,
              amount: amt,
              notes: actionReason.trim() || "Phân bổ thu tiền AR trên di động",
            },
      });

      setSelectedTargetApAr(null);
      setActionReason("");
      await loadAll();
      await loadCashDetail(selectedCash.id, isPayment ? "payments" : "collections");
      Alert.alert(
        "Đã phân bổ công nợ",
        "Đã ghi nhận phân bổ tiền vào khoản công nợ và cập nhật Sổ công nợ (ADR-0037)."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi phân bổ",
        err instanceof Error ? err.message : "Không thể phân bổ vào công nợ."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancelCash = async (cash: CashSettlementDetailDto) => {
    if (!actionReason.trim()) {
      Alert.alert(
        "Thiếu lý do kiểm toán",
        "Vui lòng nhập lý do hủy phiếu thu/chi vào ô lý do."
      );
      return;
    }
    const isPayment = subTab === "payments";
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: isPayment ? "Hủy Phiếu chi" : "Hủy Phiếu thu",
      amountSummaryVi: formatMoney(cash.amount, cash.currencyCode),
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      const endpoint = isPayment
        ? `/api/payments/${cash.id}/cancel`
        : `/api/collections/${cash.id}/cancel`;
      await apiRequest(endpoint, {
        method: "POST",
        body: { reason: actionReason.trim() },
      });
      setSelectedCash(null);
      setActionReason("");
      await loadAll();
      Alert.alert("Đã hủy phiếu", "Phiếu đã được chuyển sang trạng thái Đã hủy.");
    } catch (err) {
      Alert.alert(
        "Không thể hủy phiếu",
        err instanceof Error ? err.message : "Lỗi khi hủy phiếu."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAllocationAction = async (
    alloc: AllocationItemDto,
    action: "finalize" | "reverse"
  ) => {
    if (action === "reverse" && !actionReason.trim()) {
      Alert.alert(
        "Thiếu lý do đảo bút toán",
        "Vui lòng nhập lý do đảo phân bổ vào ô lý do kiểm toán."
      );
      return;
    }

    const isPayment = subTab === "payments";
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi:
        action === "finalize" ? "Chốt phân bổ công nợ" : "Đảo bút toán phân bổ",
      amountSummaryVi: formatMoney(alloc.amount, selectedCash?.currencyCode ?? "VND"),
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      const prefix = isPayment
        ? "/api/payment-allocations"
        : "/api/collection-allocations";
      await apiRequest(`${prefix}/${alloc.id}/${action}`, {
        method: "POST",
        ifMatch: alloc.rowVersion,
        body: action === "reverse" ? { reason: actionReason.trim() } : {},
      });
      if (selectedCash) {
        await loadCashDetail(
          selectedCash.id,
          isPayment ? "payments" : "collections"
        );
      }
      await loadAll();
      Alert.alert(
        "Thành công",
        action === "finalize"
          ? "Đã chốt phân bổ thanh toán."
          : "Đã đảo phân bổ và hoàn lại số dư công nợ."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi thao tác phân bổ",
        err instanceof Error ? err.message : "Không thể cập nhật phân bổ."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateBankLine = async () => {
    const amt = Number(bankAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Thiếu số tiền", "Vui lòng nhập số tiền dòng sao kê > 0.");
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest("/api/bank-feed/lines", {
        method: "POST",
        body: {
          valueDate: new Date().toISOString().slice(0, 10),
          amount: amt,
          currencyCode: cashCurrency,
          direction: bankDirection,
          bankReference:
            bankRef.trim() || `STMT-${Math.floor(1000 + Math.random() * 9000)}`,
          counterpartyName: bankCounterparty.trim() || null,
          description: bankDesc.trim() || "Dòng sao kê nhập từ ứng dụng di động",
        },
      });
      setShowCreateBank(false);
      setBankAmount("");
      setBankRef("");
      setBankCounterparty("");
      setBankDesc("");
      await loadAll();
      Alert.alert("Thành công", "Đã thêm dòng sao kê ngân hàng.");
    } catch (err) {
      Alert.alert(
        "Lỗi tạo dòng sao kê",
        err instanceof Error ? err.message : "Không thể thêm dòng sao kê."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleIgnoreBankLine = async (lineId: string) => {
    if (!ignoreReason.trim()) {
      Alert.alert("Thiếu lý do", "Vui lòng nhập lý do bỏ qua dòng sao kê.");
      return;
    }
    setSubmitting(true);
    try {
      await apiRequest(`/api/bank-feed/lines/${lineId}/ignore`, {
        method: "POST",
        body: { ignoreReason: ignoreReason.trim() },
      });
      setIgnoreLineId(null);
      setIgnoreReason("");
      await loadAll();
      Alert.alert("Đã cập nhật", "Dòng sao kê đã được đánh dấu bỏ qua có lý do.");
    } catch (err) {
      Alert.alert(
        "Lỗi thao tác sao kê",
        err instanceof Error ? err.message : "Không thể bỏ qua dòng sao kê."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const activeCashList = subTab === "payments" ? payments : collections;
  const candidateApArList = subTab === "payments" ? openApList : openArList;

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadAll()} />
      }
    >
      <View style={styles.tabRow}>
        {canViewCost ? (
          <TouchableOpacity
            style={[styles.tabBtn, subTab === "payments" && styles.tabBtnActive]}
            onPress={() => {
              setSubTab("payments");
              setSelectedCash(null);
            }}
          >
            <Text
              style={[
                styles.tabBtnText,
                subTab === "payments" && styles.tabBtnTextActive,
              ]}
            >
              💸 Phiếu chi ({payments.length})
            </Text>
          </TouchableOpacity>
        ) : null}

        {canViewRevenue ? (
          <TouchableOpacity
            style={[
              styles.tabBtn,
              subTab === "collections" && styles.tabBtnActive,
            ]}
            onPress={() => {
              setSubTab("collections");
              setSelectedCash(null);
            }}
          >
            <Text
              style={[
                styles.tabBtnText,
                subTab === "collections" && styles.tabBtnTextActive,
              ]}
            >
              💰 Phiếu thu ({collections.length})
            </Text>
          </TouchableOpacity>
        ) : null}

        <TouchableOpacity
          style={[styles.tabBtn, subTab === "bank" && styles.tabBtnActive]}
          onPress={() => {
            setSubTab("bank");
            setSelectedCash(null);
          }}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "bank" && styles.tabBtnTextActive,
            ]}
          >
            🏦 Sao kê ({bankLines.length})
          </Text>
        </TouchableOpacity>
      </View>

      {subTab === "payments" || subTab === "collections" ? (
        <>
          <View style={styles.headerActionRow}>
            <Text style={styles.sectionTitle}>
              {subTab === "payments"
                ? "Quản lý Phiếu chi & Gạch nợ AP"
                : "Quản lý Phiếu thu & Gạch nợ AR"}
            </Text>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => setShowCreateCash((v) => !v)}
            >
              <Text style={styles.primaryBtnText}>
                {showCreateCash
                  ? "✕ Đóng"
                  : subTab === "payments"
                  ? "+ Lập Phiếu chi"
                  : "+ Lập Phiếu thu"}
              </Text>
            </TouchableOpacity>
          </View>

          {showCreateCash ? (
            <View style={styles.formCard}>
              <Text style={styles.formTitle}>
                {subTab === "payments"
                  ? "Lập Phiếu chi Thanh toán Nhà cung cấp"
                  : "Lập Phiếu thu Tiền Khách hàng"}
              </Text>

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Số tiền thực thu/chi</Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    placeholder="VD: 15000000"
                    value={cashAmount}
                    onChangeText={setCashAmount}
                  />
                </View>
                <View style={{ width: 110 }}>
                  <Text style={styles.label}>Nguyên tệ</Text>
                  <View style={styles.chipRow}>
                    {(["VND", "USD"] as const).map((cur) => (
                      <TouchableOpacity
                        key={cur}
                        style={[
                          styles.chip,
                          cashCurrency === cur && styles.chipActive,
                        ]}
                        onPress={() => setCashCurrency(cur)}
                      >
                        <Text
                          style={[
                            styles.chipText,
                            cashCurrency === cur && styles.chipTextActive,
                          ]}
                        >
                          {cur}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
              </View>

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Số tham chiếu NH / UNC</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="VD: UNC-2026-091"
                    value={cashRefNo}
                    onChangeText={setCashRefNo}
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã Bill liên kết (Tùy chọn)</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="VD: HAWB-..."
                    value={cashBillRef}
                    onChangeText={setCashBillRef}
                    autoCapitalize="characters"
                  />
                </View>
              </View>

              <Text style={styles.label}>Nội dung thanh toán / Diễn giải</Text>
              <TextInput
                style={styles.input}
                placeholder="Nhập diễn giải chứng từ ngân hàng..."
                value={cashNotes}
                onChangeText={setCashNotes}
              />

              <TouchableOpacity
                style={[styles.submitBtn, submitting && { opacity: 0.6 }]}
                onPress={handleCreateCash}
                disabled={submitting}
              >
                <Text style={styles.submitBtnText}>
                  {submitting
                    ? "Đang xử lý..."
                    : "🔒 Xác thực Sinh trắc học & Lưu Phiếu"}
                </Text>
              </TouchableOpacity>
            </View>
          ) : null}

          {selectedCash ? (
            <View style={styles.detailSheet}>
              <View style={styles.rowBetween}>
                <Text style={styles.formTitle}>
                  Chi tiết:{" "}
                  {selectedCash.paymentNo ??
                    selectedCash.collectionNo ??
                    selectedCash.referenceNo ??
                    selectedCash.id.slice(0, 8)}
                </Text>
                <TouchableOpacity onPress={() => setSelectedCash(null)}>
                  <Text style={styles.closeLink}>Đóng ✕</Text>
                </TouchableOpacity>
              </View>

              <Text style={styles.metaText}>
                Tổng tiền:{" "}
                <Text style={styles.boldText}>
                  {formatMoney(selectedCash.amount, selectedCash.currencyCode)}
                </Text>{" "}
                • Đã phân bổ:{" "}
                {formatMoney(
                  selectedCash.allocatedAmount ?? 0,
                  selectedCash.currencyCode
                )}{" "}
                • Còn lại:{" "}
                <Text style={styles.highlightText}>
                  {formatMoney(
                    selectedCash.unallocatedAmount ??
                      Math.max(
                        0,
                        selectedCash.amount - (selectedCash.allocatedAmount ?? 0)
                      ),
                    selectedCash.currencyCode
                  )}
                </Text>
              </Text>

              <Text style={styles.subHeader}>
                1. Phân bổ vào {subTab === "payments" ? "Khoản Phải trả (AP)" : "Khoản Phải thu (AR)"} đang mở:
              </Text>

              {candidateApArList.length === 0 ? (
                <Text style={styles.emptySmall}>
                  Không có khoản công nợ mở cùng phía để phân bổ.
                </Text>
              ) : (
                <ScrollView
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  style={{ marginVertical: 6 }}
                >
                  {candidateApArList
                    .filter((item) => item.currencyCode === selectedCash.currencyCode)
                    .map((item) => {
                      const active = selectedTargetApAr?.id === item.id;
                      return (
                        <TouchableOpacity
                          key={item.id}
                          style={[
                            styles.targetCard,
                            active && styles.targetCardActive,
                          ]}
                          onPress={() => {
                            setSelectedTargetApAr(item);
                            const unalloc =
                              selectedCash.unallocatedAmount ??
                              Math.max(
                                0,
                                selectedCash.amount -
                                  (selectedCash.allocatedAmount ?? 0)
                              );
                            setAllocateAmount(
                              String(Math.min(unalloc, item.remainingBalance))
                            );
                          }}
                        >
                          <Text style={styles.targetCode}>
                            {item.documentNo ?? item.id.slice(0, 8)}
                          </Text>
                          <Text style={styles.targetParty}>
                            {item.partyName ?? "Đối tác"}
                          </Text>
                          <Text style={styles.targetAmt}>
                            Dư nợ: {formatMoney(item.remainingBalance, item.currencyCode)}
                          </Text>
                        </TouchableOpacity>
                      );
                    })}
                </ScrollView>
              )}

              {selectedTargetApAr ? (
                <View style={{ marginTop: 8 }}>
                  <Text style={styles.label}>Số tiền phân bổ gạch nợ</Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    value={allocateAmount}
                    onChangeText={setAllocateAmount}
                    placeholder="Nhập số tiền phân bổ"
                  />
                  <TextInput
                    style={styles.input}
                    value={actionReason}
                    onChangeText={setActionReason}
                    placeholder="Ghi chú phân bổ / Lý do kiểm toán..."
                  />
                  <BalanceImpactCard
                    titleVi={`Tác động Số dư ${
                      subTab === "payments" ? "AP" : "AR"
                    } (${selectedTargetApAr.documentNo ?? "Công nợ"})`}
                    subtitleVi={`Trừ vào số dư còn lại của đối tác ${
                      selectedTargetApAr.partyName ?? ""
                    }`}
                    currencyCode={selectedTargetApAr.currencyCode}
                    beforeAmount={selectedTargetApAr.remainingBalance}
                    afterAmount={Math.max(
                      0,
                      selectedTargetApAr.remainingBalance -
                        (Number(allocateAmount) || 0)
                    )}
                    deltaLabelVi="Số tiền gạch nợ"
                    rowVersion={selectedTargetApAr.rowVersion}
                    confirmLabelVi="Phân bổ Gạch nợ"
                    isSubmitting={submitting}
                    onConfirmWithBiometric={handleAllocateToApAr}
                    onCancel={() => setSelectedTargetApAr(null)}
                  />
                </View>
              ) : null}

              {selectedCash.allocations && selectedCash.allocations.length > 0 ? (
                <View style={{ marginTop: 10 }}>
                  <Text style={styles.subHeader}>
                    2. Danh sách bút toán phân bổ ({selectedCash.allocations.length}):
                  </Text>
                  {selectedCash.allocations.map((alloc) => (
                    <View key={alloc.id} style={styles.allocRow}>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.allocText}>
                          {alloc.documentNo ?? alloc.id.slice(0, 8)} •{" "}
                          {formatMoney(alloc.amount, selectedCash.currencyCode)}
                        </Text>
                        <Text style={styles.metaText}>
                          Trạng thái: {alloc.status}
                        </Text>
                      </View>
                      <View style={styles.rowActions}>
                        {alloc.status !== "finalized" &&
                        alloc.status !== "reversed" ? (
                          <TouchableOpacity
                            style={styles.smallBtn}
                            onPress={() =>
                              handleAllocationAction(alloc, "finalize")
                            }
                          >
                            <Text style={styles.smallBtnText}>Chốt</Text>
                          </TouchableOpacity>
                        ) : null}
                        {alloc.status !== "reversed" ? (
                          <TouchableOpacity
                            style={styles.smallDangerBtn}
                            onPress={() =>
                              handleAllocationAction(alloc, "reverse")
                            }
                          >
                            <Text style={styles.smallDangerText}>Đảo</Text>
                          </TouchableOpacity>
                        ) : null}
                      </View>
                    </View>
                  ))}
                </View>
              ) : null}

              <View style={styles.cancelZone}>
                <TextInput
                  style={styles.input}
                  placeholder="Nhập lý do hủy phiếu hoặc đảo phân bổ (bắt buộc)..."
                  value={actionReason}
                  onChangeText={setActionReason}
                />
                {selectedCash.status !== "cancelled" ? (
                  <TouchableOpacity
                    style={styles.dangerOutlineBtn}
                    onPress={() => handleCancelCash(selectedCash)}
                    disabled={submitting}
                  >
                    <Text style={styles.dangerOutlineText}>
                      ✕ Hủy phiếu {subTab === "payments" ? "chi" : "thu"} này (Có lý do)
                    </Text>
                  </TouchableOpacity>
                ) : null}
              </View>
            </View>
          ) : null}

          {loading ? (
            <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
          ) : activeCashList.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>
                Chưa có {subTab === "payments" ? "Phiếu chi" : "Phiếu thu"} nào
              </Text>
              <Text style={styles.emptySubtitle}>
                Nhấn nút &quot;+ Lập Phiếu&quot; phía trên để ghi nhận giao dịch thanh toán mới.
              </Text>
            </View>
          ) : (
            activeCashList.map((item) => {
              const unalloc =
                item.unallocatedAmount ??
                Math.max(0, item.amount - (item.allocatedAmount ?? 0));
              return (
                <TouchableOpacity
                  key={item.id}
                  style={styles.card}
                  onPress={() =>
                    void loadCashDetail(
                      item.id,
                      subTab === "payments" ? "payments" : "collections"
                    )
                  }
                >
                  <View style={styles.rowBetween}>
                    <Text style={styles.cardCode}>
                      {item.paymentNo ??
                        item.collectionNo ??
                        item.referenceNo ??
                        item.id.slice(0, 8)}
                    </Text>
                    <View style={styles.badge}>
                      <Text style={styles.badgeText}>{item.status}</Text>
                    </View>
                  </View>
                  <Text style={styles.metaText}>
                    Đối tác: {item.counterpartyName ?? "Chưa gán"} • Ngày:{" "}
                    {item.valueDate ?? "—"}
                  </Text>
                  <View style={[styles.rowBetween, { marginTop: 8 }]}>
                    <Text style={styles.amountText}>
                      Tổng: {formatMoney(item.amount, item.currencyCode)}
                    </Text>
                    <Text style={styles.unallocText}>
                      Chờ phân bổ: {formatMoney(unalloc, item.currencyCode)}
                    </Text>
                  </View>
                </TouchableOpacity>
              );
            })
          )}
        </>
      ) : (
        <>
          <View style={styles.headerActionRow}>
            <Text style={styles.sectionTitle}>
              Dòng Sao kê Ngân hàng (Bank Feed • MT940/CSV)
            </Text>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => setShowCreateBank((v) => !v)}
            >
              <Text style={styles.primaryBtnText}>
                {showCreateBank ? "✕ Đóng" : "+ Nhập dòng Sao kê"}
              </Text>
            </TouchableOpacity>
          </View>

          {showCreateBank ? (
            <View style={styles.formCard}>
              <Text style={styles.formTitle}>Thêm Dòng Sao kê Ngân hàng</Text>
              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Số tiền giao dịch</Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    value={bankAmount}
                    onChangeText={setBankAmount}
                    placeholder="VD: 25000000"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Chiều dòng tiền</Text>
                  <View style={styles.chipRow}>
                    <TouchableOpacity
                      style={[
                        styles.chip,
                        bankDirection === "credit" && styles.chipActive,
                      ]}
                      onPress={() => setBankDirection("credit")}
                    >
                      <Text
                        style={[
                          styles.chipText,
                          bankDirection === "credit" && styles.chipTextActive,
                        ]}
                      >
                        Báo Có (Thu)
                      </Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={[
                        styles.chip,
                        bankDirection === "debit" && styles.chipActive,
                      ]}
                      onPress={() => setBankDirection("debit")}
                    >
                      <Text
                        style={[
                          styles.chipText,
                          bankDirection === "debit" && styles.chipTextActive,
                        ]}
                      >
                        Báo Nợ (Chi)
                      </Text>
                    </TouchableOpacity>
                  </View>
                </View>
              </View>

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã tham chiếu NH</Text>
                  <TextInput
                    style={styles.input}
                    value={bankRef}
                    onChangeText={setBankRef}
                    placeholder="FT26273..."
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Tên đối tác trên sao kê</Text>
                  <TextInput
                    style={styles.input}
                    value={bankCounterparty}
                    onChangeText={setBankCounterparty}
                    placeholder="Công ty..."
                  />
                </View>
              </View>

              <Text style={styles.label}>Nội dung chuyển khoản</Text>
              <TextInput
                style={styles.input}
                value={bankDesc}
                onChangeText={setBankDesc}
                placeholder="Thanh toan cuoc van chuyen..."
              />

              <TouchableOpacity
                style={styles.submitBtn}
                onPress={handleCreateBankLine}
                disabled={submitting}
              >
                <Text style={styles.submitBtnText}>+ Lưu Dòng Sao kê</Text>
              </TouchableOpacity>
            </View>
          ) : null}

          {loading ? (
            <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
          ) : bankLines.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>Chưa có dòng sao kê ngân hàng</Text>
            </View>
          ) : (
            bankLines.map((line) => (
              <View key={line.id} style={styles.card}>
                <View style={styles.rowBetween}>
                  <Text style={styles.cardCode}>
                    {line.bankReference ?? line.id.slice(0, 8)} (
                    {(line.direction ?? "credit").toUpperCase()})
                  </Text>
                  <View style={styles.badge}>
                    <Text style={styles.badgeText}>{line.status}</Text>
                  </View>
                </View>
                <Text style={styles.amountText}>
                  {formatMoney(line.amount, line.currencyCode)} • Ngày:{" "}
                  {line.valueDate}
                </Text>
                <Text style={styles.metaText}>
                  Đối tác: {line.counterpartyName ?? "—"} • {line.description ?? ""}
                </Text>

                {line.status === "unmatched" ? (
                  <View style={{ marginTop: 8 }}>
                    {ignoreLineId === line.id ? (
                      <View>
                        <TextInput
                          style={styles.input}
                          placeholder="Nhập lý do bỏ qua dòng sao kê này..."
                          value={ignoreReason}
                          onChangeText={setIgnoreReason}
                        />
                        <View style={styles.rowActions}>
                          <TouchableOpacity
                            style={styles.smallDangerBtn}
                            onPress={() => handleIgnoreBankLine(line.id)}
                          >
                            <Text style={styles.smallDangerText}>
                              Xác nhận Bỏ qua
                            </Text>
                          </TouchableOpacity>
                          <TouchableOpacity
                            style={styles.smallBtn}
                            onPress={() => setIgnoreLineId(null)}
                          >
                            <Text style={styles.smallBtnText}>Hủy</Text>
                          </TouchableOpacity>
                        </View>
                      </View>
                    ) : (
                      <TouchableOpacity
                        style={styles.smallBtn}
                        onPress={() => setIgnoreLineId(line.id)}
                      >
                        <Text style={styles.smallBtnText}>
                          Bỏ qua dòng sao kê (Có lý do)
                        </Text>
                      </TouchableOpacity>
                    )}
                  </View>
                ) : null}
              </View>
            ))
          )}
        </>
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
    marginBottom: 14,
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
  headerActionRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: "800",
    color: "#0F172A",
    flex: 1,
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
  detailSheet: {
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
    marginBottom: 8,
  },
  subHeader: {
    fontSize: 13,
    fontWeight: "700",
    color: "#1E293B",
    marginTop: 10,
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
    marginBottom: 6,
  },
  twoCol: {
    flexDirection: "row",
    gap: 10,
  },
  chipRow: {
    flexDirection: "row",
    gap: 6,
  },
  chip: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 7,
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
    marginTop: 8,
  },
  submitBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
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
  cardCode: {
    fontSize: 15,
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
  boldText: {
    fontWeight: "700",
    color: "#0F172A",
  },
  highlightText: {
    fontWeight: "700",
    color: "#0F766E",
  },
  amountText: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
    marginTop: 4,
  },
  unallocText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F766E",
  },
  targetCard: {
    backgroundColor: "#F8FAFC",
    borderRadius: 10,
    padding: 10,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginRight: 8,
    minWidth: 170,
  },
  targetCardActive: {
    borderColor: "#2563EB",
    backgroundColor: "#EFF6FF",
  },
  targetCode: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
  },
  targetParty: {
    fontSize: 11,
    color: "#475569",
    marginTop: 2,
  },
  targetAmt: {
    fontSize: 12,
    fontWeight: "700",
    color: "#1D4ED8",
    marginTop: 4,
  },
  allocRow: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: "#F1F5F9",
  },
  allocText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  rowActions: {
    flexDirection: "row",
    gap: 6,
  },
  smallBtn: {
    backgroundColor: "#EFF6FF",
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 6,
    alignSelf: "flex-start",
  },
  smallBtnText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#1D4ED8",
  },
  smallDangerBtn: {
    backgroundColor: "#FEF2F2",
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 6,
  },
  smallDangerText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#DC2626",
  },
  cancelZone: {
    marginTop: 12,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: "#E2E8F0",
  },
  dangerOutlineBtn: {
    borderWidth: 1,
    borderColor: "#FECACA",
    backgroundColor: "#FEF2F2",
    paddingVertical: 9,
    borderRadius: 8,
    alignItems: "center",
  },
  dangerOutlineText: {
    color: "#DC2626",
    fontSize: 12,
    fontWeight: "700",
  },
  closeLink: {
    fontSize: 13,
    fontWeight: "700",
    color: "#64748B",
  },
  emptySmall: {
    fontSize: 12,
    color: "#64748B",
    fontStyle: "italic",
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

