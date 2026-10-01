import React, { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { useLocalSearchParams } from "expo-router";
import {
  BillListItemDto,
  createIdempotencyKey,
  formatMoney,
  OperationalMeasurementSummaryDto,
} from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { AttachmentGallery } from "../../src/components/AttachmentGallery";
import { enqueueOfflineMutation } from "../../src/offline/outbox";

interface BillFinancialProfileResponse {
  billId: string;
  currencyCode: string;
  costExpected?: number | null;
  costConfirmed?: number | null;
  costActual?: number | null;
  costBestAvailable?: number | null;
  revenueExpected?: number | null;
  revenueConfirmed?: number | null;
  revenueActual?: number | null;
  revenueBestAvailable?: number | null;
  profitBestAvailable?: number | null;
}

interface BillGraphNodeDto {
  id: string;
  orderNo?: string;
  shipmentNo?: string;
  legNo?: string;
  movementNo?: string;
  operationalStatus?: string;
  transportMode?: string;
}

interface BillGraphResponseDto {
  orders?: BillGraphNodeDto[];
  shipments?: BillGraphNodeDto[];
  legs?: BillGraphNodeDto[];
  movements?: BillGraphNodeDto[];
}

interface FinancialHistoryEventDto {
  id?: string;
  eventType?: string;
  category?: string;
  description?: string;
  amount?: number | null;
  currencyCode?: string | null;
  occurredAt?: string | null;
}

export default function BillDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { bootstrap } = useAuth();
  const [bill, setBill] = useState<BillListItemDto | null>(null);
  const [measurement, setMeasurement] =
    useState<OperationalMeasurementSummaryDto | null>(null);
  const [financialProfile, setFinancialProfile] =
    useState<BillFinancialProfileResponse | null>(null);
  const [graph, setGraph] = useState<BillGraphResponseDto | null>(null);
  const [finHistory, setFinHistory] = useState<FinancialHistoryEventDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);

  // CW & Measurement Edit State (ADR-0039)
  const [gwInput, setGwInput] = useState<string>("");
  const [cbmInput, setCbmInput] = useState<string>("");
  const [cwInput, setCwInput] = useState<string>("");
  const [overrideReason, setOverrideReason] = useState<string>("");

  const vis = bootstrap?.financialVisibility ?? {
    canViewCost: false,
    canViewRevenue: false,
    canViewMargin: false,
  };

  const loadBillWorkspace = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const b = await apiRequest<BillListItemDto>(`/api/bills/${id}`);
      setBill(b);

      try {
        const opRef = await apiRequest<{
          rowVersion?: string | null;
          chargeableWeight?: {
            value?: number | null;
            basis?: string | null;
            isConfirmed?: boolean;
          } | null;
          fields?: Array<{ code: string; value?: string | null }>;
        }>(`/api/operational-references/bill/${id}/edit`);
        const fieldMap = new Map(
          (opRef.fields ?? []).map((f) => [f.code, f.value ?? ""])
        );
        const gwVal = fieldMap.get("gross_weight_kg");
        const cbmVal = fieldMap.get("volume_cbm");
        const cwVal =
          opRef.chargeableWeight?.value ??
          (fieldMap.get("chargeable_weight")
            ? Number(fieldMap.get("chargeable_weight"))
            : null);

        setMeasurement({
          grossWeightKg: gwVal ? Number(gwVal) : null,
          volumeCbm: cbmVal ? Number(cbmVal) : null,
          chargeableWeightKg: cwVal,
          chargeableWeightBasis: opRef.chargeableWeight?.basis ?? null,
          isChargeableWeightConfirmed: Boolean(
            opRef.chargeableWeight?.isConfirmed
          ),
          rowVersion: opRef.rowVersion ?? b.rowVersion,
        });
        setGwInput(gwVal ?? "");
        setCbmInput(cbmVal ?? "");
        setCwInput(cwVal !== null && cwVal !== undefined ? String(cwVal) : "");
      } catch {
        // Optional operational reference
      }

      try {
        const g = await apiRequest<BillGraphResponseDto>(
          `/api/bills/${id}/graph`
        );
        setGraph(g);
      } catch {
        setGraph(null);
      }

      if (vis.canViewCost || vis.canViewRevenue) {
        try {
          const fp = await apiRequest<BillFinancialProfileResponse>(
            `/api/bills/${id}/financial-profile`
          );
          setFinancialProfile(fp);
        } catch {
          // Optional financial profile
        }

        try {
          const fh = await apiRequest<
            FinancialHistoryEventDto[] | { items?: FinancialHistoryEventDto[]; events?: FinancialHistoryEventDto[] }
          >(`/api/bills/${id}/financial-history`);
          setFinHistory(
            Array.isArray(fh) ? fh : fh?.items ?? fh?.events ?? []
          );
        } catch {
          setFinHistory([]);
        }
      }
    } catch (err) {
      Alert.alert(
        "Không tìm thấy Bill",
        err instanceof Error ? err.message : "Không thể tải thông tin vận đơn."
      );
    } finally {
      setLoading(false);
    }
  }, [id, vis.canViewCost, vis.canViewRevenue]);

  useEffect(() => {
    void loadBillWorkspace();
  }, [loadBillWorkspace]);

  const handleConfirmCwAndMeasurements = async () => {
    if (!id || !bill) return;
    const changes: Record<string, string | null> = {};
    if (gwInput.trim()) changes["gross_weight_kg"] = gwInput.trim();
    if (cbmInput.trim()) changes["volume_cbm"] = cbmInput.trim();
    if (cwInput.trim()) changes["chargeable_weight"] = cwInput.trim();

    const patchPayload = {
      changes,
      reason:
        overrideReason.trim() ||
        "Cập nhật thông số đo lường từ ứng dụng di động hiện trường",
      revertFields: [],
    };

    try {
      if (Object.keys(changes).length > 0) {
        await apiRequest(`/api/operational-references/bill/${id}`, {
          method: "PATCH",
          ifMatch: measurement?.rowVersion ?? bill.rowVersion,
          idempotencyKey: createIdempotencyKey("opref"),
          body: patchPayload,
        });
      }
      await apiRequest(
        `/api/operational-references/bill/${id}/chargeable-weight/confirm`,
        {
          method: "POST",
          idempotencyKey: createIdempotencyKey("cw-conf"),
          body: {},
        }
      );
      Alert.alert(
        "Đã xác nhận CW",
        "Trọng lượng tính cước (CW) đã được xác nhận và sẵn sàng cho tự động tính cước (Rating)."
      );
      await loadBillWorkspace();
    } catch {
      await enqueueOfflineMutation({
        titleVi: `Cập nhật & xác nhận CW (${cwInput} kg) cho Bill ${bill.billNo}`,
        endpoint: `/api/operational-references/bill/${id}`,
        method: "PATCH",
        payload: patchPayload,
        ifMatch: measurement?.rowVersion ?? bill.rowVersion,
        idempotencyPrefix: "cw",
      });
      Alert.alert(
        "Đã lưu Ngoại tuyến",
        "Cập nhật thông số đo lường & CW đã được lưu vào bộ nhớ máy và sẽ tự động đồng bộ khi có kết nối mạng."
      );
    }
  };

  const handleTriggerRerate = async () => {
    if (!id) return;
    try {
      await apiRequest(`/api/ratings/readiness`, {
        method: "POST",
        body: { billId: id },
      });
      Alert.alert(
        "Đủ điều kiện tính cước",
        "Vận đơn đã đầy đủ thông số vận hành và CW đã xác nhận để tính cước tự động."
      );
      await loadBillWorkspace();
    } catch (err) {
      Alert.alert(
        "Chưa đủ điều kiện tính cước",
        err instanceof Error
          ? err.message
          : "Vui lòng kiểm tra xác nhận CW và thông tin tuyến/đối tác."
      );
    }
  };

  if (loading || !bill) {
    return (
      <View style={styles.loadingWrap}>
        <ActivityIndicator size="large" color="#0F172A" />
      </View>
    );
  }

  const currency =
    financialProfile?.currencyCode ??
    bootstrap?.tenant.defaultCurrencyCode ??
    "VND";

  return (
    <ScrollView style={styles.container}>
      {/* Bill Header */}
      <View style={styles.headerCard}>
        <Text style={styles.billNo}>{bill.billNo}</Text>
        <Text style={styles.routeText}>
          {bill.originCode ?? "—"} → {bill.destinationCode ?? "—"} •{" "}
          {(bill.transportMode ?? bill.billType).toUpperCase()}
        </Text>
        {bill.masterBillNo ? (
          <Text style={styles.subText}>MAWB: {bill.masterBillNo}</Text>
        ) : null}
      </View>

      {/* SoD-Aware Financial Profile */}
      {(vis.canViewCost || vis.canViewRevenue) && (
        <View style={styles.sectionCard}>
          <Text style={styles.sectionTitle}>
            Hồ sơ Tài chính Bill (Phân tách Chi phí ≠ Doanh thu)
          </Text>

          {vis.canViewCost ? (
            <View style={styles.finBlock}>
              <Text style={styles.finSideTitle}>Chi phí (Cost):</Text>
              <Text style={styles.finRow}>
                • Dự kiến: {formatMoney(financialProfile?.costExpected, currency)} | Xác nhận:{" "}
                {formatMoney(financialProfile?.costConfirmed, currency)}
              </Text>
              <Text style={styles.finBest}>
                • Thực tế / Best Available:{" "}
                {formatMoney(
                  financialProfile?.costActual ?? financialProfile?.costBestAvailable,
                  currency
                )}
              </Text>
            </View>
          ) : null}

          {vis.canViewRevenue ? (
            <View style={styles.finBlock}>
              <Text style={styles.finSideTitle}>Doanh thu (Revenue):</Text>
              <Text style={styles.finRow}>
                • Dự kiến: {formatMoney(financialProfile?.revenueExpected, currency)} | Xác nhận:{" "}
                {formatMoney(financialProfile?.revenueConfirmed, currency)}
              </Text>
              <Text style={styles.finBest}>
                • Thực tế / Best Available:{" "}
                {formatMoney(
                  financialProfile?.revenueActual ??
                    financialProfile?.revenueBestAvailable,
                  currency
                )}
              </Text>
            </View>
          ) : null}

          {vis.canViewMargin ? (
            <View style={styles.marginBanner}>
              <Text style={styles.marginText}>
                Biên lợi nhuận (Margin):{" "}
                {formatMoney(financialProfile?.profitBestAvailable, currency)}
              </Text>
            </View>
          ) : null}
        </View>
      )}

      {/* Operational Measurements & CW Confirmation (ADR-0039) */}
      <View style={styles.sectionCard}>
        <View style={styles.rowBetween}>
          <Text style={styles.sectionTitle}>
            Thông số Đo lường &amp; Xác nhận Trọng lượng Tính cước (CW)
          </Text>
          <View
            style={[
              styles.cwBadge,
              measurement?.isChargeableWeightConfirmed
                ? styles.cwConfirmed
                : styles.cwPending,
            ]}
          >
            <Text style={styles.cwBadgeText}>
              {measurement?.isChargeableWeightConfirmed
                ? "CW Đã xác nhận"
                : "CW Chưa xác nhận"}
            </Text>
          </View>
        </View>

        <View style={styles.inputRow}>
          <View style={styles.inputCol}>
            <Text style={styles.inputLabel}>Gross Weight (kg)</Text>
            <TextInput
              style={styles.input}
              keyboardType="numeric"
              value={gwInput}
              onChangeText={setGwInput}
              placeholder="0.0"
            />
          </View>
          <View style={styles.inputCol}>
            <Text style={styles.inputLabel}>Thể tích (CBM)</Text>
            <TextInput
              style={styles.input}
              keyboardType="numeric"
              value={cbmInput}
              onChangeText={setCbmInput}
              placeholder="0.0"
            />
          </View>
          <View style={styles.inputCol}>
            <Text style={styles.inputLabel}>CW Tính cước (kg)</Text>
            <TextInput
              style={styles.input}
              keyboardType="numeric"
              value={cwInput}
              onChangeText={setCwInput}
              placeholder="0.0"
            />
          </View>
        </View>

        <TextInput
          style={styles.input}
          placeholder="Nhập lý do cập nhật hoặc điều chỉnh thông số vận hành..."
          value={overrideReason}
          onChangeText={setOverrideReason}
        />

        <View style={styles.btnRow}>
          <TouchableOpacity
            style={styles.primaryBtn}
            onPress={handleConfirmCwAndMeasurements}
          >
            <Text style={styles.primaryBtnText}>✓ Lưu & Xác nhận CW</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={styles.secondaryBtn}
            onPress={handleTriggerRerate}
          >
            <Text style={styles.secondaryBtnText}>⚡ Tự động tính cước (Rerate)</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Multi-level Operational Graph (Orders, Shipments, Legs, Movements) */}
      <View style={styles.sectionCard}>
        <Text style={styles.sectionTitle}>
          Đồ thị Vận hành Đa cấp (Orders ↔ Shipments ↔ Chặng &amp; Chuyến)
        </Text>
        <Text style={styles.finRow}>
          • Đơn hàng (Orders):{" "}
          {(graph?.orders ?? []).map((o) => o.orderNo ?? o.id.slice(0, 8)).join(", ") ||
            "Chưa liên kết"}
        </Text>
        <Text style={styles.finRow}>
          • Lô hàng (Shipments):{" "}
          {(graph?.shipments ?? [])
            .map((s) => s.shipmentNo ?? s.id.slice(0, 8))
            .join(", ") || "Chưa liên kết"}
        </Text>
        <Text style={styles.finRow}>
          • Chặng vận chuyển (Legs):{" "}
          {(graph?.legs ?? []).map((l) => l.legNo ?? l.id.slice(0, 8)).join(", ") ||
            "Chưa liên kết"}
        </Text>
        <Text style={styles.finRow}>
          • Chuyến xe / Tàu (Movements):{" "}
          {(graph?.movements ?? [])
            .map((m) => m.movementNo ?? m.id.slice(0, 8))
            .join(", ") || "Chưa liên kết"}
        </Text>
      </View>

      {/* Chronological Financial History */}
      {finHistory.length > 0 ? (
        <View style={styles.sectionCard}>
          <Text style={styles.sectionTitle}>
            Dòng thời gian Tài chính Bill ({finHistory.length})
          </Text>
          {finHistory.slice(0, 8).map((ev, i) => (
            <View key={ev.id ?? i} style={styles.finBlock}>
              <Text style={styles.finSideTitle}>
                {ev.eventType ?? ev.category ?? "Sự kiện"} •{" "}
                {ev.amount !== null && ev.amount !== undefined
                  ? formatMoney(ev.amount, ev.currencyCode ?? currency)
                  : ""}
              </Text>
              <Text style={styles.finRow}>
                {ev.description ?? ""} ({(ev.occurredAt ?? "").slice(0, 16)})
              </Text>
            </View>
          ))}
        </View>
      ) : null}

      {/* Local Disk Attachment Gallery on Contabo VPS */}
      <AttachmentGallery
        objectType="bill"
        objectId={bill.id}
        objectLabelVi={`Bill ${bill.billNo}`}
        canWrite={true}
      />

      <View style={{ height: 36 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  loadingWrap: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
  },
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 16,
  },
  headerCard: {
    backgroundColor: "#0F172A",
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
  },
  billNo: {
    color: "#FFFFFF",
    fontSize: 20,
    fontWeight: "800",
  },
  routeText: {
    color: "#CBD5E1",
    fontSize: 13,
    marginTop: 4,
  },
  subText: {
    color: "#94A3B8",
    fontSize: 12,
    marginTop: 4,
  },
  sectionCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 12,
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 8,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
    flex: 1,
  },
  finBlock: {
    marginTop: 8,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  finSideTitle: {
    fontSize: 13,
    fontWeight: "700",
    color: "#1E293B",
  },
  finRow: {
    fontSize: 12,
    color: "#475569",
    marginTop: 2,
  },
  finBest: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
    marginTop: 2,
  },
  marginBanner: {
    marginTop: 10,
    backgroundColor: "#F0FDF4",
    padding: 10,
    borderRadius: 8,
  },
  marginText: {
    fontSize: 14,
    fontWeight: "800",
    color: "#047857",
  },
  cwBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  cwConfirmed: {
    backgroundColor: "#ECFDF5",
  },
  cwPending: {
    backgroundColor: "#FFFBEB",
  },
  cwBadgeText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#0F172A",
  },
  inputRow: {
    flexDirection: "row",
    gap: 8,
    marginBottom: 8,
  },
  inputCol: {
    flex: 1,
  },
  inputLabel: {
    fontSize: 11,
    color: "#475569",
    marginBottom: 4,
    fontWeight: "600",
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 13,
    color: "#0F172A",
    backgroundColor: "#F8FAFC",
    marginBottom: 8,
  },
  btnRow: {
    flexDirection: "row",
    gap: 8,
  },
  primaryBtn: {
    flex: 1,
    backgroundColor: "#0F172A",
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: "center",
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  secondaryBtn: {
    flex: 1,
    backgroundColor: "#EFF6FF",
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "#BFDBFE",
  },
  secondaryBtnText: {
    color: "#1D4ED8",
    fontSize: 12,
    fontWeight: "700",
  },
});
