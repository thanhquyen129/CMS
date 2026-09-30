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
import { CompactTxnCard } from "../../src/components/CompactTxnCard";
import { BalanceImpactCard } from "../../src/components/BalanceImpactCard";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";

interface LedgerEntryDto {
  id?: string;
  entryType: string;
  deltaAmount: number;
  runningBalance: number;
  reason?: string | null;
  createdAt?: string | null;
  referenceId?: string | null;
}

interface ApArLedgerDto {
  accountsPayableId?: string;
  initialAmount?: number;
  settledAmount?: number;
  writeOffAmount?: number;
  adjustedAmount?: number;
  outstandingAmount?: number;
  currencyCode?: string;
  entries?: LedgerEntryDto[];
}

export default function AccountsPayableScreen() {
  const { bootstrap, t } = useAuth();
  const [items, setItems] = useState<ApArItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [selectedAp, setSelectedAp] = useState<ApArItemDto | null>(null);
  const [sheetMode, setSheetMode] = useState<"writeoff" | "ledger">("ledger");
  const [ledger, setLedger] = useState<ApArLedgerDto | null>(null);
  const [loadingLedger, setLoadingLedger] = useState<boolean>(false);
  const [writeOffAmount, setWriteOffAmount] = useState<string>("");
  const [reason, setReason] = useState<string>("");
  const [submitting, setSubmitting] = useState<boolean>(false);

  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? false;

  const loadAp = useCallback(async () => {
    if (!canViewCost) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const res = await apiRequest<ApArItemDto[] | { items: ApArItemDto[] }>(
        "/api/accounts-payable"
      );
      setItems(Array.isArray(res) ? res : res?.items ?? []);
    } catch {
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [canViewCost]);

  useEffect(() => {
    void loadAp();
  }, [loadAp]);

  const loadApLedger = async (ap: ApArItemDto) => {
    setSelectedAp(ap);
    setWriteOffAmount(String(ap.remainingBalance));
    setLoadingLedger(true);
    try {
      const res = await apiRequest<ApArLedgerDto>(
        `/api/accounts-payable/${ap.id}/ledger`
      );
      setLedger(res);
    } catch {
      setLedger(null);
    } finally {
      setLoadingLedger(false);
    }
  };

  if (!canViewCost) {
    return (
      <View style={styles.sodBlocked}>
        <Text style={styles.sodTitle}>Phân tách Nhiệm vụ (Chi phí ≠ Doanh thu)</Text>
        <Text style={styles.sodDesc}>
          Tài khoản của bạn không được cấp quyền xem Công nợ Phải trả (AP).
        </Text>
      </View>
    );
  }

  const handleWriteOff = async (ap: ApArItemDto) => {
    if (!reason.trim()) {
      Alert.alert("Thiếu lý do", "Xóa nợ (Write-off) bắt buộc phải có lý do kiểm toán.");
      return;
    }
    const amt = Number(writeOffAmount) || ap.remainingBalance;
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Xóa nợ Công nợ Phải trả (AP Write-off)",
      amountSummaryVi: formatMoney(amt, ap.currencyCode),
    });
    if (!bio.verified) {
      Alert.alert("Chưa xác thực sinh trắc học", bio.errorVi ?? "Đã hủy thao tác.");
      return;
    }

    setSubmitting(true);
    try {
      const res = await apiRequest<{
        requiresApproval?: boolean;
        message?: string;
      }>(`/api/accounts-payable/${ap.id}/write-off`, {
        method: "POST",
        ifMatch: ap.rowVersion,
        idempotencyKey: createIdempotencyKey("ap-wo"),
        body: {
          amount: amt,
          reason: reason.trim(),
        },
      });
      setReason("");
      await loadAp();
      await loadApLedger(ap);
      if (res?.requiresApproval) {
        Alert.alert(
          "Đã chuyển Hàng đợi Phê duyệt",
          res.message ??
            "Số tiền xóa nợ vượt hạn mức tự động và đã được gửi tới Kiểm soát viên phê duyệt."
        );
      } else {
        Alert.alert(
          "Thành công",
          "Đã thực hiện xóa nợ (Write-off) AP và cập nhật Sổ công nợ bất biến (ADR-0037)."
        );
      }
    } catch (err) {
      Alert.alert(
        "Lỗi thao tác AP",
        err instanceof Error ? err.message : "Không thể thực hiện xóa nợ."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleReverseWriteOff = async (
    ap: ApArItemDto,
    adjustmentId: string,
    deltaAmt: number
  ) => {
    if (!reason.trim()) {
      Alert.alert(
        "Thiếu lý do hoàn tác",
        "Hoàn tác xóa nợ bắt buộc phải nhập lý do giải trình kiểm toán."
      );
      return;
    }

    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Hoàn tác Xóa nợ Phải trả (Reverse AP Write-off)",
      amountSummaryVi: formatMoney(Math.abs(deltaAmt), ap.currencyCode),
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      await apiRequest(
        `/api/accounts-payable/${ap.id}/write-offs/${adjustmentId}/reverse`,
        {
          method: "POST",
          ifMatch: ap.rowVersion,
          idempotencyKey: createIdempotencyKey("ap-wo-rev"),
          body: { reason: reason.trim() },
        }
      );
      setReason("");
      await loadAp();
      await loadApLedger(ap);
      Alert.alert(
        "Đã hoàn tác xóa nợ",
        "Đã ghi nhận bút toán WRITEOFF_REVERSAL và khôi phục số dư công nợ AP."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi hoàn tác xóa nợ",
        err instanceof Error ? err.message : "Không thể hoàn tác xóa nợ AP."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleSettlementCorrection = async (ap: ApArItemDto) => {
    if (!reason.trim()) {
      Alert.alert(
        "Thiếu lý do hiệu chỉnh",
        "Hiệu chỉnh số dư kế toán (ADR-0038) bắt buộc phải có lý do kiểm toán."
      );
      return;
    }
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Hiệu chỉnh Số dư Thanh toán AP (ADR-0038)",
      amountSummaryVi: ap.documentNo ?? ap.id.slice(0, 8),
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      const res = await apiRequest<{
        previousSettledAmount?: number;
        correctedSettledAmount?: number;
        deltaAmount?: number;
      }>(`/api/accounts-payable/${ap.id}/settlement-correction`, {
        method: "POST",
        ifMatch: ap.rowVersion,
        body: { reason: reason.trim() },
      });
      setReason("");
      await loadAp();
      await loadApLedger(ap);
      Alert.alert(
        "Đã hiệu chỉnh số dư AP",
        `Đã đồng bộ số tiền đã thanh toán từ ${formatMoney(
          res?.previousSettledAmount ?? ap.settledAmount,
          ap.currencyCode
        )} → ${formatMoney(
          res?.correctedSettledAmount ?? ap.settledAmount,
          ap.currencyCode
        )}.`
      );
    } catch (err) {
      Alert.alert(
        "Lỗi hiệu chỉnh số dư",
        err instanceof Error ? err.message : "Không thể hiệu chỉnh số dư AP."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadAp()} />
      }
    >
      <Text style={styles.heading}>
        Công nợ Phải trả (AP • {items.length}) • Sổ Công nợ Bất biến (ADR-0037)
      </Text>

      {selectedAp ? (
        <View style={styles.sheet}>
          <View style={styles.rowBetween}>
            <Text style={styles.sheetTitle}>
              Hồ sơ AP: {selectedAp.documentNo ?? selectedAp.id.slice(0, 8)}
            </Text>
            <TouchableOpacity onPress={() => setSelectedAp(null)}>
              <Text style={styles.closeText}>Đóng ✕</Text>
            </TouchableOpacity>
          </View>

          <View style={styles.modeRow}>
            <TouchableOpacity
              style={[
                styles.modeBtn,
                sheetMode === "ledger" && styles.modeBtnActive,
              ]}
              onPress={() => setSheetMode("ledger")}
            >
              <Text
                style={[
                  styles.modeBtnText,
                  sheetMode === "ledger" && styles.modeBtnTextActive,
                ]}
              >
                📖 Sổ công nợ &amp; Hoàn tác (ADR-0037)
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[
                styles.modeBtn,
                sheetMode === "writeoff" && styles.modeBtnActive,
              ]}
              onPress={() => setSheetMode("writeoff")}
            >
              <Text
                style={[
                  styles.modeBtnText,
                  sheetMode === "writeoff" && styles.modeBtnTextActive,
                ]}
              >
                ✂️ Xóa nợ (Write-off)
              </Text>
            </TouchableOpacity>
          </View>

          {sheetMode === "ledger" ? (
            <View>
              <View style={styles.summaryGrid}>
                <View style={styles.summaryCell}>
                  <Text style={styles.summaryLabel}>Ghi nhận gốc</Text>
                  <Text style={styles.summaryVal}>
                    {formatMoney(
                      ledger?.initialAmount ?? selectedAp.recognizedAmount,
                      selectedAp.currencyCode
                    )}
                  </Text>
                </View>
                <View style={styles.summaryCell}>
                  <Text style={styles.summaryLabel}>Đã thanh toán</Text>
                  <Text style={styles.summaryVal}>
                    {formatMoney(
                      ledger?.settledAmount ?? selectedAp.settledAmount,
                      selectedAp.currencyCode
                    )}
                  </Text>
                </View>
                <View style={styles.summaryCell}>
                  <Text style={styles.summaryLabel}>Đã xóa nợ</Text>
                  <Text style={styles.summaryVal}>
                    {formatMoney(
                      ledger?.writeOffAmount ?? selectedAp.writtenOffAmount,
                      selectedAp.currencyCode
                    )}
                  </Text>
                </View>
                <View style={styles.summaryCell}>
                  <Text style={styles.summaryLabel}>Số dư còn lại</Text>
                  <Text style={[styles.summaryVal, { color: "#DC2626" }]}>
                    {formatMoney(
                      ledger?.outstandingAmount ?? selectedAp.remainingBalance,
                      selectedAp.currencyCode
                    )}
                  </Text>
                </View>
              </View>

              <TextInput
                style={styles.input}
                value={reason}
                onChangeText={setReason}
                placeholder="Nhập lý do hoàn tác xóa nợ hoặc hiệu chỉnh số dư (bắt buộc)..."
              />

              <TouchableOpacity
                style={styles.correctionBtn}
                onPress={() => handleSettlementCorrection(selectedAp)}
                disabled={submitting}
              >
                <Text style={styles.correctionBtnText}>
                  ⚖️ Đối chiếu &amp; Hiệu chỉnh Số dư Thanh toán (ADR-0038)
                </Text>
              </TouchableOpacity>

              <Text style={styles.subHeader}>
                Lịch sử Bút toán Sổ Công nợ Bất biến:
              </Text>
              {loadingLedger ? (
                <ActivityIndicator size="small" color="#0F172A" />
              ) : !ledger?.entries || ledger.entries.length === 0 ? (
                <Text style={styles.emptyLedger}>
                  Khoản công nợ đang ở trạng thái khởi tạo ban đầu.
                </Text>
              ) : (
                ledger.entries.map((entry, idx) => {
                  const isWriteOff =
                    entry.entryType.toUpperCase().includes("WRITEOFF") &&
                    !entry.entryType.toUpperCase().includes("REVERSAL");
                  const revertId = entry.referenceId ?? entry.id;
                  return (
                    <View key={entry.id ?? idx} style={styles.ledgerRow}>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.ledgerType}>{entry.entryType}</Text>
                        <Text style={styles.ledgerMeta}>
                          Biến động:{" "}
                          {formatMoney(entry.deltaAmount, selectedAp.currencyCode)}{" "}
                          → Lũy kế:{" "}
                          {formatMoney(
                            entry.runningBalance,
                            selectedAp.currencyCode
                          )}
                        </Text>
                        {entry.reason ? (
                          <Text style={styles.ledgerMeta}>
                            Lý do: {entry.reason}
                          </Text>
                        ) : null}
                      </View>
                      {isWriteOff && revertId ? (
                        <TouchableOpacity
                          style={styles.reverseBtn}
                          onPress={() =>
                            handleReverseWriteOff(
                              selectedAp,
                              revertId,
                              entry.deltaAmount
                            )
                          }
                          disabled={submitting}
                        >
                          <Text style={styles.reverseBtnText}>
                            ↩️ Hoàn tác
                          </Text>
                        </TouchableOpacity>
                      ) : null}
                    </View>
                  );
                })
              )}
            </View>
          ) : (
            <View>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={writeOffAmount}
                onChangeText={setWriteOffAmount}
                placeholder="Số tiền xóa nợ (Write-off)"
              />
              <TextInput
                style={styles.input}
                value={reason}
                onChangeText={setReason}
                placeholder="Lý do xóa nợ / điều chỉnh công nợ (bắt buộc)..."
              />
              <BalanceImpactCard
                titleVi="Tác động Số dư Còn lại (Before → After)"
                currencyCode={selectedAp.currencyCode}
                beforeAmount={selectedAp.remainingBalance}
                afterAmount={Math.max(
                  0,
                  selectedAp.remainingBalance -
                    (Number(writeOffAmount) || selectedAp.remainingBalance)
                )}
                rowVersion={selectedAp.rowVersion}
                confirmLabelVi="Xác nhận Write-off AP"
                isSubmitting={submitting}
                onConfirmWithBiometric={() => handleWriteOff(selectedAp)}
                onCancel={() => setSelectedAp(null)}
              />
            </View>
          )}
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : (
        items.map((ap) => (
          <CompactTxnCard
            key={ap.id}
            primaryCode={ap.documentNo ?? "Công nợ Phải trả (AP)"}
            statusLabelVi={t(
              `settlement.${ap.settlementStatus}`,
              ap.settlementStatus
            )}
            statusTone={
              ap.settlementStatus === "settled"
                ? "success"
                : ap.settlementStatus === "partially_settled"
                ? "info"
                : "warning"
            }
            secondaryLabelVi={`NCC: ${ap.partyName ?? "Nhà cung cấp"} • Đã trả: ${formatMoney(
              ap.settledAmount,
              ap.currencyCode
            )}`}
            effectiveDate={ap.dueDate ? `Hạn: ${ap.dueDate}` : undefined}
            amount={ap.remainingBalance}
            currencyCode={ap.currencyCode}
            actionLabelVi="Sổ công nợ & Xử lý"
            onPress={() => void loadApLedger(ap)}
            onActionPress={() => void loadApLedger(ap)}
          />
        ))
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
  heading: {
    fontSize: 14,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 12,
  },
  sodBlocked: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 24,
  },
  sodTitle: {
    fontSize: 16,
    fontWeight: "800",
    color: "#DC2626",
  },
  sodDesc: {
    fontSize: 13,
    color: "#475569",
    marginTop: 6,
    textAlign: "center",
  },
  sheet: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginBottom: 14,
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 8,
  },
  sheetTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
  },
  closeText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#64748B",
  },
  modeRow: {
    flexDirection: "row",
    gap: 6,
    marginBottom: 10,
  },
  modeBtn: {
    flex: 1,
    paddingVertical: 8,
    borderRadius: 8,
    backgroundColor: "#F1F5F9",
    alignItems: "center",
  },
  modeBtnActive: {
    backgroundColor: "#0F172A",
  },
  modeBtnText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#334155",
  },
  modeBtnTextActive: {
    color: "#FFFFFF",
  },
  summaryGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
    marginBottom: 10,
  },
  summaryCell: {
    flex: 1,
    minWidth: 120,
    backgroundColor: "#F8FAFC",
    padding: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  summaryLabel: {
    fontSize: 11,
    color: "#64748B",
  },
  summaryVal: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
    marginTop: 2,
  },
  subHeader: {
    fontSize: 12,
    fontWeight: "800",
    color: "#1E293B",
    marginTop: 8,
    marginBottom: 4,
  },
  correctionBtn: {
    backgroundColor: "#EFF6FF",
    borderWidth: 1,
    borderColor: "#93C5FD",
    borderRadius: 8,
    paddingVertical: 9,
    alignItems: "center",
    marginBottom: 6,
  },
  correctionBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#1D4ED8",
  },
  ledgerRow: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: "#F1F5F9",
  },
  ledgerType: {
    fontSize: 12,
    fontWeight: "800",
    color: "#0F172A",
  },
  ledgerMeta: {
    fontSize: 11,
    color: "#475569",
    marginTop: 2,
  },
  reverseBtn: {
    backgroundColor: "#FEF2F2",
    borderWidth: 1,
    borderColor: "#FECACA",
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 6,
  },
  reverseBtnText: {
    fontSize: 11,
    fontWeight: "700",
    color: "#DC2626",
  },
  emptyLedger: {
    fontSize: 12,
    color: "#64748B",
    fontStyle: "italic",
    marginVertical: 6,
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 13,
    marginBottom: 8,
    backgroundColor: "#F8FAFC",
    color: "#0F172A",
  },
});
