import React from "react";
import { StyleSheet, Text, TouchableOpacity, View } from "react-native";
import { formatMoney } from "@lcms/shared";

export interface CompactTxnCardProps {
  primaryCode: string;
  statusLabelVi: string;
  statusTone?: "neutral" | "success" | "warning" | "danger" | "info";
  secondaryLabelVi: string;
  effectiveDate?: string | null;
  amount: number;
  currencyCode: string;
  reportingAmount?: number | null;
  reportingCurrencyCode?: string | null;
  fxStatusLabelVi?: string | null;
  actionLabelVi?: string;
  onPress?: () => void;
  onActionPress?: () => void;
}

/**
 * UI-TABLE-01 compliant 3-line mobile transaction card:
 * - Line 1: Business Code (BillNo / DocumentNo / CostType) + Vietnamese Status Badge
 * - Line 2: Counterparty / Attribution + Effective Date
 * - Line 3: Original Transaction Money + Reporting Base Money (never mixes currencies)
 */
export const CompactTxnCard: React.FC<CompactTxnCardProps> = ({
  primaryCode,
  statusLabelVi,
  statusTone = "neutral",
  secondaryLabelVi,
  effectiveDate,
  amount,
  currencyCode,
  reportingAmount,
  reportingCurrencyCode,
  fxStatusLabelVi,
  actionLabelVi,
  onPress,
  onActionPress,
}) => {
  const badgeColors = {
    neutral: { bg: "#F1F5F9", text: "#334155" },
    info: { bg: "#EFF6FF", text: "#1D4ED8" },
    success: { bg: "#ECFDF5", text: "#047857" },
    warning: { bg: "#FFFBEB", text: "#B45309" },
    danger: { bg: "#FEF2F2", text: "#B91C1C" },
  }[statusTone];

  const showReporting =
    reportingAmount !== null &&
    reportingAmount !== undefined &&
    reportingCurrencyCode &&
    reportingCurrencyCode.toUpperCase() !== currencyCode.toUpperCase();

  return (
    <TouchableOpacity
      style={styles.card}
      activeOpacity={onPress ? 0.75 : 1}
      onPress={onPress}
    >
      {/* Line 1: Code + Vietnamese Status Badge */}
      <View style={styles.rowBetween}>
        <Text style={styles.primaryCode} numberOfLines={1}>
          {primaryCode}
        </Text>
        <View style={[styles.badge, { backgroundColor: badgeColors.bg }]}>
          <Text style={[styles.badgeText, { color: badgeColors.text }]}>
            {statusLabelVi}
          </Text>
        </View>
      </View>

      {/* Line 2: Secondary context + Date */}
      <View style={styles.rowBetween}>
        <Text style={styles.secondaryText} numberOfLines={1}>
          {secondaryLabelVi}
        </Text>
        {effectiveDate ? (
          <Text style={styles.dateText}>{effectiveDate}</Text>
        ) : null}
      </View>

      {/* Line 3: Money (Original + Reporting) + Action */}
      <View style={[styles.rowBetween, styles.moneyRow]}>
        <View>
          <Text style={styles.moneyPrimary}>
            {formatMoney(amount, currencyCode)}
          </Text>
          {showReporting ? (
            <Text style={styles.moneySecondary}>
              Quy đổi: {formatMoney(reportingAmount, reportingCurrencyCode!)}
              {fxStatusLabelVi ? ` • ${fxStatusLabelVi}` : ""}
            </Text>
          ) : fxStatusLabelVi ? (
            <Text style={styles.moneySecondary}>{fxStatusLabelVi}</Text>
          ) : null}
        </View>

        {actionLabelVi && onActionPress ? (
          <TouchableOpacity style={styles.actionBtn} onPress={onActionPress}>
            <Text style={styles.actionBtnText}>{actionLabelVi}</Text>
          </TouchableOpacity>
        ) : null}
      </View>
    </TouchableOpacity>
  );
};

const styles = StyleSheet.create({
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 10,
    padding: 14,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 4,
  },
  primaryCode: {
    fontSize: 15,
    fontWeight: "700",
    color: "#0F172A",
    flex: 1,
    marginRight: 8,
  },
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: "700",
  },
  secondaryText: {
    fontSize: 13,
    color: "#475569",
    flex: 1,
    marginRight: 8,
  },
  dateText: {
    fontSize: 12,
    color: "#64748B",
  },
  moneyRow: {
    marginTop: 6,
    marginBottom: 0,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: "#F1F5F9",
  },
  moneyPrimary: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
  },
  moneySecondary: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 2,
  },
  actionBtn: {
    backgroundColor: "#0F172A",
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 6,
  },
  actionBtnText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#FFFFFF",
  },
});

