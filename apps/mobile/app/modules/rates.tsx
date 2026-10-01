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
import { BillListItemDto, formatMoney } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";

interface RateCardDto {
  id: string;
  code: string;
  name: string;
  partyType: string;
  currencyCode: string;
  description?: string | null;
  transportMode?: string | null;
  routeCode?: string | null;
  carrierName?: string | null;
  isActive?: boolean;
}

interface RateVersionDto {
  id: string;
  versionNo?: number;
  status?: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  note?: string | null;
}

interface PricingRuleDto {
  id: string;
  code: string;
  name: string;
  calcMethod: string;
  unitAmount: number;
  currencyCode: string;
  minAmount?: number | null;
  maxAmount?: number | null;
  originCode?: string | null;
  destinationCode?: string | null;
}

interface RateComparisonQuoteDto {
  rateCardId?: string;
  rateVersionId?: string;
  code?: string;
  name?: string;
  carrierName?: string | null;
  totalAmount?: number;
  amount?: number;
  currency?: string;
  currencyCode?: string;
  breakdown?: Array<{ code?: string; name?: string; amount?: number }>;
}

interface SurchargeDto {
  id: string;
  code: string;
  name: string;
  calcMethod?: string | null;
  amount?: number | null;
  currencyCode?: string | null;
  transportMode?: string | null;
  isActive?: boolean;
}

interface RatingHistoryDto {
  id: string;
  billId?: string | null;
  billNo?: string | null;
  rateCardCode?: string | null;
  totalAmount?: number;
  currencyCode?: string;
  calculatedAt?: string;
  status?: string;
}

