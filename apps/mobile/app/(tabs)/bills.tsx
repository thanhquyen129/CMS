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
import { useRouter } from "expo-router";
import { BillListItemDto } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { enqueueOfflineMutation } from "../../src/offline/outbox";

interface OrderItemDto {
  id: string;
  orderNo: string;
  operationalStatus?: string;
  transportMode?: string | null;
  originCode?: string | null;
  destinationCode?: string | null;
  customerReference?: string | null;
}

interface ShipmentItemDto {
  id: string;
  shipmentNo: string;
  operationalStatus?: string;
  transportMode?: string | null;
  originCode?: string | null;
  destinationCode?: string | null;
}

interface MovementItemDto {
  id: string;
  movementNo?: string;
  legNo?: string;
  operationalStatus?: string;
  sourceSystem?: string;
}

export default function BillsScreen() {
  const router = useRouter();
  const { t } = useAuth();
  const [opTab, setOpTab] = useState<
    "bills" | "orders" | "shipments" | "movements"
  >("bills");
  const [bills, setBills] = useState<BillListItemDto[]>([]);
  const [orders, setOrders] = useState<OrderItemDto[]>([]);
  const [shipments, setShipments] = useState<ShipmentItemDto[]>([]);
  const [movements, setMovements] = useState<MovementItemDto[]>([]);
  const [search, setSearch] = useState<string>("");
  const [loading, setLoading] = useState<boolean>(true);

  // Create Bill / Waybill / Order / Shipment / Movement state
  const [showCreateForm, setShowCreateForm] = useState<boolean>(false);
  const [createMode, setCreateMode] = useState<"bill" | "waybill">("waybill");
  const [billNo, setBillNo] = useState<string>("");
  const [billType, setBillType] = useState<"AIR" | "SEA" | "ROAD">("ROAD");
  const [originCode, setOriginCode] = useState<string>("SGN");
  const [destinationCode, setDestinationCode] = useState<string>("HAN");
  const [senderName, setSenderName] = useState<string>("");
  const [consigneeName, setConsigneeName] = useState<string>("");
  const [parcelCount, setParcelCount] = useState<string>("1");
  const [actualWeightKg, setActualWeightKg] = useState<string>("");
  const [chargeableWeightKg, setChargeableWeightKg] = useState<string>("");
  const [basePostage, setBasePostage] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<"VND" | "USD">("VND");
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Quick Order / Shipment / Movement creation
  const [refCodeInput, setRefCodeInput] = useState<string>("");
  const [linkedBillId, setLinkedBillId] = useState<string>("");

  const loadBills = useCallback(async () => {
    setLoading(true);
    try {
      const [billRes, ordRes, shpRes, movRes] = await Promise.all([
        apiRequest<BillListItemDto[] | { items: BillListItemDto[] }>(
          "/api/bills"
        ).catch(() => [] as BillListItemDto[]),
        apiRequest<OrderItemDto[] | { items: OrderItemDto[] }>(
          "/api/orders"
        ).catch(() => [] as OrderItemDto[]),
        apiRequest<ShipmentItemDto[] | { items: ShipmentItemDto[] }>(
          "/api/shipments"
        ).catch(() => [] as ShipmentItemDto[]),
        apiRequest<MovementItemDto[] | { items: MovementItemDto[] }>(
          "/api/transport-movements"
        ).catch(() => [] as MovementItemDto[]),
      ]);

      const billList = Array.isArray(billRes) ? billRes : billRes?.items ?? [];
      setBills(billList);
      if (billList.length > 0 && !linkedBillId) {
        setLinkedBillId(billList[0].id);
      }
      setOrders(Array.isArray(ordRes) ? ordRes : ordRes?.items ?? []);
      setShipments(Array.isArray(shpRes) ? shpRes : shpRes?.items ?? []);
      setMovements(Array.isArray(movRes) ? movRes : movRes?.items ?? []);
    } finally {
      setLoading(false);
    }
  }, [linkedBillId]);

  useEffect(() => {
    void loadBills();
  }, [loadBills]);

  const buildCreatePayload = () => {
    const cleanBillNo =
      billNo.trim() ||
      `WB-${new Date().toISOString().slice(2, 10).replace(/-/g, "")}-${Math.floor(
        100 + Math.random() * 900
      )}`;

    if (createMode === "bill") {
      return {
        endpoint: "/api/bills",
        billNo: cleanBillNo,
        payload: {
          billNo: cleanBillNo,
          billType,
          sourceSystem: "LCMS_MOBILE",
        },
      };
    }

    const cw = Number(chargeableWeightKg) || Number(actualWeightKg) || 0;
    const gross = Number(actualWeightKg) || cw;
    const postage = Number(basePostage) || 0;

    return {
      endpoint: "/api/bills/waybills",
      billNo: cleanBillNo,
      payload: {
        billNo: cleanBillNo,
        billType,
        sourceSystem: "LCMS_MOBILE",
        carrierName: `LCMS ${billType}`,
        sender: senderName.trim() ? { name: senderName.trim() } : null,
        consignee: consigneeName.trim() ? { name: consigneeName.trim() } : null,
        parcelCount: Number(parcelCount) || 1,
        actualWeightKg: gross > 0 ? gross : null,
        chargeableWeightKg: cw > 0 ? cw : null,
        basePostage: postage > 0 ? postage : null,
        grandTotal: postage > 0 ? postage : null,
        currencyCode,
        senderCommitAccepted: true,
        operationsNote: `Tuyến ${originCode.trim() || "SGN"} -> ${
          destinationCode.trim() || "HAN"
        } (Mobile Capture)`,
      },
    };
  };

  const handleCreateBillOnline = async () => {
    const { endpoint, billNo: resolvedNo, payload } = buildCreatePayload();
    setSubmitting(true);
    try {
      const created = await apiRequest<{ id: string }>(endpoint, {
        method: "POST",
        body: payload,
      });

      if (created?.id && (originCode.trim() || destinationCode.trim())) {
        try {
          await apiRequest(`/api/bills/${created.id}/context`, {
            method: "PATCH",
            body: {
              transportMode: billType,
              originCode: originCode.trim() || "SGN",
              destinationCode: destinationCode.trim() || "HAN",
            },
          });
        } catch {
          // Best-effort context patch
        }
      }

      setShowCreateForm(false);
      setBillNo("");
      setActualWeightKg("");
      setChargeableWeightKg("");
      setBasePostage("");
      await loadBills();
      Alert.alert(
        "Đã tạo Vận đơn thành công",
        `Đã khởi tạo vận đơn ${resolvedNo} trên hệ thống LCMS.`
      );
    } catch (err) {
      Alert.alert(
        "Không thể gửi trực tuyến",
        `${
          err instanceof Error ? err.message : "Lỗi mạng."
        } Bạn có muốn lưu vào Hàng đợi Offline để đồng bộ sau không?`,
        [
          { text: "Đóng", style: "cancel" },
          {
            text: "Lưu Hàng đợi Offline",
            onPress: () => void handleQueueOffline(),
          },
        ]
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleQueueOffline = async () => {
    const { endpoint, billNo: resolvedNo, payload } = buildCreatePayload();
    await enqueueOfflineMutation({
      titleVi: `Tạo vận đơn ${resolvedNo} (${billType})`,
      endpoint,
      method: "POST",
      payload,
      idempotencyPrefix: "wb",
    });
    setShowCreateForm(false);
    setBillNo("");
    Alert.alert(
      "Đã lưu Ngoại tuyến",
      `Vận đơn ${resolvedNo} đã được lưu an toàn trên thiết bị và sẽ tự động đồng bộ khi có kết nối mạng.`
    );
  };

  const handleCreateUpperLevelRef = async (
    kind: "orders" | "shipments" | "movements"
  ) => {
    setSubmitting(true);
    try {
      const dateCode = new Date().toISOString().slice(2, 10).replace(/-/g, "");
      const rand = Math.floor(100 + Math.random() * 900);

      if (kind === "orders") {
        const code = refCodeInput.trim() || `ORD-${dateCode}-${rand}`;
        const res = await apiRequest<{ id: string }>("/api/orders", {
          method: "PUT",
          body: {
            orderNo: code,
            sourceSystem: "LCMS_MOBILE",
            externalId: code,
            operationalStatus: "in_transit",
            transportMode: billType,
            originCode: originCode.trim() || "SGN",
            destinationCode: destinationCode.trim() || "HAN",
          },
        });
        if (res?.id && linkedBillId) {
          await apiRequest(`/api/orders/${res.id}/bills/${linkedBillId}`, {
            method: "POST",
            body: {},
          }).catch(() => undefined);
        }
        Alert.alert("Đã tạo Đơn hàng", `Đã lưu Đơn hàng ${code} và gắn với Bill.`);
      } else if (kind === "shipments") {
        const code = refCodeInput.trim() || `SHP-${dateCode}-${rand}`;
        const res = await apiRequest<{ id: string }>("/api/shipments", {
          method: "PUT",
          body: {
            shipmentNo: code,
            sourceSystem: "LCMS_MOBILE",
            externalId: code,
            operationalStatus: "in_transit",
            transportMode: billType,
            originCode: originCode.trim() || "SGN",
            destinationCode: destinationCode.trim() || "HAN",
          },
        });
        if (res?.id && linkedBillId) {
          await apiRequest(`/api/shipments/${res.id}/bills/${linkedBillId}`, {
            method: "POST",
            body: {},
          }).catch(() => undefined);
        }
        Alert.alert("Đã tạo Lô hàng", `Đã lưu Lô hàng ${code} và gắn với Bill.`);
      } else {
        const code = refCodeInput.trim() || `MOV-${dateCode}-${rand}`;
        const res = await apiRequest<{ id: string }>("/api/transport-movements", {
          method: "PUT",
          body: {
            movementNo: code,
            sourceSystem: "LCMS_MOBILE",
            externalId: code,
            operationalStatus: "in_transit",
          },
        });
        if (res?.id && linkedBillId) {
          await apiRequest(
            `/api/transport-movements/${res.id}/bills/${linkedBillId}`,
            {
              method: "POST",
              body: {},
            }
          ).catch(() => undefined);
        }
        Alert.alert(
          "Đã tạo Chuyến vận chuyển",
          `Đã lưu Chuyến xe/tàu ${code} và gắn với Bill.`
        );
      }

      setRefCodeInput("");
      setShowCreateForm(false);
      await loadBills();
    } catch (err) {
      Alert.alert(
        "Lỗi tạo tham chiếu vận hành",
        err instanceof Error ? err.message : "Không thể lưu dữ liệu vận hành."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const filtered = bills.filter((b) => {
    if (!search.trim()) return true;
    const q = search.trim().toLowerCase();
    return (
      b.billNo.toLowerCase().includes(q) ||
      (b.masterBillNo ?? "").toLowerCase().includes(q) ||
      (b.customerReference ?? "").toLowerCase().includes(q)
    );
  });

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadBills()} />
      }
    >
      <View style={styles.opTabsRow}>
        <TouchableOpacity
          style={[styles.opTabBtn, opTab === "bills" && styles.opTabBtnActive]}
          onPress={() => setOpTab("bills")}
        >
          <Text
            style={[
              styles.opTabText,
              opTab === "bills" && styles.opTabTextActive,
            ]}
          >
            📦 Bill ({bills.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.opTabBtn, opTab === "orders" && styles.opTabBtnActive]}
          onPress={() => setOpTab("orders")}
        >
          <Text
            style={[
              styles.opTabText,
              opTab === "orders" && styles.opTabTextActive,
            ]}
          >
            📝 Đơn hàng ({orders.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.opTabBtn,
            opTab === "shipments" && styles.opTabBtnActive,
          ]}
          onPress={() => setOpTab("shipments")}
        >
          <Text
            style={[
              styles.opTabText,
              opTab === "shipments" && styles.opTabTextActive,
            ]}
          >
            🚢 Lô hàng ({shipments.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.opTabBtn,
            opTab === "movements" && styles.opTabBtnActive,
          ]}
          onPress={() => setOpTab("movements")}
        >
          <Text
            style={[
              styles.opTabText,
              opTab === "movements" && styles.opTabTextActive,
            ]}
          >
            🚛 Chuyến ({movements.length})
          </Text>
        </TouchableOpacity>
      </View>

      <View style={styles.searchBarRow}>
        <TextInput
          style={styles.searchInput}
          placeholder="Tìm theo Số Bill, HAWB, MAWB, Đơn hàng, Chuyến..."
          value={search}
          onChangeText={setSearch}
        />
        <TouchableOpacity
          style={styles.scanShortcutBtn}
          onPress={() => router.push("/(tabs)/scanner")}
        >
          <Text style={styles.scanShortcutText}>📷 Quét</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.createBtn}
          onPress={() => setShowCreateForm((v) => !v)}
        >
          <Text style={styles.createBtnText}>
            {showCreateForm
              ? "✕ Đóng"
              : opTab === "bills"
              ? "+ Tạo Bill"
              : "+ Tạo Mới"}
          </Text>
        </TouchableOpacity>
      </View>

      {showCreateForm && opTab !== "bills" ? (
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>
            {opTab === "orders"
              ? "Khởi tạo Đơn hàng (Order) & Gắn Vận đơn"
              : opTab === "shipments"
              ? "Khởi tạo Lô hàng tổng (Shipment) & Gắn Vận đơn"
              : "Khởi tạo Chuyến xe / Tàu (Movement) & Gắn Vận đơn"}
          </Text>
          <Text style={styles.label}>Mã định danh (Để trống tự sinh mã):</Text>
          <TextInput
            style={styles.input}
            value={refCodeInput}
            onChangeText={setRefCodeInput}
            placeholder="VD: ORD-2026-001 / SHP-2026-001 / MOV-51C..."
            autoCapitalize="characters"
          />

          <Text style={styles.label}>Liên kết với Vận đơn (Bill):</Text>
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            style={{ marginVertical: 6 }}
          >
            {bills.slice(0, 12).map((b) => (
              <TouchableOpacity
                key={b.id}
                style={[
                  styles.chip,
                  linkedBillId === b.id && styles.chipActive,
                ]}
                onPress={() => setLinkedBillId(b.id)}
              >
                <Text
                  style={[
                    styles.chipText,
                    linkedBillId === b.id && styles.chipTextActive,
                  ]}
                >
                  {b.billNo}
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>

          <TouchableOpacity
            style={[styles.primaryBtn, { marginTop: 8 }]}
            onPress={() => void handleCreateUpperLevelRef(opTab)}
            disabled={submitting}
          >
            <Text style={styles.primaryBtnText}>
              ✓ Lưu &amp; Liên kết Đồ thị Vận hành
            </Text>
          </TouchableOpacity>
        </View>
      ) : null}

      {showCreateForm && opTab === "bills" ? (
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>
            Lập Vận đơn / Phiếu gửi Hiện trường (Waybill Capture)
          </Text>

          <View style={styles.chipRow}>
            <TouchableOpacity
              style={[
                styles.chip,
                createMode === "waybill" && styles.chipActive,
              ]}
              onPress={() => setCreateMode("waybill")}
            >
              <Text
                style={[
                  styles.chipText,
                  createMode === "waybill" && styles.chipTextActive,
                ]}
              >
                Phiếu gửi Waybill (Đầy đủ CW & Cước)
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.chip, createMode === "bill" && styles.chipActive]}
              onPress={() => setCreateMode("bill")}
            >
              <Text
                style={[
                  styles.chipText,
                  createMode === "bill" && styles.chipTextActive,
                ]}
              >
                Vận đơn nhanh (Bill)
              </Text>
            </TouchableOpacity>
          </View>

          <Text style={styles.label}>Mã Vận đơn (Để trống sẽ tự sinh mã WB-...)</Text>
          <TextInput
            style={styles.input}
            placeholder="VD: HAWB-2026-088"
            value={billNo}
            onChangeText={setBillNo}
            autoCapitalize="characters"
          />

          <Text style={styles.label}>Phương thức vận tải</Text>
          <View style={styles.chipRow}>
            {(["ROAD", "AIR", "SEA"] as const).map((m) => (
              <TouchableOpacity
                key={m}
                style={[styles.chip, billType === m && styles.chipActive]}
                onPress={() => setBillType(m)}
              >
                <Text
                  style={[
                    styles.chipText,
                    billType === m && styles.chipTextActive,
                  ]}
                >
                  {m === "ROAD"
                    ? "🚚 Đường bộ (ROAD)"
                    : m === "AIR"
                    ? "✈️ Hàng không (AIR)"
                    : "🚢 Đường biển (SEA)"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Điểm đi (Origin)</Text>
              <TextInput
                style={styles.input}
                value={originCode}
                onChangeText={setOriginCode}
                autoCapitalize="characters"
                placeholder="SGN"
              />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Điểm đến (Destination)</Text>
              <TextInput
                style={styles.input}
                value={destinationCode}
                onChangeText={setDestinationCode}
                autoCapitalize="characters"
                placeholder="HAN"
              />
            </View>
          </View>

          {createMode === "waybill" ? (
            <>
              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Người gửi (Sender)</Text>
                  <TextInput
                    style={styles.input}
                    value={senderName}
                    onChangeText={setSenderName}
                    placeholder="Tên công ty/người gửi"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Người nhận (Consignee)</Text>
                  <TextInput
                    style={styles.input}
                    value={consigneeName}
                    onChangeText={setConsigneeName}
                    placeholder="Tên người nhận"
                  />
                </View>
              </View>

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>TL Thực tế (Gross Kg)</Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    value={actualWeightKg}
                    onChangeText={setActualWeightKg}
                    placeholder="VD: 120"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>TL Tính cước (CW Kg)</Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    value={chargeableWeightKg}
                    onChangeText={setChargeableWeightKg}
                    placeholder="VD: 150"
                  />
                </View>
              </View>

              <View style={styles.twoCol}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Cước dự kiến</Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    value={basePostage}
                    onChangeText={setBasePostage}
                    placeholder="VD: 2500000"
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.label}>Loại tiền</Text>
                  <View style={styles.chipRow}>
                    {(["VND", "USD"] as const).map((cur) => (
                      <TouchableOpacity
                        key={cur}
                        style={[
                          styles.chip,
                          currencyCode === cur && styles.chipActive,
                        ]}
                        onPress={() => setCurrencyCode(cur)}
                      >
                        <Text
                          style={[
                            styles.chipText,
                            currencyCode === cur && styles.chipTextActive,
                          ]}
                        >
                          {cur}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
              </View>
            </>
          ) : null}

          <View style={styles.actionRow}>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={handleCreateBillOnline}
              disabled={submitting}
            >
              <Text style={styles.primaryBtnText}>
                {submitting ? "Đang tạo..." : "✓ Tạo Vận đơn Trực tuyến"}
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.offlineBtn}
              onPress={handleQueueOffline}
              disabled={submitting}
            >
              <Text style={styles.offlineBtnText}>📡 Lưu Offline</Text>
            </TouchableOpacity>
          </View>
        </View>
      ) : null}

      {opTab === "bills" ? (
        <>
          <Text style={styles.countText}>
            Danh sách Vận đơn ({filtered.length}) • Nhấn vào Bill để xem Chi phí/Doanh thu, CW, Đồ thị Vận hành & Chứng từ
          </Text>

          {loading ? (
            <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
          ) : filtered.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>Không tìm thấy Vận đơn phù hợp</Text>
            </View>
          ) : (
            filtered.map((bill) => (
              <TouchableOpacity
                key={bill.id}
                style={styles.card}
                onPress={() => router.push(`/bills/${bill.id}`)}
              >
                <View style={styles.rowBetween}>
                  <Text style={styles.billNo}>{bill.billNo}</Text>
                  <View style={styles.statusBadge}>
                    <Text style={styles.statusText}>
                      {t(`bill.${bill.operationalStatus}`, bill.operationalStatus)}
                    </Text>
                  </View>
                </View>

                <Text style={styles.routeText}>
                  Tuyến: {bill.originCode ?? "—"} → {bill.destinationCode ?? "—"} •{" "}
                  Phương thức: {(bill.transportMode ?? bill.billType).toUpperCase()}
                </Text>

                {bill.masterBillNo || bill.customerReference ? (
                  <Text style={styles.metaText}>
                    {bill.masterBillNo ? `MAWB: ${bill.masterBillNo} ` : ""}
                    {bill.customerReference ? `• Ref: ${bill.customerReference}` : ""}
                  </Text>
                ) : null}
              </TouchableOpacity>
            ))
          )}
        </>
      ) : opTab === "orders" ? (
        orders.length === 0 ? (
          <View style={styles.emptyCard}>
            <Text style={styles.emptyTitle}>Chưa có Đơn hàng (Orders)</Text>
          </View>
        ) : (
          orders.map((ord) => (
            <View key={ord.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.billNo}>{ord.orderNo}</Text>
                <View style={styles.statusBadge}>
                  <Text style={styles.statusText}>
                    {ord.operationalStatus ?? "active"}
                  </Text>
                </View>
              </View>
              <Text style={styles.routeText}>
                Tuyến: {ord.originCode ?? "SGN"} → {ord.destinationCode ?? "HAN"} •{" "}
                {ord.transportMode ?? "ROAD"}
              </Text>
            </View>
          ))
        )
      ) : opTab === "shipments" ? (
        shipments.length === 0 ? (
          <View style={styles.emptyCard}>
            <Text style={styles.emptyTitle}>Chưa có Lô hàng tổng (Shipments)</Text>
          </View>
        ) : (
          shipments.map((shp) => (
            <View key={shp.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.billNo}>{shp.shipmentNo}</Text>
                <View style={styles.statusBadge}>
                  <Text style={styles.statusText}>
                    {shp.operationalStatus ?? "in_transit"}
                  </Text>
                </View>
              </View>
              <Text style={styles.routeText}>
                Tuyến: {shp.originCode ?? "SGN"} → {shp.destinationCode ?? "HAN"} •{" "}
                {shp.transportMode ?? "ROAD"}
              </Text>
            </View>
          ))
        )
      ) : movements.length === 0 ? (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyTitle}>
            Chưa có Chuyến vận chuyển (Movements)
          </Text>
        </View>
      ) : (
        movements.map((mov) => (
          <View key={mov.id} style={styles.card}>
            <View style={styles.rowBetween}>
              <Text style={styles.billNo}>
                {mov.movementNo ?? mov.legNo ?? mov.id.slice(0, 8)}
              </Text>
              <View style={styles.statusBadge}>
                <Text style={styles.statusText}>
                  {mov.operationalStatus ?? "in_transit"}
                </Text>
              </View>
            </View>
            <Text style={styles.metaText}>
              Nguồn dữ liệu: {mov.sourceSystem ?? "LCMS"}
            </Text>
          </View>
        ))
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
  opTabsRow: {
    flexDirection: "row",
    gap: 5,
    marginBottom: 10,
  },
  opTabBtn: {
    flex: 1,
    paddingVertical: 8,
    borderRadius: 8,
    backgroundColor: "#E2E8F0",
    alignItems: "center",
  },
  opTabBtnActive: {
    backgroundColor: "#0F172A",
  },
  opTabText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#334155",
  },
  opTabTextActive: {
    color: "#FFFFFF",
  },
  searchBarRow: {
    flexDirection: "row",
    gap: 6,
    marginBottom: 10,
  },
  searchInput: {
    flex: 1,
    backgroundColor: "#FFFFFF",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 10,
    paddingHorizontal: 10,
    paddingVertical: 10,
    fontSize: 13,
    color: "#0F172A",
  },
  scanShortcutBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 10,
    justifyContent: "center",
    borderRadius: 10,
  },
  scanShortcutText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  createBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 12,
    justifyContent: "center",
    borderRadius: 10,
  },
  createBtnText: {
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
  formTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 10,
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
    backgroundColor: "#F8FAFC",
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
  actionRow: {
    flexDirection: "row",
    gap: 8,
    marginTop: 12,
  },
  primaryBtn: {
    flex: 1,
    backgroundColor: "#0F172A",
    paddingVertical: 11,
    borderRadius: 8,
    alignItems: "center",
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  offlineBtn: {
    backgroundColor: "#EFF6FF",
    borderWidth: 1,
    borderColor: "#93C5FD",
    paddingHorizontal: 12,
    paddingVertical: 11,
    borderRadius: 8,
    justifyContent: "center",
  },
  offlineBtnText: {
    color: "#1D4ED8",
    fontSize: 12,
    fontWeight: "700",
  },
  countText: {
    fontSize: 12,
    color: "#64748B",
    marginBottom: 10,
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
  billNo: {
    fontSize: 16,
    fontWeight: "800",
    color: "#0F172A",
  },
  statusBadge: {
    backgroundColor: "#EFF6FF",
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  statusText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#1D4ED8",
  },
  routeText: {
    fontSize: 13,
    color: "#334155",
    marginTop: 6,
  },
  metaText: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 4,
  },
});
