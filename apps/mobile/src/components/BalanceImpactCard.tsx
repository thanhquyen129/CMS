import React from "react";
import { StyleSheet, Text, TouchableOpacity, View } from "react-native";
import { formatMoney } from "@lcms/shared";

export interface BalanceImpactCardProps {
  titleVi: string;
  subtitleVi?: string;
  currencyCode: string;
  beforeAmount: number;
  afterAmount: number;
  deltaLabelVi?: string;
  rowVersion?: string | null;
  confirmLabelVi: string;
  isSubmitting?: boolean;
  onConfirmWithBiometric: () => void;
  onCancel?: () => void;
}

/**
 * Displays a transparent Before → After financial impact preview before any money mutation
 * (Approve, Confirm/Actualize, Allocate, Write-off, Reverse Write-off).
 */
export const BalanceImpactCard: React.FC<BalanceImpactCardProps> = ({
  titleVi,
  subtitleVi,
  currencyCode,
  beforeAmount,
  afterAmount,
  deltaLabelVi,
  rowVersion,
  confirmLabelVi,
  isSubmitting,
  onConfirmWithBiometric,
  onCancel,
}) => {
  const delta = afterAmount - beforeAmount;
  const deltaPrefix = delta > 0 ? "+" : "";

  return (
    <View style={styles.card}>
      <View style={styles.headerRow}>
        <Text style={styles.title}>{titleVi}</Text>
        <View style={styles.biometricBadge}>
          <Text style={styles.biometricBadgeText}>Sinh trắc học</Text>
        </View>
      </View>

      {subtitleVi ? <Text style={styles.subtitle}>{subtitleVi}</Text> : null}

      <View style={styles.impactBox}>
        <View style={styles.impactCol}>
          <Text style={styles.impactLabel}>Trước thao tác</Text>
          <Text style={styles.impactValue}>{formatMoney(beforeAmount, currencyCode)}</Text>
        </View>
        <Text style={styles.arrow}>→</Text>
        <View style={styles.impactCol}>
          <Text style={styles.impactLabel}>Sau thao tác</Text>
          <Text style={[styles.impactValue, styles.impactValueAfter]}>
            {formatMoney(afterAmount, currencyCode)}
          </Text>
        </View>
      </View>

      <View style={styles.metaRow}>
        <Text style={styles.deltaText}>
          {deltaLabelVi ?? "Biến động"}: {deltaPrefix}
          {formatMoney(delta, currencyCode)}
        </Text>
        {rowVersion ? (
          <Text style={styles.versionText}>If-Match: {rowVersion.slice(0, 8)}…</Text>
        ) : null}
      </View>

      <View style={styles.actionsRow}>
        {onCancel ? (
          <TouchableOpacity
            style={styles.cancelBtn}
            onPress={onCancel}
            disabled={isSubmitting}
          >
            <Text style={styles.cancelBtnText}>Hủy</Text>
          </TouchableOpacity>
        ) : null}
        <TouchableOpacity
          style={[styles.confirmBtn, isSubmitting && styles.confirmBtnDisabled]}
          onPress={onConfirmWithBiometric}
          disabled={isSubmitting}
        >
          <Text style={styles.confirmBtnText}>
            {isSubmitting ? "Đang xử lý..." : `🔒 ${confirmLabelVi}`}
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginVertical: 8,
  },
  headerRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 4,
  },
  title: {
    fontSize: 15,
    fontWeight: "700",
    color: "#0F172A",
    flex: 1,
  },
  biometricBadge: {
    backgroundColor: "#EFF6FF",
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderWidth: 1,
    borderColor: "#BFDBFE",
  },
  biometricBadgeText: {
    fontSize: 11,
    fontWeight: "600",
    color: "#1D4ED8",
  },
  subtitle: {
    fontSize: 13,
    color: "#475569",
    marginBottom: 10,
  },
  impactBox: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    backgroundColor: "#F8FAFC",
    borderRadius: 8,
    padding: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  impactCol: {
    flex: 1,
  },
  impactLabel: {
    fontSize: 11,
    color: "#64748B",
    marginBottom: 2,
  },
  impactValue: {
    fontSize: 14,
    fontWeight: "700",
    color: "#334155",
  },
  impactValueAfter: {
    color: "#0F766E",
  },
  arrow: {
    fontSize: 18,
    fontWeight: "700",
    color: "#64748B",
    paddingHorizontal: 10,
  },
  metaRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 8,
    marginBottom: 12,
  },
  deltaText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#334155",
  },
  versionText: {
    fontSize: 11,
    color: "#94A3B8",
  },
  actionsRow: {
    flexDirection: "row",
    justifyContent: "flex-end",
    gap: 8,
  },
  cancelBtn: {
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#CBD5E1",
  },
  cancelBtnText: {
    fontSize: 13,
    fontWeight: "600",
    color: "#475569",
  },
  confirmBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 8,
  },
  confirmBtnDisabled: {
    opacity: 0.6,
  },
  confirmBtnText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#FFFFFF",
  },
});

