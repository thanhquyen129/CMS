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
import { formatMoney } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";

interface BusinessPartyDto {
  id: string;
  code: string;
  name: string;
  legalName?: string | null;
  taxId?: string | null;
  phone?: string | null;
  email?: string | null;
  defaultCurrencyCode?: string | null;
  paymentTermDays?: number | null;
  creditLimit?: number | null;
  creditLimitCurrencyCode?: string | null;
  status?: string | null;
  isBlocked?: boolean;
  roleCodes?: string[];
}

interface PartyFinancialDto {
  creditLimit?: number | null;
  creditLimitCurrencyCode?: string | null;
  currentApBalance?: number;
  currentArBalance?: number;
  exposureAmount?: number;
  currencyCode?: string;
}

interface FxRateDto {
  id: string;
  fromCurrencyCode: string;
  toCurrencyCode: string;
  rateDate: string;
  rate: number;
  source?: string | null;
  note?: string | null;
}

interface FxExceptionDto {
  id: string;
  objectType?: string;
  objectId?: string;
  fromCurrencyCode?: string;
  toCurrencyCode?: string;
  status?: string;
  message?: string;
}

interface LocationDto {
  id: string;
  code: string;
  name: string;
  locationType?: string | null;
  countryCode?: string | null;
  city?: string | null;
  iataCode?: string | null;
  unlocode?: string | null;
  isActive?: boolean;
}

interface RouteDto {
  id: string;
  code: string;
  name: string;
  transportModeCode?: string | null;
  originCode?: string | null;
  destinationCode?: string | null;
  isActive?: boolean;
}