export default function RatesModuleScreen() {
  const { bootstrap } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;
  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? false;

  const [subTab, setSubTab] = useState<"cards" | "compare" | "surcharges">(
    "cards"
  );
  const [loading, setLoading] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Rate cards list & filter
  const [rateCards, setRateCards] = useState<RateCardDto[]>([]);
  const [search, setSearch] = useState<string>("");
  const [modeFilter, setModeFilter] = useState<"ALL" | "ROAD" | "AIR" | "SEA">(
    "ALL"
  );
  const [selectedCard, setSelectedCard] = useState<RateCardDto | null>(null);
  const [versions, setVersions] = useState<RateVersionDto[]>([]);
  const [selectedVersionId, setSelectedVersionId] = useState<string | null>(null);
  const [rules, setRules] = useState<PricingRuleDto[]>([]);

  // Create Rate Card form
  const [showCreateCard, setShowCreateCard] = useState<boolean>(false);
  const [cardCode, setCardCode] = useState<string>("");
  const [cardName, setCardName] = useState<string>("");
  const [cardPartyType, setCardPartyType] = useState<"vendor" | "customer">(
    canViewCost ? "vendor" : "customer"
  );
  const [cardCurrency, setCardCurrency] = useState<"VND" | "USD">("VND");
  const [cardMode, setCardMode] = useState<"ROAD" | "AIR" | "SEA">("ROAD");
  const [cardRoute, setCardRoute] = useState<string>("SGN-HAN");
  const [cardCarrier, setCardCarrier] = useState<string>("");

  // Add Pricing Rule form
  const [showAddRule, setShowAddRule] = useState<boolean>(false);
  const [ruleCode, setRuleCode] = useState<string>("");
  const [ruleName, setRuleName] = useState<string>("");
  const [ruleMethod, setRuleMethod] = useState<"PER_KG" | "FLAT" | "PER_CBM">(
    "PER_KG"
  );
  const [ruleUnitAmt, setRuleUnitAmt] = useState<string>("");
  const [ruleMinAmt, setRuleMinAmt] = useState<string>("");

  // Compare & Calculate
  const [bills, setBills] = useState<BillListItemDto[]>([]);
  const [selectedBillId, setSelectedBillId] = useState<string>("");
  const [cmpOrigin, setCmpOrigin] = useState<string>("SGN");
  const [cmpDest, setCmpDest] = useState<string>("HAN");
  const [cmpMode, setCmpMode] = useState<"ROAD" | "AIR" | "SEA">("ROAD");
  const [cmpPartyType, setCmpPartyType] = useState<"vendor" | "customer">(
    canViewCost ? "vendor" : "customer"
  );
  const [cmpWeightKg, setCmpWeightKg] = useState<string>("150");
  const [cmpVolumeCbm, setCmpVolumeCbm] = useState<string>("1.2");
  const [quotes, setQuotes] = useState<RateComparisonQuoteDto[]>([]);
  const [readinessMsg, setReadinessMsg] = useState<string | null>(null);

  // Surcharges & History
  const [surcharges, setSurcharges] = useState<SurchargeDto[]>([]);
  const [history, setHistory] = useState<RatingHistoryDto[]>([]);

  const loadAll = useCallback(async () => {
    setLoading(true);
    try {
      const [cardsRes, billsRes, surRes, histRes] = await Promise.all([
        apiRequest<RateCardDto[] | { items: RateCardDto[] }>(
          "/api/rate-cards"
        ).catch(() => [] as RateCardDto[]),
        apiRequest<BillListItemDto[] | { items: BillListItemDto[] }>(
          "/api/bills"
        ).catch(() => [] as BillListItemDto[]),
        apiRequest<SurchargeDto[] | { items: SurchargeDto[] }>(
          "/api/surcharges"
        ).catch(() => [] as SurchargeDto[]),
        apiRequest<RatingHistoryDto[] | { items: RatingHistoryDto[] }>(
          "/api/rating-history"
        ).catch(() => [] as RatingHistoryDto[]),
      ]);

      const rawCards = Array.isArray(cardsRes)
        ? cardsRes
        : cardsRes?.items ?? [];

      // Enforce SoD filter on Buy vs Sell rate cards
      const sodFilteredCards = rawCards.filter((c) => {
        const pt = (c.partyType || "").toLowerCase();
        if (pt === "vendor" || pt === "buy" || pt === "carrier") {
          return canViewCost || (!canViewCost && !canViewRevenue);
        }
        if (pt === "customer" || pt === "sell") {
          return canViewRevenue || (!canViewCost && !canViewRevenue);
        }
        return true;
      });

      setRateCards(sodFilteredCards);
      const billList = Array.isArray(billsRes) ? billsRes : billsRes?.items ?? [];
      setBills(billList);
      if (billList.length > 0 && !selectedBillId) {
        setSelectedBillId(billList[0].id);
      }
      setSurcharges(Array.isArray(surRes) ? surRes : surRes?.items ?? []);
      setHistory(Array.isArray(histRes) ? histRes : histRes?.items ?? []);
    } finally {
      setLoading(false);
    }
  }, [canViewCost, canViewRevenue, selectedBillId]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const loadCardVersions = async (card: RateCardDto) => {
    setSelectedCard(card);
    setSelectedVersionId(null);
    setRules([]);
    try {
      const verRes = await apiRequest<
        RateVersionDto[] | { items: RateVersionDto[] }
      >(`/api/rate-cards/${card.id}/versions`);
      const verList = Array.isArray(verRes) ? verRes : verRes?.items ?? [];
      setVersions(verList);
      if (verList.length > 0) {
        await loadVersionRules(verList[0].id);
      }
    } catch {
      setVersions([]);
    }
  };

  const loadVersionRules = async (versionId: string) => {
    setSelectedVersionId(versionId);
    try {
      const ruleRes = await apiRequest<
        PricingRuleDto[] | { items: PricingRuleDto[] }
      >(`/api/rate-versions/${versionId}/rules`);
      setRules(Array.isArray(ruleRes) ? ruleRes : ruleRes?.items ?? []);
    } catch {
      setRules([]);
    }
  };

  const handleCreateRateCard = async () => {
    if (!cardName.trim()) {
      Alert.alert("Thiếu thông tin", "Vui lòng nhập Tên bảng giá.");
      return;
    }
    setSubmitting(true);
    try {
      const cleanCode =
        cardCode.trim() ||
        `RC-${cardMode}-${Math.floor(100 + Math.random() * 900)}`;
      const created = await apiRequest<{ id: string }>("/api/rate-cards", {
        method: "POST",
        body: {
          code: cleanCode,
          name: cardName.trim(),
          partyType: cardPartyType,
          currencyCode: cardCurrency,
          transportMode: cardMode,
          routeCode: cardRoute.trim() || null,
          carrierName: cardCarrier.trim() || null,
          description: `Bảng giá ${cardPartyType === "vendor" ? "Mua (Chi phí)" : "Bán (Doanh thu)"} lập trên Mobile`,
        },
      });
      if (created?.id) {
        await apiRequest(`/api/rate-cards/${created.id}/versions`, {
          method: "POST",
          body: {
            effectiveFrom: new Date().toISOString(),
            note: "Phiên bản khởi tạo từ Mobile",
          },
        }).catch(() => undefined);
      }
      setShowCreateCard(false);
      setCardCode("");
      setCardName("");
      await loadAll();
      Alert.alert("Thành công", `Đã tạo bảng giá ${cleanCode}.`);
    } catch (err) {
      Alert.alert(
        "Lỗi tạo bảng giá",
        err instanceof Error ? err.message : "Không thể tạo bảng giá."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateVersion = async (cardId: string) => {
    setSubmitting(true);
    try {
      await apiRequest(`/api/rate-cards/${cardId}/versions`, {
        method: "POST",
        body: {
          effectiveFrom: new Date().toISOString(),
          note: "Phiên bản bổ sung trên ứng dụng di động",
        },
      });
      if (selectedCard) {
        await loadCardVersions(selectedCard);
      }
      Alert.alert("Đã tạo phiên bản", "Đã thêm phiên bản biểu giá mới.");
    } catch (err) {
      Alert.alert(
        "Lỗi tạo phiên bản",
        err instanceof Error ? err.message : "Không thể tạo phiên bản biểu giá."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handlePublishVersion = async (versionId: string) => {
    setSubmitting(true);
    try {
      await apiRequest(`/api/rate-versions/${versionId}/publish`, {
        method: "POST",
        body: {},
      });
      if (selectedCard) {
        await loadCardVersions(selectedCard);
      }
      Alert.alert(
        "Đã phát hành biểu giá",
        "Phiên bản biểu giá đã chuyển sang trạng thái hiệu lực (Published)."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi phát hành",
        err instanceof Error ? err.message : "Không thể phát hành phiên bản."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAddPricingRule = async () => {
    if (!selectedVersionId || ! selectedCard) return;
    const unitAmt = Number(ruleUnitAmt);
    if (!ruleName.trim() || !unitAmt || unitAmt <= 0) {
      Alert.alert(
        "Thiếu thông tin quy tắc giá",
        "Vui lòng nhập tên quy tắc và đơn giá > 0."
      );
      return;
    }

    setSubmitting(true);
    try {
      await apiRequest(`/api/rate-versions/${selectedVersionId}/rules`, {
        method: "POST",
        body: {
          code:
            ruleCode.trim() ||
            `RULE-${Math.floor(100 + Math.random() * 900)}`,
          name: ruleName.trim(),
          calcMethod: ruleMethod,
          unitAmount: unitAmt,
          currencyCode: selectedCard.currencyCode,
          minAmount: Number(ruleMinAmt) > 0 ? Number(ruleMinAmt) : null,
          transportMode: selectedCard.transportMode ?? "ROAD",
          routeCode: selectedCard.routeCode ?? null,
          applicability: "ALL",
          sortOrder: rules.length + 1,
        },
      });
      setShowAddRule(false);
      setRuleCode("");
      setRuleName("");
      setRuleUnitAmt("");
      setRuleMinAmt("");
      await loadVersionRules(selectedVersionId);
      Alert.alert("Đã thêm quy tắc", "Đã bổ sung đơn giá vào phiên bản biểu giá.");
    } catch (err) {
      Alert.alert(
        "Lỗi thêm quy tắc giá",
        err instanceof Error ? err.message : "Không thể thêm quy tắc giá."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCompareRates = async () => {
    setSubmitting(true);
    setReadinessMsg(null);
    try {
      const res = await apiRequest<
        RateComparisonQuoteDto[] | { items: RateComparisonQuoteDto[] }
      >("/api/ratings/compare", {
        method: "POST",
        body: {
          billId: selectedBillId || null,
          partyType: cmpPartyType,
          transportMode: cmpMode,
          originCode: cmpOrigin.trim() || "SGN",
          destinationCode: cmpDest.trim() || "HAN",
          routeCode: `${cmpOrigin.trim() || "SGN"}-${cmpDest.trim() || "HAN"}`,
          quantity: Number(cmpWeightKg) || 1,
          grossWeightKg: Number(cmpWeightKg) || 100,
          volumeCbm: Number(cmpVolumeCbm) || 1,
        },
      });
      setQuotes(Array.isArray(res) ? res : res?.items ?? []);
    } catch (err) {
      Alert.alert(
        "Lỗi so sánh báo giá",
        err instanceof Error ? err.message : "Không thể chạy so sánh bảng giá."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCheckReadinessAndRate = async (rateVersionId?: string) => {
    if (!selectedBillId) {
      Alert.alert("Chưa chọn Vận đơn", "Vui lòng chọn 1 Bill để tính cước.");
      return;
    }
    const targetVersionId =
      rateVersionId ?? selectedVersionId ?? versions[0]?.id;
    if (!targetVersionId) {
      Alert.alert(
        "Chưa chọn Phiên bản Bảng giá",
        "Vui lòng mở 1 Bảng giá ở tab Bảng giá hoặc chọn từ kết quả so sánh."
      );
      return;
    }

    setSubmitting(true);
    try {
      const readiness = await apiRequest<{
        isReady?: boolean;
        canCalculate?: boolean;
        missingParameters?: string[];
      }>("/api/ratings/readiness", {
        method: "POST",
        body: {
          billId: selectedBillId,
          rateVersionId: targetVersionId,
          weight: Number(cmpWeightKg) || 100,
          grossWeightKg: Number(cmpWeightKg) || 100,
          volumeCbm: Number(cmpVolumeCbm) || 1,
          originCode: cmpOrigin.trim() || "SGN",
          destinationCode: cmpDest.trim() || "HAN",
          transportMode: cmpMode,
        },
      });

      const missing = readiness?.missingParameters ?? [];
      if (readiness?.isReady === false && missing.length > 0) {
        setReadinessMsg(`⚠️ Thiếu thông số: ${missing.join(", ")}`);
      } else {
        setReadinessMsg("✓ Bill đủ điều kiện tính giá tự động (Readiness OK)");
      }

      const created = await apiRequest<{ id: string }>("/api/ratings", {
        method: "POST",
        body: {
          billId: selectedBillId,
          rateVersionId: targetVersionId,
          quantity: Number(cmpWeightKg) || 100,
          weight: Number(cmpWeightKg) || 100,
          grossWeightKg: Number(cmpWeightKg) || 100,
          volumeCbm: Number(cmpVolumeCbm) || 1,
          originCode: cmpOrigin.trim() || "SGN",
          destinationCode: cmpDest.trim() || "HAN",
          transportMode: cmpMode,
          seedExpectedCosts: canViewCost && cmpPartyType === "vendor",
          seedExpectedRevenues: canViewRevenue && cmpPartyType === "customer",
        },
      });

      await loadAll();
      Alert.alert(
        "Đã tính cước tự động",
        `Đã hoàn tất tính giá (Mã Rating: ${created?.id?.slice(0, 8) ?? "OK"}) và khởi tạo dữ liệu dự kiến.`
      );
    } catch (err) {
      Alert.alert(
        "Không thể tính cước tự động",
        err instanceof Error ? err.message : "Lỗi khi gọi động cơ tính giá."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const filteredCards = rateCards.filter((c) => {
    if (
      modeFilter !== "ALL" &&
      (c.transportMode || "").toUpperCase() !== modeFilter
    ) {
      return false;
    }
    if (!search.trim()) return true;
    const q = search.trim().toLowerCase();
    return (
      c.code.toLowerCase().includes(q) ||
      c.name.toLowerCase().includes(q) ||
      (c.routeCode ?? "").toLowerCase().includes(q) ||
      (c.carrierName ?? "").toLowerCase().includes(q)
    );
  });

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadAll()} />
      }
    >
      <View style={styles.tabRow}>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "cards" && styles.tabBtnActive]}
          onPress={() => setSubTab("cards")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "cards" && styles.tabBtnTextActive,
            ]}
          >
            📋 Bảng giá ({filteredCards.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "compare" && styles.tabBtnActive]}
          onPress={() => setSubTab("compare")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "compare" && styles.tabBtnTextActive,
            ]}
          >
            🧮 So sánh & Tính cước
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.tabBtn,
            subTab === "surcharges" && styles.tabBtnActive,
          ]}
          onPress={() => setSubTab("surcharges")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "surcharges" && styles.tabBtnTextActive,
            ]}
          >
            ⚡ Phụ phí & Lịch sử
          </Text>
        </TouchableOpacity>
      </View>

      {subTab === "cards" ? (
        <>
          <View style={styles.searchRow}>
            <TextInput
              style={styles.searchInput}
              placeholder="Tìm mã bảng giá, tên, tuyến (SGN-HAN), hãng..."
              value={search}
              onChangeText={setSearch}
            />
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => setShowCreateCard((v) => !v)}
            >
              <Text style={styles.primaryBtnText}>
                {showCreateCard ? "✕ Đóng" : "+ Tạo Bảng giá"}
              </Text>
            </TouchableOpacity>
          </View>

          <View style={styles.chipRow}>
            {(["ALL", "ROAD", "AIR", "SEA"] as const).map((m) => (
              <TouchableOpacity
                key={m}
                style={[styles.chip, modeFilter === m && styles.chipActive]}
                onPress={() => setModeFilter(m)}
              >
                <Text
                  style={[
                    styles.chipText,
                    modeFilter === m && styles.chipTextActive,
                  ]}
                >
                  {m === "ALL"
                    ? "Tất cả phương thức"
                    : m === "ROAD"
                    ? "🚚 Đường bộ"
                    : m === "AIR"
                    ? "✈️ Hàng không"
                    : "🚢 Đường biển"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {showCreateCard ? (
            <View style={styles.formCard}>
              <Text style={styles.formTitle}>Tạo Bảng giá Chuẩn Mới</Text>
              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã bảng giá</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="VD: RC-ROAD-SGN"
                    value={cardCode}
                    onChangeText={setCardCode}
                    autoCapitalize="characters"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Phân loại Biểu giá</Text>
                  <View style={styles.chipRow}>
                    {canViewCost ? (
                      <TouchableOpacity
                        style={[
                          styles.chip,
                          cardPartyType === "vendor" && styles.chipActive,
                        ]}
                        onPress={() => setCardPartyType("vendor")}
                      >
                        <Text
                          style={[
                            styles.chipText,
                            cardPartyType === "vendor" && styles.chipTextActive,
                          ]}
                        >
                          Mua (NCC)
                        </Text>
                      </TouchableOpacity>
                    ) : null}
                    {canViewRevenue ? (
                      <TouchableOpacity
                        style={[
                          styles.chip,
                          cardPartyType === "customer" && styles.chipActive,
                        ]}
                        onPress={() => setCardPartyType("customer")}
                      >
                        <Text
                          style={[
                            styles.chipText,
                            cardPartyType === "customer" &&
                              styles.chipTextActive,
                          ]}
                        >
                          Bán (KH)
                        </Text>
                      </TouchableOpacity>
                    ) : null}
                  </View>
                </View>
              </View>

              <Text style={styles.label}>Tên bảng giá</Text>
              <TextInput
                style={styles.input}
                placeholder="VD: Biểu giá vận tải Bắc Nam 2026"
                value={cardName}
                onChangeText={setCardName}
              />

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã tuyến (Route)</Text>
                  <TextInput
                    style={styles.input}
                    value={cardRoute}
                    onChangeText={setCardRoute}
                    autoCapitalize="characters"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Hãng / Đối tác</Text>
                  <TextInput
                    style={styles.input}
                    value={cardCarrier}
                    onChangeText={setCardCarrier}
                    placeholder="Tên hãng vận tải"
                  />
                </View>
              </View>

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Phương thức</Text>
                  <View style={styles.chipRow}>
                    {(["ROAD", "AIR", "SEA"] as const).map((m) => (
                      <TouchableOpacity
                        key={m}
                        style={[styles.chip, cardMode === m && styles.chipActive]}
                        onPress={() => setCardMode(m)}
                      >
                        <Text
                          style={[
                            styles.chipText,
                            cardMode === m && styles.chipTextActive,
                          ]}
                        >
                          {m}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
                <View style={{ width: 110 }}>
                  <Text style={styles.label}>Tiền tệ</Text>
                  <View style={styles.chipRow}>
                    {(["VND", "USD"] as const).map((cur) => (
                      <TouchableOpacity
                        key={cur}
                        style={[
                          styles.chip,
                          cardCurrency === cur && styles.chipActive,
                        ]}
                        onPress={() => setCardCurrency(cur)}
                      >
                        <Text
                          style={[
                            styles.chipText,
                            cardCurrency === cur && styles.chipTextActive,
                          ]}
                        >
                          {cur}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
              </View>

              <TouchableOpacity
                style={styles.submitBtn}
                onPress={handleCreateRateCard}
                disabled={submitting}
              >
                <Text style={styles.submitBtnText}>✓ Lưu Bảng giá Mới</Text>
              </TouchableOpacity>
            </View>
          ) : null}

          {selectedCard ? (
            <View style={styles.detailCard}>
              <View style={styles.rowBetween}>
                <Text style={styles.formTitle}>
                  {selectedCard.code} • {selectedCard.name}
                </Text>
                <TouchableOpacity onPress={() => setSelectedCard(null)}>
                  <Text style={styles.closeText}>Đóng ✕</Text>
                </TouchableOpacity>
              </View>
              <Text style={styles.metaText}>
                Loại:{" "}
                {selectedCard.partyType.toLowerCase() === "vendor"
                  ? "Giá Mua (Chi phí NCC)"
                  : "Giá Bán (Doanh thu KH)"}{" "}
                • Tiền tệ: {selectedCard.currencyCode} • Tuyến:{" "}
                {selectedCard.routeCode ?? "Toàn mạng"}
              </Text>

              <View style={[styles.rowBetween, { marginTop: 10 }]}>
                <Text style={styles.subHeader}>
                  Phiên bản Hiệu lực ({versions.length}):
                </Text>
                <TouchableOpacity
                  style={styles.smallBtn}
                  onPress={() => handleCreateVersion(selectedCard.id)}
                >
                  <Text style={styles.smallBtnText}>+ Thêm Phiên bản</Text>
                </TouchableOpacity>
              </View>

              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                style={{ marginVertical: 6 }}
              >
                {versions.map((ver, idx) => {
                  const active = selectedVersionId === ver.id;
                  return (
                    <TouchableOpacity
                      key={ver.id}
                      style={[
                        styles.versionPill,
                        active && styles.versionPillActive,
                      ]}
                      onPress={() => void loadVersionRules(ver.id)}
                    >
                      <Text style={styles.versionTitle}>
                        Ver #{ver.versionNo ?? idx + 1} (
                        {ver.status ?? "draft"})
                      </Text>
                      <Text style={styles.versionMeta}>
                        Từ:{" "}
                        {ver.effectiveFrom
                          ? ver.effectiveFrom.slice(0, 10)
                          : "—"}
                      </Text>
                      {ver.status !== "published" ? (
                        <TouchableOpacity
                          style={styles.publishBtn}
                          onPress={() => handlePublishVersion(ver.id)}
                        >
                          <Text style={styles.publishBtnText}>
                            Phát hành ngay
                          </Text>
                        </TouchableOpacity>
                      ) : null}
                    </TouchableOpacity>
                  );
                })}
              </ScrollView>

              {selectedVersionId ? (
                <View style={{ marginTop: 8 }}>
                  <View style={styles.rowBetween}>
                    <Text style={styles.subHeader}>
                      Quy tắc Đơn giá ({rules.length}):
                    </Text>
                    <TouchableOpacity
                      style={styles.smallBtn}
                      onPress={() => setShowAddRule((v) => !v)}
                    >
                      <Text style={styles.smallBtnText}>
                        {showAddRule ? "Đóng" : "+ Thêm Quy tắc giá"}
                      </Text>
                    </TouchableOpacity>
                  </View>

                  {showAddRule ? (
                    <View style={styles.ruleFormBox}>
                      <View style={styles.twoCol}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.label}>Mã quy tắc</Text>
                          <TextInput
                            style={styles.input}
                            value={ruleCode}
                            onChangeText={setRuleCode}
                            placeholder="VD: FREIGHT-KG"
                          />
                        </View>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.label}>Phương pháp tính</Text>
                          <View style={styles.chipRow}>
                            {(["PER_KG", "FLAT", "PER_CBM"] as const).map(
                              (m) => (
                                <TouchableOpacity
                                  key={m}
                                  style={[
                                    styles.chip,
                                    ruleMethod === m && styles.chipActive,
                                  ]}
                                  onPress={() => setRuleMethod(m)}
                                >
                                  <Text
                                    style={[
                                      styles.chipText,
                                      ruleMethod === m && styles.chipTextActive,
                                    ]}
                                  >
                                    {m}
                                  </Text>
                                </TouchableOpacity>
                              )
                            )}
                          </View>
                        </View>
                      </View>

                      <Text style={styles.label}>Tên dòng cước</Text>
                      <TextInput
                        style={styles.input}
                        value={ruleName}
                        onChangeText={setRuleName}
                        placeholder="VD: Cước chính theo Kg tính cước (CW)"
                      />

                      <View style={styles.twoCol}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.label}>Đơn giá ({selectedCard.currencyCode})</Text>
                          <TextInput
                            style={styles.input}
                            keyboardType="numeric"
                            value={ruleUnitAmt}
                            onChangeText={setRuleUnitAmt}
                            placeholder="VD: 15000"
                          />
                        </View>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.label}>Cước tối thiểu (Min)</Text>
                          <TextInput
                            style={styles.input}
                            keyboardType="numeric"
                            value={ruleMinAmt}
                            onChangeText={setRuleMinAmt}
                            placeholder="VD: 250000"
                          />
                        </View>
                      </View>

                      <TouchableOpacity
                        style={styles.submitBtn}
                        onPress={handleAddPricingRule}
                        disabled={submitting}
                      >
                        <Text style={styles.submitBtnText}>
                          + Lưu Quy tắc Đơn giá
                        </Text>
                      </TouchableOpacity>
                    </View>
                  ) : null}

                  {rules.length === 0 ? (
                    <Text style={styles.metaText}>
                      Chưa có quy tắc giá trong phiên bản này.
                    </Text>
                  ) : (
                    rules.map((r) => (
                      <View key={r.id} style={styles.ruleRow}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.ruleTitle}>
                            {r.code} • {r.name}
                          </Text>
                          <Text style={styles.metaText}>
                            Cách tính: {r.calcMethod}
                            {r.minAmount
                              ? ` • Min: ${formatMoney(
                                  r.minAmount,
                                  r.currencyCode
                                )}`
                              : ""}
                          </Text>
                        </View>
                        <Text style={styles.ruleAmt}>
                          {formatMoney(r.unitAmount, r.currencyCode)}
                        </Text>
                      </View>
                    ))
                  )}
                </View>
              ) : null}
            </View>
          ) : null}

          {loading ? (
            <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
          ) : filteredCards.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>
                Không có bảng giá phù hợp bộ lọc
              </Text>
            </View>
          ) : (
            filteredCards.map((card) => (
              <TouchableOpacity
                key={card.id}
                style={styles.card}
                onPress={() => void loadCardVersions(card)}
              >
                <View style={styles.rowBetween}>
                  <Text style={styles.cardCode}>{card.code}</Text>
                  <View style={styles.badge}>
                    <Text style={styles.badgeText}>
                      {card.partyType.toLowerCase() === "vendor"
                        ? "Giá Mua (Cost)"
                        : "Giá Bán (Revenue)"}{" "}
                      • {card.currencyCode}
                    </Text>
                  </View>
                </View>
                <Text style={styles.cardTitle}>{card.name}</Text>
                <Text style={styles.metaText}>
                  Phương thức: {card.transportMode ?? "ALL"} • Tuyến:{" "}
                  {card.routeCode ?? "Toàn mạng"}
                  {card.carrierName ? ` • Hãng: ${card.carrierName}` : ""}
                </Text>
              </TouchableOpacity>
            ))
          )}
        </>
      ) : subTab === "compare" ? (
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>
            So sánh Báo giá & Tính cước Tự động theo Bill
          </Text>

          <Text style={styles.label}>Chọn Vận đơn (Bill) cần tính cước:</Text>
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            style={{ marginVertical: 6 }}
          >
            {bills.slice(0, 15).map((b) => (
              <TouchableOpacity
                key={b.id}
                style={[
                  styles.chip,
                  selectedBillId === b.id && styles.chipActive,
                ]}
                onPress={() => {
                  setSelectedBillId(b.id);
                  if (b.originCode) setCmpOrigin(b.originCode);
                  if (b.destinationCode) setCmpDest(b.destinationCode);
                }}
              >
                <Text
                  style={[
                    styles.chipText,
                    selectedBillId === b.id && styles.chipTextActive,
                  ]}
                >
                  {b.billNo} ({b.originCode ?? "SGN"}→{b.destinationCode ?? "HAN"})
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Điểm đi (Origin)</Text>
              <TextInput
                style={styles.input}
                value={cmpOrigin}
                onChangeText={setCmpOrigin}
                autoCapitalize="characters"
              />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Điểm đến (Destination)</Text>
              <TextInput
                style={styles.input}
                value={cmpDest}
                onChangeText={setCmpDest}
                autoCapitalize="characters"
              />
            </View>
          </View>

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>TL Tính cước / Gross (Kg)</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={cmpWeightKg}
                onChangeText={setCmpWeightKg}
              />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Thể tích (CBM)</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={cmpVolumeCbm}
                onChangeText={setCmpVolumeCbm}
              />
            </View>
          </View>

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Phương thức</Text>
              <View style={styles.chipRow}>
                {(["ROAD", "AIR", "SEA"] as const).map((m) => (
                  <TouchableOpacity
                    key={m}
                    style={[styles.chip, cmpMode === m && styles.chipActive]}
                    onPress={() => setCmpMode(m)}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        cmpMode === m && styles.chipTextActive,
                      ]}
                    >
                      {m}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Chiều biểu giá</Text>
              <View style={styles.chipRow}>
                {canViewCost ? (
                  <TouchableOpacity
                    style={[
                      styles.chip,
                      cmpPartyType === "vendor" && styles.chipActive,
                    ]}
                    onPress={() => setCmpPartyType("vendor")}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        cmpPartyType === "vendor" && styles.chipTextActive,
                      ]}
                    >
                      Mua
                    </Text>
                  </TouchableOpacity>
                ) : null}
                {canViewRevenue ? (
                  <TouchableOpacity
                    style={[
                      styles.chip,
                      cmpPartyType === "customer" && styles.chipActive,
                    ]}
                    onPress={() => setCmpPartyType("customer")}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        cmpPartyType === "customer" && styles.chipTextActive,
                      ]}
                    >
                      Bán
                    </Text>
                  </TouchableOpacity>
                ) : null}
              </View>
            </View>
          </View>

          <View style={[styles.twoCol, { marginTop: 10 }]}>
            <TouchableOpacity
              style={[styles.submitBtn, { flex: 1 }]}
              onPress={handleCompareRates}
              disabled={submitting}
            >
              <Text style={styles.submitBtnText}>
                🔍 So sánh các Bảng giá
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[
                styles.submitBtn,
                { flex: 1, backgroundColor: "#2563EB" },
              ]}
              onPress={() => void handleCheckReadinessAndRate()}
              disabled={submitting}
            >
              <Text style={styles.submitBtnText}>
                ⚡ Tính cước Tự động
              </Text>
            </TouchableOpacity>
          </View>

          {readinessMsg ? (
            <Text style={styles.readinessBanner}>{readinessMsg}</Text>
          ) : null}

          {quotes.length > 0 ? (
            <View style={{ marginTop: 14 }}>
              <Text style={styles.subHeader}>
                Kết quả So sánh Báo giá ({quotes.length} phương án):
              </Text>
              {quotes.map((q, idx) => (
                <View key={q.rateCardId ?? idx} style={styles.quoteRow}>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.cardCode}>
                      #{idx + 1} • {q.code ?? "RATE"} — {q.name ?? "Phương án giá"}
                    </Text>
                    <Text style={styles.metaText}>
                      Hãng: {q.carrierName ?? "Tiêu chuẩn"}
                    </Text>
                  </View>
                  <View style={{ alignItems: "flex-end" }}>
                    <Text style={styles.ruleAmt}>
                      {formatMoney(
                        q.totalAmount ?? q.amount ?? 0,
                        q.currency ?? q.currencyCode ?? "VND"
                      )}
                    </Text>
                    {q.rateVersionId ? (
                      <TouchableOpacity
                        style={styles.smallBtn}
                        onPress={() =>
                          void handleCheckReadinessAndRate(q.rateVersionId)
                        }
                      >
                        <Text style={styles.smallBtnText}>Áp dụng giá này</Text>
                      </TouchableOpacity>
                    ) : null}
                  </View>
                </View>
              ))}
            </View>
          ) : null}
        </View>
      ) : (
        <>
          <Text style={styles.subHeader}>
            Danh mục Phụ phí Chuẩn ({surcharges.length})
          </Text>
          {surcharges.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>Chưa có phụ phí nào</Text>
            </View>
          ) : (
            surcharges.map((s) => (
              <View key={s.id} style={styles.card}>
                <View style={styles.rowBetween}>
                  <Text style={styles.cardCode}>
                    {s.code} • {s.name}
                  </Text>
                  <Text style={styles.ruleAmt}>
                    {formatMoney(s.amount ?? 0, s.currencyCode ?? "VND")}
                  </Text>
                </View>
                <Text style={styles.metaText}>
                  Cách tính: {s.calcMethod ?? "FLAT"} • Phương thức:{" "}
                  {s.transportMode ?? "ALL"}
                </Text>
              </View>
            ))
          )}

          <Text style={[styles.subHeader, { marginTop: 16 }]}>
            Lịch sử Tính cước Tự động ({history.length})
          </Text>
          {history.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>Chưa có lịch sử tính giá</Text>
            </View>
          ) : (
            history.map((h) => (
              <View key={h.id} style={styles.card}>
                <View style={styles.rowBetween}>
                  <Text style={styles.cardCode}>
                    Bill: {h.billNo ?? h.billId?.slice(0, 8) ?? "—"}
                  </Text>
                  <Text style={styles.ruleAmt}>
                    {formatMoney(h.totalAmount ?? 0, h.currencyCode ?? "VND")}
                  </Text>
                </View>
                <Text style={styles.metaText}>
                  Bảng giá: {h.rateCardCode ?? "Auto"} • Lúc:{" "}
                  {h.calculatedAt ? h.calculatedAt.slice(0, 16) : "—"}
                </Text>
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
    fontSize: 11,
    fontWeight: "700",
    color: "#334155",
  },
  tabBtnTextActive: {
    color: "#FFFFFF",
  },
  searchRow: {
    flexDirection: "row",
    gap: 8,
    marginBottom: 8,
  },
  searchInput: {
    flex: 1,
    backgroundColor: "#FFFFFF",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 10,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 13,
    color: "#0F172A",
  },
  primaryBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 12,
    justifyContent: "center",
    borderRadius: 10,
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  chipRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginVertical: 4,
  },
  chip: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 6,
    backgroundColor: "#FFFFFF",
    marginRight: 4,
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
  formCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#BFDBFE",
    marginVertical: 10,
  },
  detailCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#93C5FD",
    marginVertical: 10,
  },
  ruleFormBox: {
    backgroundColor: "#F8FAFC",
    padding: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginVertical: 8,
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
    marginBottom: 6,
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
    fontSize: 14,
    fontWeight: "800",
    color: "#0F172A",
  },
  cardTitle: {
    fontSize: 13,
    fontWeight: "600",
    color: "#1E293B",
    marginTop: 4,
  },
  metaText: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 4,
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
  versionPill: {
    backgroundColor: "#F8FAFC",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 10,
    padding: 10,
    marginRight: 8,
    minWidth: 150,
  },
  versionPillActive: {
    borderColor: "#2563EB",
    backgroundColor: "#EFF6FF",
  },
  versionTitle: {
    fontSize: 12,
    fontWeight: "800",
    color: "#0F172A",
  },
  versionMeta: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 2,
  },
  publishBtn: {
    backgroundColor: "#0F766E",
    borderRadius: 6,
    paddingVertical: 4,
    paddingHorizontal: 8,
    marginTop: 6,
    alignSelf: "flex-start",
  },
  publishBtnText: {
    color: "#FFFFFF",
    fontSize: 10,
    fontWeight: "700",
  },
  ruleRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: "#F1F5F9",
  },
  ruleTitle: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  ruleAmt: {
    fontSize: 14,
    fontWeight: "800",
    color: "#0F766E",
  },
  quoteRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    padding: 10,
    backgroundColor: "#F8FAFC",
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 8,
  },
  readinessBanner: {
    marginTop: 10,
    padding: 10,
    borderRadius: 8,
    backgroundColor: "#F0FDF4",
    color: "#166534",
    fontSize: 12,
    fontWeight: "700",
  },
  smallBtn: {
    backgroundColor: "#EFF6FF",
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 6,
    marginTop: 4,
  },
  smallBtnText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#1D4ED8",
  },
  closeText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#64748B",
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

