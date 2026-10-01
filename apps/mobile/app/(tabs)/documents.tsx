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
import { createIdempotencyKey, formatMoney } from "@lcms/shared";
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";
import { AttachmentGallery } from "../../src/components/AttachmentGallery";
import { verifyBiometricForMoneyAction } from "../../src/security/biometrics";

interface FinancialDocumentItem {
  id: string;
  documentNo: string;
  documentType: string;
  direction: "payable" | "receivable";
  currencyCode: string;
  totalAmount: number;
  receiptStatus: string;
  acceptanceStatus: string;
  matchingStatus: string;
  issueDate?: string | null;
  rowVersion: string;
}

interface MatchSuggestionItem {
  sourceLineId?: string;
  targetCostId?: string | null;
  targetRevenueId?: string | null;
  suggestedAmount?: number;
  description?: string;
}

export default function DocumentsScreen() {
  const { bootstrap, t } = useAuth();
  const canViewCost = bootstrap?.financialVisibility.canViewCost ?? true;
  const canViewRevenue = bootstrap?.financialVisibility.canViewRevenue ?? true;

  const [docs, setDocs] = useState<FinancialDocumentItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [selectedDoc, setSelectedDoc] = useState<FinancialDocumentItem | null>(
    null
  );

  // Create Document state
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [docNo, setDocNo] = useState<string>("");
  const [docType, setDocType] = useState<"INVOICE" | "DEBIT_NOTE" | "CREDIT_NOTE">("INVOICE");
  const [direction, setDirection] = useState<"payable" | "receivable">(
    canViewCost ? "payable" : "receivable"
  );
  const [totalAmount, setTotalAmount] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<"VND" | "USD">("VND");
  const [billRef, setBillRef] = useState<string>("");
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Match & Line state
  const [activeMatchId, setActiveMatchId] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<MatchSuggestionItem[]>([]);
  const [lineAmount, setLineAmount] = useState<string>("");
  const [lineDesc, setLineDesc] = useState<string>("");
  const [cancelReason, setCancelReason] = useState<string>("");

  const loadDocs = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiRequest<
        FinancialDocumentItem[] | { items: FinancialDocumentItem[] }
      >("/api/financial-documents");
      setDocs(Array.isArray(res) ? res : res?.items ?? []);
    } catch {
      setDocs([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDocs();
  }, [loadDocs]);

  const handleCreateDocument = async () => {
    const amt = Number(totalAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Số tiền không hợp lệ", "Vui lòng nhập tổng tiền chứng từ lớn hơn 0.");
      return;
    }

    const cleanDocNo =
      docNo.trim() ||
      `DOC-${new Date().toISOString().slice(2, 10).replace(/-/g, "")}-${Math.floor(
        100 + Math.random() * 900
      )}`;

    setSubmitting(true);
    try {
      const created = await apiRequest<{ id: string }>("/api/financial-documents", {
        method: "POST",
        idempotencyKey: createIdempotencyKey("doc-new"),
        body: {
          documentType: docType,
          documentNo: cleanDocNo,
          direction,
          totalAmount: amt,
          currencyCode,
          billId: billRef.trim() || null,
          notes: "Tiếp nhận từ ứng dụng di động LCMS",
          sourceSystem: "LCMS_MOBILE",
        },
      });

      if (created?.id) {
        await apiRequest(`/api/financial-documents/${created.id}/lines`, {
          method: "POST",
          body: {
            amount: amt,
            description: `Dòng chứng từ ${cleanDocNo}`,
            billId: billRef.trim() || null,
            costTypeCode: direction === "payable" ? "FREIGHT" : null,
            revenueTypeCode: direction === "receivable" ? "FREIGHT_REV" : null,
            currencyCode,
          },
        }).catch(() => undefined);
      }

      setShowCreate(false);
      setDocNo("");
      setTotalAmount("");
      setBillRef("");
      await loadDocs();
      Alert.alert(
        "Đã tiếp nhận Chứng từ",
        `Đã khởi tạo chứng từ ${cleanDocNo} (${formatMoney(amt, currencyCode)}).`
      );
    } catch (err) {
      Alert.alert(
        "Lỗi tiếp nhận chứng từ",
        err instanceof Error ? err.message : "Không thể tạo chứng từ mới."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleAcceptDocument = async (doc: FinancialDocumentItem) => {
    try {
      await apiRequest(`/api/financial-documents/${doc.id}/accept`, {
        method: "POST",
        ifMatch: doc.rowVersion,
        idempotencyKey: createIdempotencyKey("doc-acc"),
        body: {},
      });
      Alert.alert(
        "Đã nghiệm thu chứng từ",
        `Chứng từ ${doc.documentNo} đã chuyển sang Đã nghiệm thu (Accepted).`
      );
      await loadDocs();
    } catch (err) {
      Alert.alert(
        "Không thể nghiệm thu",
        err instanceof Error ? err.message : "Lỗi nghiệm thu chứng từ."
      );
    }
  };

  const handleAddLine = async (doc: FinancialDocumentItem) => {
    const amt = Number(lineAmount);
    if (!amt || amt <= 0) {
      Alert.alert("Số tiền không hợp lệ", "Vui lòng nhập số tiền dòng chứng từ.");
      return;
    }
    try {
      await apiRequest(`/api/financial-documents/${doc.id}/lines`, {
        method: "POST",
        body: {
          amount: amt,
          description: lineDesc.trim() || `Dòng chi tiết ${doc.documentNo}`,
          currencyCode: doc.currencyCode,
          costTypeCode: doc.direction === "payable" ? "FREIGHT" : null,
          revenueTypeCode: doc.direction === "receivable" ? "FREIGHT_REV" : null,
        },
      });
      setLineAmount("");
      setLineDesc("");
      Alert.alert("Đã thêm dòng", "Đã bổ sung dòng chi tiết cho chứng từ.");
      await loadDocs();
    } catch (err) {
      Alert.alert(
        "Lỗi thêm dòng",
        err instanceof Error ? err.message : "Không thể thêm dòng chứng từ."
      );
    }
  };

  const handleStartMatchAndCreateExposure = async (doc: FinancialDocumentItem) => {
    const bio = await verifyBiometricForMoneyAction({
      actionLabelVi: "Đối chiếu Chứng từ N:N & Tạo Công nợ (AP/AR)",
      amountSummaryVi: `${doc.documentNo} • ${formatMoney(doc.totalAmount, doc.currencyCode)}`,
    });
    if (!bio.verified) return;

    setSubmitting(true);
    try {
      const started = await apiRequest<{ id: string }>("/api/document-matches", {
        method: "POST",
        idempotencyKey: createIdempotencyKey("match-start"),
        body: {
          primaryDocumentId: doc.id,
          matchMethod: "auto_tolerance",
          notes: `Đối chiếu trên Mobile cho ${doc.documentNo}`,
          toleranceAmount: 1000,
          tolerancePercent: 1,
        },
      });

      const matchId = started.id;
      setActiveMatchId(matchId);

      const sugList = await apiRequest<MatchSuggestionItem[]>(
        `/api/document-matches/${matchId}/suggestions`
      ).catch(() => []);
      setSuggestions(Array.isArray(sugList) ? sugList : []);

      await apiRequest(`/api/document-matches/${matchId}/confirm`, {
        method: "POST",
      }).catch(() => undefined);

      await apiRequest(`/api/document-matches/${matchId}/create-exposures`, {
        method: "POST",
      }).catch(() => undefined);

      await loadDocs();
      Alert.alert(
        "Đã đối chiếu & Tạo Công nợ",
        `Đã khởi tạo phiên đối chiếu N:N và sinh Công nợ (${doc.direction === "payable" ? "AP" : "AR"}) từ chứng từ ${doc.documentNo}.`
      );
    } catch (err) {
      Alert.alert(
        "Thông báo đối chiếu",
        err instanceof Error
          ? err.message
          : "Không thể hoàn tất đối chiếu tự động."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancelDocument = async (doc: FinancialDocumentItem) => {
    if (!cancelReason.trim()) {
      Alert.alert("Thiếu lý do hủy", "Vui lòng nhập lý do hủy chứng từ để lưu Audit.");
      return;
    }
    try {
      await apiRequest(`/api/financial-documents/${doc.id}/cancel`, {
        method: "POST",
        body: {
          reason: cancelReason.trim(),
          void: false,
        },
      });
      setCancelReason("");
      setSelectedDoc(null);
      await loadDocs();
      Alert.alert("Đã hủy chứng từ", `Chứng từ ${doc.documentNo} đã được hủy có lý do.`);
    } catch (err) {
      Alert.alert(
        "Không thể hủy chứng từ",
        err instanceof Error ? err.message : "Lỗi hủy chứng từ."
      );
    }
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadDocs()} />
      }
    >
      <View style={styles.headerRow}>
        <Text style={styles.heading}>
          Chứng từ Tài chính ({docs.length}) • 3 Trục Độc lập
        </Text>
        <TouchableOpacity
          style={styles.addBtn}
          onPress={() => setShowCreate((v) => !v)}
        >
          <Text style={styles.addBtnText}>
            {showCreate ? "✕ Đóng" : "+ Tiếp nhận Chứng từ"}
          </Text>
        </TouchableOpacity>
      </View>

      {showCreate ? (
        <View style={styles.sheet}>
          <Text style={styles.sheetTitle}>
            Tiếp nhận Chứng từ Mới (Hóa đơn / Debit Note / Credit Note)
          </Text>

          <Text style={styles.label}>Loại Chứng từ</Text>
          <View style={styles.chipRow}>
            {(["INVOICE", "DEBIT_NOTE", "CREDIT_NOTE"] as const).map((tp) => (
              <TouchableOpacity
                key={tp}
                style={[styles.chip, docType === tp && styles.chipActive]}
                onPress={() => setDocType(tp)}
              >
                <Text
                  style={[
                    styles.chipText,
                    docType === tp && styles.chipTextActive,
                  ]}
                >
                  {tp === "INVOICE"
                    ? "Hóa đơn (Invoice)"
                    : tp === "DEBIT_NOTE"
                    ? "Giấy báo nợ (Debit Note)"
                    : "Giấy báo có (Credit Note)"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <Text style={styles.label}>Phân loại Nghiệp vụ Công nợ</Text>
          <View style={styles.chipRow}>
            {canViewCost ? (
              <TouchableOpacity
                style={[
                  styles.chip,
                  direction === "payable" && styles.chipActive,
                ]}
                onPress={() => setDirection("payable")}
              >
                <Text
                  style={[
                    styles.chipText,
                    direction === "payable" && styles.chipTextActive,
                  ]}
                >
                  Đầu vào Phải trả (Payable / AP)
                </Text>
              </TouchableOpacity>
            ) : null}
            {canViewRevenue ? (
              <TouchableOpacity
                style={[
                  styles.chip,
                  direction === "receivable" && styles.chipActive,
                ]}
                onPress={() => setDirection("receivable")}
              >
                <Text
                  style={[
                    styles.chipText,
                    direction === "receivable" && styles.chipTextActive,
                  ]}
                >
                  Đầu ra Phải thu (Receivable / AR)
                </Text>
              </TouchableOpacity>
            ) : null}
          </View>

          <Text style={styles.label}>Số Chứng từ (Để trống sẽ tự sinh DOC-...)</Text>
          <TextInput
            style={styles.input}
            value={docNo}
            onChangeText={setDocNo}
            placeholder="VD: INV-2026-0091"
            autoCapitalize="characters"
          />

          <View style={styles.twoCol}>
            <View style={{ flex: 1 }}>
              <Text style={styles.label}>Tổng tiền chứng từ</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                value={totalAmount}
                onChangeText={setTotalAmount}
                placeholder="VD: 5500000"
              />
            </View>
            <View>
              <Text style={styles.label}>Tiền tệ</Text>
              <View style={styles.chipRow}>
                {(["VND", "USD"] as const).map((cur) => (
                  <TouchableOpacity
                    key={cur}
                    style={[styles.chip, currencyCode === cur && styles.chipActive]}
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

          <Text style={styles.label}>Mã Vận đơn liên kết (Tùy chọn)</Text>
          <TextInput
            style={styles.input}
            value={billRef}
            onChangeText={setBillRef}
            placeholder="Nhập Số Bill hoặc ID Bill..."
          />

          <TouchableOpacity
            style={styles.primaryBtn}
            onPress={handleCreateDocument}
            disabled={submitting}
          >
            <Text style={styles.primaryBtnText}>
              {submitting ? "Đang xử lý..." : "✓ Tiếp nhận Chứng từ & Tạo Dòng"}
            </Text>
          </TouchableOpacity>
        </View>
      ) : null}

      {selectedDoc ? (
        <View style={styles.sheet}>
          <View style={styles.rowBetween}>
            <Text style={styles.sheetTitle}>
              Xử lý Chứng từ: {selectedDoc.documentNo}
            </Text>
            <TouchableOpacity onPress={() => setSelectedDoc(null)}>
              <Text style={styles.closeLink}>Đóng ✕</Text>
            </TouchableOpacity>
          </View>

          <View style={styles.actionGrid}>
            {selectedDoc.acceptanceStatus === "not_accepted" ? (
              <TouchableOpacity
                style={styles.acceptBtn}
                onPress={() => handleAcceptDocument(selectedDoc)}
              >
                <Text style={styles.acceptBtnText}>
                  ✓ 1. Nghiệm thu Chứng từ (Accept)
                </Text>
              </TouchableOpacity>
            ) : null}

            <TouchableOpacity
              style={styles.matchBtn}
              onPress={() => handleStartMatchAndCreateExposure(selectedDoc)}
              disabled={submitting}
            >
              <Text style={styles.matchBtnText}>
                ⚡ 2. Đối chiếu N:N & Sinh Công nợ ({selectedDoc.direction === "payable" ? "AP" : "AR"})
              </Text>
            </TouchableOpacity>
          </View>

          {activeMatchId && suggestions.length > 0 ? (
            <View style={styles.suggestionBox}>
              <Text style={styles.label}>
                Gợi ý Đối chiếu Tự động ({suggestions.length}):
              </Text>
              {suggestions.map((s, idx) => (
                <Text key={idx} style={styles.suggestionItem}>
                  • {s.description ?? "Khoản khớp"}:{" "}
                  {formatMoney(s.suggestedAmount ?? selectedDoc.totalAmount, selectedDoc.currencyCode)}
                </Text>
              ))}
            </View>
          ) : null}

          <View style={styles.subBox}>
            <Text style={styles.label}>Bổ sung Dòng chi tiết Chứng từ:</Text>
            <View style={styles.twoCol}>
              <TextInput
                style={[styles.input, { flex: 1 }]}
                keyboardType="numeric"
                value={lineAmount}
                onChangeText={setLineAmount}
                placeholder={`Số tiền (${selectedDoc.currencyCode})`}
              />
              <TextInput
                style={[styles.input, { flex: 1.5 }]}
                value={lineDesc}
                onChangeText={setLineDesc}
                placeholder="Diễn giải dòng..."
              />
            </View>
            <TouchableOpacity
              style={styles.secondaryBtn}
              onPress={() => handleAddLine(selectedDoc)}
            >
              <Text style={styles.secondaryBtnText}>+ Thêm dòng chứng từ</Text>
            </TouchableOpacity>
          </View>

          <View style={styles.subBox}>
            <Text style={styles.label}>Hủy Chứng từ (Bắt buộc nhập lý do):</Text>
            <View style={styles.twoCol}>
              <TextInput
                style={[styles.input, { flex: 1 }]}
                value={cancelReason}
                onChangeText={setCancelReason}
                placeholder="Lý do hủy / sai thông tin..."
              />
              <TouchableOpacity
                style={styles.dangerBtn}
                onPress={() => handleCancelDocument(selectedDoc)}
              >
                <Text style={styles.dangerBtnText}>Hủy chứng từ</Text>
              </TouchableOpacity>
            </View>
          </View>

          <AttachmentGallery
            objectType="financial_document"
            objectId={selectedDoc.id}
            objectLabelVi={`Chứng từ ${selectedDoc.documentNo}`}
          />
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : (
        docs.map((doc) => (
          <TouchableOpacity
            key={doc.id}
            style={styles.card}
            onPress={() => setSelectedDoc(doc)}
          >
            <View style={styles.rowBetween}>
              <Text style={styles.docNo}>
                {doc.documentNo} • {doc.direction === "payable" ? "Phải trả (AP)" : "Phải thu (AR)"}
              </Text>
              <Text style={styles.amount}>
                {formatMoney(doc.totalAmount, doc.currencyCode)}
              </Text>
            </View>
            <View style={styles.axisRow}>
              <Text style={styles.axisChip}>
                1. {t(`receipt.${doc.receiptStatus}`, doc.receiptStatus)}
              </Text>
              <Text style={styles.axisChip}>
                2. {t(`acceptance.${doc.acceptanceStatus}`, doc.acceptanceStatus)}
              </Text>
              <Text style={styles.axisChip}>
                3. {t(`matching.${doc.matchingStatus}`, doc.matchingStatus)}
              </Text>
            </View>
          </TouchableOpacity>
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
  headerRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  heading: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
    flex: 1,
  },
  addBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 8,
  },
  addBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
  },
  sheet: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#CBD5E1",
    marginBottom: 14,
  },
  sheetTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 8,
  },
  closeLink: {
    fontSize: 12,
    fontWeight: "700",
    color: "#64748B",
  },
  label: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
    marginTop: 6,
    marginBottom: 4,
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
    gap: 8,
    alignItems: "center",
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
  primaryBtn: {
    backgroundColor: "#0F172A",
    paddingVertical: 11,
    borderRadius: 8,
    alignItems: "center",
    marginTop: 10,
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  actionGrid: {
    gap: 8,
    marginBottom: 8,
  },
  acceptBtn: {
    backgroundColor: "#047857",
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: "center",
  },
  acceptBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  matchBtn: {
    backgroundColor: "#1D4ED8",
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: "center",
  },
  matchBtnText: {
    color: "#FFFFFF",
    fontSize: 13,
    fontWeight: "700",
  },
  suggestionBox: {
    backgroundColor: "#EFF6FF",
    borderRadius: 8,
    padding: 10,
    marginVertical: 6,
  },
  suggestionItem: {
    fontSize: 12,
    color: "#1E3A8A",
    marginTop: 2,
  },
  subBox: {
    borderTopWidth: 1,
    borderTopColor: "#E2E8F0",
    paddingTop: 8,
    marginTop: 8,
  },
  secondaryBtn: {
    backgroundColor: "#F1F5F9",
    paddingVertical: 8,
    borderRadius: 8,
    alignItems: "center",
  },
  secondaryBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#0F172A",
  },
  dangerBtn: {
    backgroundColor: "#FEF2F2",
    borderWidth: 1,
    borderColor: "#FECACA",
    paddingHorizontal: 12,
    paddingVertical: 9,
    borderRadius: 8,
  },
  dangerBtnText: {
    color: "#DC2626",
    fontSize: 12,
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
  docNo: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
  },
  amount: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
  },
  axisRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginTop: 8,
  },
  axisChip: {
    backgroundColor: "#F1F5F9",
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
    fontSize: 11,
    color: "#334155",
    fontWeight: "600",
  },
});