export default function MasterDataModuleScreen() {
  const [subTab, setSubTab] = useState<"parties" | "fx" | "locations">(
    "parties"
  );
  const [loading, setLoading] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Parties state
  const [parties, setParties] = useState<BusinessPartyDto[]>([]);
  const [partySearch, setPartySearch] = useState<string>("");
  const [roleFilter, setRoleFilter] = useState<
    "ALL" | "CUSTOMER" | "VENDOR" | "CARRIER"
  >("ALL");
  const [selectedParty, setSelectedParty] = useState<BusinessPartyDto | null>(
    null
  );
  const [partyFin, setPartyFin] = useState<PartyFinancialDto | null>(null);
  const [blockReason, setBlockReason] = useState<string>("");

  // Create Party form
  const [showCreateParty, setShowCreateParty] = useState<boolean>(false);
  const [newPartyCode, setNewPartyCode] = useState<string>("");
  const [newPartyName, setNewPartyName] = useState<string>("");
  const [newPartyTaxId, setNewPartyTaxId] = useState<string>("");
  const [newPartyRole, setNewPartyRole] = useState<
    "CUSTOMER" | "VENDOR" | "CARRIER"
  >("CUSTOMER");
  const [newPartyCredit, setNewPartyCredit] = useState<string>("");

  // FX Rates state
  const [fxRates, setFxRates] = useState<FxRateDto[]>([]);
  const [fxExceptions, setFxExceptions] = useState<FxExceptionDto[]>([]);
  const [showCreateFx, setShowCreateFx] = useState<boolean>(false);
  const [fxFrom, setFxFrom] = useState<string>("USD");
  const [fxTo, setFxTo] = useState<string>("VND");
  const [fxRateVal, setFxRateVal] = useState<string>("25450");

  // Locations & Routes state
  const [locations, setLocations] = useState<LocationDto[]>([]);
  const [routes, setRoutes] = useState<RouteDto[]>([]);
  const [showCreateLoc, setShowCreateLoc] = useState<boolean>(false);
  const [locCode, setLocCode] = useState<string>("");
  const [locName, setLocName] = useState<string>("");
  const [locType, setLocType] = useState<"PORT" | "AIRPORT" | "HUB" | "ICD">(
    "HUB"
  );
  const [locCountry, setLocCountry] = useState<string>("VN");

  const loadAll = useCallback(async () => {
    setLoading(true);
    try {
      const [partyRes, fxRes, fxExcRes, locRes, routeRes] = await Promise.all([
        apiRequest<BusinessPartyDto[] | { items: BusinessPartyDto[] }>(
          "/api/business-parties/directory"
        ).catch(() => [] as BusinessPartyDto[]),
        apiRequest<FxRateDto[] | { items: FxRateDto[] }>("/api/fx-rates").catch(
          () => [] as FxRateDto[]
        ),
        apiRequest<FxExceptionDto[] | { items: FxExceptionDto[] }>(
          "/api/fx-rates/exceptions"
        ).catch(() => [] as FxExceptionDto[]),
        apiRequest<LocationDto[] | { items: LocationDto[] }>(
          "/api/locations"
        ).catch(() => [] as LocationDto[]),
        apiRequest<RouteDto[] | { items: RouteDto[] }>("/api/routes").catch(
          () => [] as RouteDto[]
        ),
      ]);

      setParties(Array.isArray(partyRes) ? partyRes : partyRes?.items ?? []);
      setFxRates(Array.isArray(fxRes) ? fxRes : fxRes?.items ?? []);
      setFxExceptions(
        Array.isArray(fxExcRes) ? fxExcRes : fxExcRes?.items ?? []
      );
      setLocations(Array.isArray(locRes) ? locRes : locRes?.items ?? []);
      setRoutes(Array.isArray(routeRes) ? routeRes : routeRes?.items ?? []);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const handleSelectParty = async (party: BusinessPartyDto) => {
    setSelectedParty(party);
    setPartyFin(null);
    try {
      const fin = await apiRequest<PartyFinancialDto>(
        `/api/business-parties/${party.id}/financial`
      );
      setPartyFin(fin);
    } catch {
      setPartyFin(null);
    }
  };

  const handleCreateParty = async () => {
    if (!newPartyName.trim()) {
      Alert.alert("Thiếu tên đối tác", "Vui lòng nhập Tên đối tác.");
      return;
    }
    setSubmitting(true);
    try {
      const code =
        newPartyCode.trim() ||
        `PT-${newPartyRole.slice(0, 3)}-${Math.floor(100 + Math.random() * 900)}`;
      await apiRequest("/api/business-parties", {
        method: "POST",
        body: {
          code,
          name: newPartyName.trim(),
          legalName: newPartyName.trim(),
          taxId: newPartyTaxId.trim() || null,
          countryCode: "VN",
          defaultCurrencyCode: "VND",
          paymentTermDays: 30,
          creditLimit: Number(newPartyCredit) > 0 ? Number(newPartyCredit) : null,
          creditLimitCurrencyCode: "VND",
          roleCodes: [newPartyRole],
          partyKind: "ORGANIZATION",
        },
      });
      setShowCreateParty(false);
      setNewPartyCode("");
      setNewPartyName("");
      setNewPartyTaxId("");
      setNewPartyCredit("");
      await loadAll();
      Alert.alert("Thành công", `Đã tạo đối tác ${code} (${newPartyRole}).`);
    } catch (err) {
      Alert.alert(
        "Lỗi tạo đối tác",
        err instanceof Error ? err.message : "Không thể tạo đối tác."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggleBlockParty = async (
    party: BusinessPartyDto,
    action: "block" | "unblock"
  ) => {
    if (action === "block" && !blockReason.trim()) {
      Alert.alert(
        "Thiếu lý do khóa đối tác",
        "Vui lòng nhập lý do khóa giao dịch đối tác (VD: Quá hạn tín dụng)."
      );
      return;
    }
    setSubmitting(true);
    try {
      await apiRequest(`/api/business-parties/${party.id}/${action}`, {
        method: "POST",
        body: { reason: blockReason.trim() || "Mở khóa giao dịch trên Mobile" },
      });
      setBlockReason("");
      setSelectedParty(null);
      await loadAll();
      Alert.alert(
        "Đã cập nhật",
        action === "block"
          ? "Đã khóa giao dịch đối tác."
          : "Đã mở khóa giao dịch đối tác."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi thao tác đối tác",
        err instanceof Error ? err.message : "Không thể cập nhật trạng thái đối tác."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleSyncVcbFx = async () => {
    setSubmitting(true);
    try {
      const res = await apiRequest<{
        updatedCount?: number;
        date?: string;
        status?: string;
      }>("/api/fx-rates/sync-vcb", {
        method: "POST",
        body: {},
      });
      await loadAll();
      Alert.alert(
        "Đồng bộ Vietcombank thành công",
        `Đã cập nhật ${res?.updatedCount ?? "các"} cặp tỷ giá niêm yết ngày ${
          res?.date ?? new Date().toISOString().slice(0, 10)
        }.`
      );
    } catch (err) {
      Alert.alert(
        "Lỗi đồng bộ tỷ giá VCB",
        err instanceof Error ? err.message : "Không thể kết nối nguồn tỷ giá VCB."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateFxRate = async () => {
    const rate = Number(fxRateVal);
    if (!rate || rate <= 0) {
      Alert.alert("Tỷ giá không hợp lệ", "Vui lòng nhập tỷ giá > 0.");
      return;
    }
    setSubmitting(true);
    try {
      await apiRequest("/api/fx-rates", {
        method: "POST",
        body: {
          fromCurrencyCode: fxFrom.trim().toUpperCase() || "USD",
          toCurrencyCode: fxTo.trim().toUpperCase() || "VND",
          rateDate: new Date().toISOString().slice(0, 10),
          rate,
          source: "MOBILE_MANUAL",
          version: 1,
          note: "Cập nhật tỷ giá từ ứng dụng di động",
        },
      });
      setShowCreateFx(false);
      await loadAll();
      Alert.alert("Đã lưu tỷ giá", `Đã cập nhật tỷ giá ${fxFrom} → ${fxTo}.`);
    } catch (err) {
      Alert.alert(
        "Lỗi lưu tỷ giá",
        err instanceof Error ? err.message : "Không thể lưu tỷ giá."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateLocation = async () => {
    if (!locCode.trim() || !locName.trim()) {
      Alert.alert("Thiếu thông tin", "Vui lòng nhập Mã địa điểm và Tên địa điểm.");
      return;
    }
    setSubmitting(true);
    try {
      await apiRequest("/api/locations", {
        method: "PUT",
        body: {
          code: locCode.trim().toUpperCase(),
          name: locName.trim(),
          locationType: locType,
          countryCode: locCountry.trim().toUpperCase() || "VN",
          isActive: true,
        },
      });
      setShowCreateLoc(false);
      setLocCode("");
      setLocName("");
      await loadAll();
      Alert.alert("Thành công", "Đã cập nhật danh mục địa điểm vận hành.");
    } catch (err) {
      Alert.alert(
        "Lỗi lưu địa điểm",
        err instanceof Error ? err.message : "Không thể lưu địa điểm."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const filteredParties = parties.filter((p) => {
    if (
      roleFilter !== "ALL" &&
      !(p.roleCodes ?? []).some((r) => r.toUpperCase().includes(roleFilter))
    ) {
      return false;
    }
    if (!partySearch.trim()) return true;
    const q = partySearch.trim().toLowerCase();
    return (
      p.code.toLowerCase().includes(q) ||
      p.name.toLowerCase().includes(q) ||
      (p.taxId ?? "").toLowerCase().includes(q)
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
          style={[styles.tabBtn, subTab === "parties" && styles.tabBtnActive]}
          onPress={() => setSubTab("parties")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "parties" && styles.tabBtnTextActive,
            ]}
          >
            🏢 Đối tác ({filteredParties.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "fx" && styles.tabBtnActive]}
          onPress={() => setSubTab("fx")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "fx" && styles.tabBtnTextActive,
            ]}
          >
            💱 Tỷ giá VCB ({fxRates.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "locations" && styles.tabBtnActive]}
          onPress={() => setSubTab("locations")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "locations" && styles.tabBtnTextActive,
            ]}
          >
            📍 Cảng & Tuyến ({locations.length})
          </Text>
        </TouchableOpacity>
      </View>

      {subTab === "parties" ? (
        <>
          <View style={styles.searchRow}>
            <TextInput
              style={styles.searchInput}
              placeholder="Tìm mã đối tác, tên công ty, mã số thuế..."
              value={partySearch}
              onChangeText={setPartySearch}
            />
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => setShowCreateParty((v) => !v)}
            >
              <Text style={styles.primaryBtnText}>
                {showCreateParty ? "✕ Đóng" : "+ Tạo Đối tác"}
              </Text>
            </TouchableOpacity>
          </View>

          <View style={styles.chipRow}>
            {(["ALL", "CUSTOMER", "VENDOR", "CARRIER"] as const).map((r) => (
              <TouchableOpacity
                key={r}
                style={[styles.chip, roleFilter === r && styles.chipActive]}
                onPress={() => setRoleFilter(r)}
              >
                <Text
                  style={[
                    styles.chipText,
                    roleFilter === r && styles.chipTextActive,
                  ]}
                >
                  {r === "ALL"
                    ? "Tất cả"
                    : r === "CUSTOMER"
                    ? "Khách hàng"
                    : r === "VENDOR"
                    ? "Nhà cung cấp"
                    : "Hãng vận tải"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {showCreateParty ? (
            <View style={styles.formCard}>
              <Text style={styles.formTitle}>Thêm Đối tác Kinh doanh Mới</Text>
              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã đối tác</Text>
                  <TextInput
                    style={styles.input}
                    value={newPartyCode}
                    onChangeText={setNewPartyCode}
                    placeholder="VD: KH-SAMSUNG"
                    autoCapitalize="characters"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã số thuế (Tax ID)</Text>
                  <TextInput
                    style={styles.input}
                    value={newPartyTaxId}
                    onChangeText={setNewPartyTaxId}
                    placeholder="010..."
                  />
                </View>
              </View>

              <Text style={styles.label}>Tên pháp nhân / Công ty</Text>
              <TextInput
                style={styles.input}
                value={newPartyName}
                onChangeText={setNewPartyName}
                placeholder="Công ty TNHH..."
              />

              <Text style={styles.label}>Vai trò giao dịch</Text>
              <View style={styles.chipRow}>
                {(["CUSTOMER", "VENDOR", "CARRIER"] as const).map((r) => (
                  <TouchableOpacity
                    key={r}
                    style={[
                      styles.chip,
                      newPartyRole === r && styles.chipActive,
                    ]}
                    onPress={() => setNewPartyRole(r)}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        newPartyRole === r && styles.chipTextActive,
                      ]}
                    >
                      {r}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>

              <Text style={styles.label}>Hạn mức tín dụng (VND)</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={newPartyCredit}
                onChangeText={setNewPartyCredit}
                placeholder="VD: 500000000"
              />

              <TouchableOpacity
                style={styles.submitBtn}
                onPress={handleCreateParty}
                disabled={submitting}
              >
                <Text style={styles.submitBtnText}>+ Lưu Đối tác Mới</Text>
              </TouchableOpacity>
            </View>
          ) : null}

          {selectedParty ? (
            <View style={styles.detailCard}>
              <View style={styles.rowBetween}>
                <Text style={styles.formTitle}>
                  {selectedParty.code} • {selectedParty.name}
                </Text>
                <TouchableOpacity onPress={() => setSelectedParty(null)}>
                  <Text style={styles.closeLink}>Đóng ✕</Text>
                </TouchableOpacity>
              </View>
              <Text style={styles.metaText}>
                MST: {selectedParty.taxId ?? "—"} • Vai trò:{" "}
                {(selectedParty.roleCodes ?? []).join(", ") || "Đối tác"} • Kỳ
                hạn nợ: {selectedParty.paymentTermDays ?? 30} ngày
              </Text>

              {partyFin ? (
                <View style={styles.finBox}>
                  <Text style={styles.subHeader}>
                    Hồ sơ Tín dụng &amp; Dư nợ Hiện tại:
                  </Text>
                  <Text style={styles.metaText}>
                    • Hạn mức tín dụng:{" "}
                    {formatMoney(
                      partyFin.creditLimit ?? selectedParty.creditLimit ?? 0,
                      partyFin.creditLimitCurrencyCode ?? "VND"
                    )}
                  </Text>
                  <Text style={styles.metaText}>
                    • Dư nợ Phải trả (AP):{" "}
                    {formatMoney(
                      partyFin.currentApBalance ?? 0,
                      partyFin.currencyCode ?? "VND"
                    )}{" "}
                    | Dư nợ Phải thu (AR):{" "}
                    {formatMoney(
                      partyFin.currentArBalance ?? 0,
                      partyFin.currencyCode ?? "VND"
                    )}
                  </Text>
                </View>
              ) : null}

              <View style={{ marginTop: 10 }}>
                <TextInput
                  style={styles.input}
                  placeholder="Lý do khóa / mở khóa giao dịch đối tác..."
                  value={blockReason}
                  onChangeText={setBlockReason}
                />
                <View style={styles.twoCol}>
                  <TouchableOpacity
                    style={[
                      styles.submitBtn,
                      { flex: 1, backgroundColor: "#DC2626" },
                    ]}
                    onPress={() =>
                      handleToggleBlockParty(selectedParty, "block")
                    }
                    disabled={submitting}
                  >
                    <Text style={styles.submitBtnText}>🔒 Khóa Đối tác</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[
                      styles.submitBtn,
                      { flex: 1, backgroundColor: "#0F766E" },
                    ]}
                    onPress={() =>
                      handleToggleBlockParty(selectedParty, "unblock")
                    }
                    disabled={submitting}
                  >
                    <Text style={styles.submitBtnText}>🔓 Mở khóa</Text>
                  </TouchableOpacity>
                </View>
              </View>
            </View>
          ) : null}

          {loading ? (
            <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
          ) : (
            filteredParties.map((p) => (
              <TouchableOpacity
                key={p.id}
                style={styles.card}
                onPress={() => void handleSelectParty(p)}
              >
                <View style={styles.rowBetween}>
                  <Text style={styles.cardCode}>{p.code}</Text>
                  <View style={styles.badge}>
                    <Text style={styles.badgeText}>
                      {(p.roleCodes ?? []).join(", ") || p.status || "Active"}
                    </Text>
                  </View>
                </View>
                <Text style={styles.cardTitle}>{p.name}</Text>
                <Text style={styles.metaText}>
                  MST: {p.taxId ?? "—"} • Tiền tệ: {p.defaultCurrencyCode ?? "VND"}
                  {p.creditLimit
                    ? ` • Hạn mức: ${formatMoney(
                        p.creditLimit,
                        p.creditLimitCurrencyCode ?? "VND"
                      )}`
                    : ""}
                </Text>
              </TouchableOpacity>
            ))
          )}
        </>
      ) : subTab === "fx" ? (
        <>
          <View style={styles.vcbBanner}>
            <View style={{ flex: 1 }}>
              <Text style={styles.vcbTitle}>
                Đồng bộ Tỷ giá Ngoại tệ Vietcombank
              </Text>
              <Text style={styles.vcbSub}>
                Cập nhật tự động tỷ giá mua/bán chuyển khoản USD, EUR, JPY, CNY, SGD → VND
              </Text>
            </View>
            <TouchableOpacity
              style={styles.vcbBtn}
              onPress={handleSyncVcbFx}
              disabled={submitting}
            >
              <Text style={styles.vcbBtnText}>
                {submitting ? "Đang đồng bộ..." : "🔄 Đồng bộ VCB"}
              </Text>
            </TouchableOpacity>
          </View>

          <View style={styles.rowBetween}>
            <Text style={styles.subHeader}>
              Bảng Tỷ giá Quy đổi ({fxRates.length})
            </Text>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => setShowCreateFx((v) => !v)}
            >
              <Text style={styles.primaryBtnText}>
                {showCreateFx ? "✕ Đóng" : "+ Nhập Tỷ giá Tay"}
              </Text>
            </TouchableOpacity>
          </View>

          {showCreateFx ? (
            <View style={styles.formCard}>
              <Text style={styles.formTitle}>Thêm / Ghi đè Tỷ giá Quy đổi</Text>
              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Từ nguyên tệ</Text>
                  <TextInput
                    style={styles.input}
                    value={fxFrom}
                    onChangeText={setFxFrom}
                    autoCapitalize="characters"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Sang đồng tiền</Text>
                  <TextInput
                    style={styles.input}
                    value={fxTo}
                    onChangeText={setFxTo}
                    autoCapitalize="characters"
                  />
                </View>
              </View>
              <Text style={styles.label}>Tỷ giá quy đổi</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={fxRateVal}
                onChangeText={setFxRateVal}
              />
              <TouchableOpacity
                style={styles.submitBtn}
                onPress={handleCreateFxRate}
                disabled={submitting}
              >
                <Text style={styles.submitBtnText}>+ Lưu Tỷ giá</Text>
              </TouchableOpacity>
            </View>
          ) : null}

          {fxExceptions.length > 0 ? (
            <View style={[styles.card, { borderColor: "#FECACA" }]}>
              <Text style={[styles.subHeader, { color: "#DC2626" }]}>
                ⚠️ Cảnh báo Thiếu Tỷ giá ({fxExceptions.length}):
              </Text>
              {fxExceptions.slice(0, 5).map((ex) => (
                <Text key={ex.id} style={styles.metaText}>
                  • {ex.fromCurrencyCode} → {ex.toCurrencyCode}:{" "}
                  {ex.message ?? ex.status}
                </Text>
              ))}
            </View>
          ) : null}

          {fxRates.map((fx) => (
            <View key={fx.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.cardCode}>
                  1 {fx.fromCurrencyCode} ={" "}
                  {formatMoney(fx.rate, fx.toCurrencyCode)}
                </Text>
                <View style={styles.badge}>
                  <Text style={styles.badgeText}>{fx.source ?? "VCB"}</Text>
                </View>
              </View>
              <Text style={styles.metaText}>
                Ngày hiệu lực: {fx.rateDate ? fx.rateDate.slice(0, 10) : "—"}
                {fx.note ? ` • ${fx.note}` : ""}
              </Text>
            </View>
          ))}
        </>
      ) : (
        <>
          <View style={styles.rowBetween}>
            <Text style={styles.subHeader}>
              Danh mục Cảng, Sân bay, ICD &amp; Hub ({locations.length})
            </Text>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={() => setShowCreateLoc((v) => !v)}
            >
              <Text style={styles.primaryBtnText}>
                {showCreateLoc ? "✕ Đóng" : "+ Thêm Địa điểm"}
              </Text>
            </TouchableOpacity>
          </View>

          {showCreateLoc ? (
            <View style={styles.formCard}>
              <Text style={styles.formTitle}>Thêm Địa điểm Vận hành Mới</Text>
              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Mã địa điểm (IATA/UNLOCODE)</Text>
                  <TextInput
                    style={styles.input}
                    value={locCode}
                    onChangeText={setLocCode}
                    placeholder="VD: VNSGN, HAN"
                    autoCapitalize="characters"
                  />
                </View>
                <View style={{ width: 90 }}>
                  <Text style={styles.label}>Quốc gia</Text>
                  <TextInput
                    style={styles.input}
                    value={locCountry}
                    onChangeText={setLocCountry}
                    autoCapitalize="characters"
                  />
                </View>
              </View>
              <Text style={styles.label}>Tên Cảng / Sân bay / Kho Hub</Text>
              <TextInput
                style={styles.input}
                value={locName}
                onChangeText={setLocName}
                placeholder="VD: Cảng Cát Lái TP.HCM"
              />
              <View style={styles.chipRow}>
                {(["HUB", "PORT", "AIRPORT", "ICD"] as const).map((t) => (
                  <TouchableOpacity
                    key={t}
                    style={[styles.chip, locType === t && styles.chipActive]}
                    onPress={() => setLocType(t)}
                  >
                    <Text
                      style={[
                        styles.chipText,
                        locType === t && styles.chipTextActive,
                      ]}
                    >
                      {t}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
              <TouchableOpacity
                style={styles.submitBtn}
                onPress={handleCreateLocation}
                disabled={submitting}
              >
                <Text style={styles.submitBtnText}>+ Lưu Địa điểm</Text>
              </TouchableOpacity>
            </View>
          ) : null}

          {locations.map((loc) => (
            <View key={loc.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.cardCode}>
                  {loc.code} • {loc.name}
                </Text>
                <View style={styles.badge}>
                  <Text style={styles.badgeText}>
                    {loc.locationType ?? "HUB"}
                  </Text>
                </View>
              </View>
              <Text style={styles.metaText}>
                Quốc gia: {loc.countryCode ?? "VN"}
                {loc.unlocode ? ` • UN/LOCODE: ${loc.unlocode}` : ""}
                {loc.iataCode ? ` • IATA: ${loc.iataCode}` : ""}
              </Text>
            </View>
          ))}

          <Text style={[styles.subHeader, { marginTop: 14 }]}>
            Danh mục Tuyến đường Chuẩn ({routes.length})
          </Text>
          {routes.map((rt) => (
            <View key={rt.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.cardCode}>{rt.code}</Text>
                <Text style={styles.metaText}>
                  {rt.transportModeCode ?? "ROAD"}
                </Text>
              </View>
              <Text style={styles.cardTitle}>{rt.name}</Text>
            </View>
          ))}
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
    paddingVertical: 8,
    justifyContent: "center",
    borderRadius: 8,
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
    marginVertical: 6,
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
  finBox: {
    backgroundColor: "#F8FAFC",
    padding: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginTop: 8,
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
    marginVertical: 6,
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
  vcbBanner: {
    backgroundColor: "#0F766E",
    borderRadius: 12,
    padding: 14,
    flexDirection: "row",
    alignItems: "center",
    marginBottom: 12,
    gap: 10,
  },
  vcbTitle: {
    color: "#FFFFFF",
    fontSize: 14,
    fontWeight: "800",
  },
  vcbSub: {
    color: "#CCFBF1",
    fontSize: 11,
    marginTop: 2,
  },
  vcbBtn: {
    backgroundColor: "#FFFFFF",
    paddingHorizontal: 12,
    paddingVertical: 9,
    borderRadius: 8,
  },
  vcbBtnText: {
    color: "#0F766E",
    fontSize: 12,
    fontWeight: "800",
  },
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    marginBottom: 8,
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
    marginTop: 3,
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
    marginTop: 3,
  },
  closeLink: {
    fontSize: 12,
    fontWeight: "700",
    color: "#64748B",
  },
});

