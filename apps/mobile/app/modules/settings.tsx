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
import { apiRequest } from "../../src/api/client";
import { useAuth } from "../../src/auth/AuthContext";

interface TenantProfileDto {
  id?: string;
  code?: string;
  name?: string;
  legalName?: string | null;
  taxId?: string | null;
  defaultCurrencyCode?: string;
  timeZoneId?: string;
  dateFormat?: string;
}

interface TenantReadinessDto {
  isReady?: boolean;
  missingSteps?: string[];
}

interface AuditEventDto {
  id: string;
  eventTime?: string;
  occurredAt?: string;
  actorId?: string | null;
  actorName?: string | null;
  action: string;
  objectType: string;
  objectId?: string | null;
  correlationId?: string | null;
  changesJson?: string | null;
  reason?: string | null;
}

interface NotificationInboxItemDto {
  id: string;
  title?: string;
  message?: string;
  body?: string;
  category?: string;
  isRead?: boolean;
  createdAt?: string;
}

interface IntegrationJobHealthDto {
  outboxPending?: number;
  integrationErrorsPending?: number;
  integrationErrorsDeadLetter?: number;
}

interface TenantBackupDto {
  id: string;
  createdAt?: string;
  createdBy?: string | null;
  note?: string | null;
  sizeBytes?: number;
  status?: string;
}

export default function SettingsAdminModuleScreen() {
  const { bootstrap, pushToken, refreshBootstrap } = useAuth();
  const [subTab, setSubTab] = useState<
    "tenant" | "audit" | "notifications" | "backups"
  >("audit");
  const [loading, setLoading] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState<boolean>(false);

  const [profile, setProfile] = useState<TenantProfileDto | null>(null);
  const [readiness, setReadiness] = useState<TenantReadinessDto | null>(null);
  const [auditEvents, setAuditEvents] = useState<AuditEventDto[]>([]);
  const [auditFilter, setAuditFilter] = useState<string>("ALL");
  const [inbox, setInbox] = useState<NotificationInboxItemDto[]>([]);
  const [jobHealth, setJobHealth] = useState<IntegrationJobHealthDto | null>(
    null
  );
  const [backups, setBackups] = useState<TenantBackupDto[]>([]);
  const [backupNote, setBackupNote] = useState<string>("");

  const loadAll = useCallback(async () => {
    setLoading(true);
    try {
      const objQuery =
        auditFilter === "ALL" ? "?take=60" : `?objectType=${auditFilter}&take=60`;

      const [profRes, readyRes, auditRes, inboxRes, healthRes, backupRes] =
        await Promise.all([
          apiRequest<TenantProfileDto>("/api/tenant-profile").catch(() => null),
          apiRequest<TenantReadinessDto>("/api/tenant-profile/readiness").catch(
            () => null
          ),
          apiRequest<AuditEventDto[] | { items: AuditEventDto[] }>(
            `/api/audit-events${objQuery}`
          ).catch(() => [] as AuditEventDto[]),
          apiRequest<
            NotificationInboxItemDto[] | { items: NotificationInboxItemDto[] }
          >("/api/notifications/inbox?take=40").catch(
            () => [] as NotificationInboxItemDto[]
          ),
          apiRequest<IntegrationJobHealthDto>(
            "/api/integration-errors/job-health"
          ).catch(() => null),
          apiRequest<TenantBackupDto[] | { items: TenantBackupDto[] }>(
            "/api/tenant-backups"
          ).catch(() => [] as TenantBackupDto[]),
        ]);

      setProfile(profRes);
      setReadiness(readyRes);
      setAuditEvents(
        Array.isArray(auditRes) ? auditRes : auditRes?.items ?? []
      );
      setInbox(Array.isArray(inboxRes) ? inboxRes : inboxRes?.items ?? []);
      setJobHealth(healthRes);
      setBackups(Array.isArray(backupRes) ? backupRes : backupRes?.items ?? []);
    } finally {
      setLoading(false);
    }
  }, [auditFilter]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const handleMarkAllRead = async () => {
    setSubmitting(true);
    try {
      await apiRequest("/api/notifications/inbox/read-all", {
        method: "POST",
        body: {},
      });
      await Promise.all([loadAll(), refreshBootstrap()]);
      Alert.alert("Đã cập nhật", "Đã đánh dấu đọc toàn bộ thông báo.");
    } catch (err) {
      Alert.alert(
        "Lỗi thông báo",
        err instanceof Error ? err.message : "Không thể đánh dấu đã đọc."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreateBackup = async () => {
    setSubmitting(true);
    try {
      await apiRequest("/api/tenant-backups", {
        method: "POST",
        body: {
          note:
            backupNote.trim() ||
            "Bản sao lưu dữ liệu thuê bao khởi tạo từ ứng dụng di động",
        },
      });
      setBackupNote("");
      await loadAll();
      Alert.alert(
        "Đã tạo Bản sao lưu",
        "Hệ thống đã lưu trữ bản sao lưu dữ liệu doanh nghiệp an toàn trên máy chủ."
      );
    } catch (err) {
      Alert.alert(
        "Lỗi sao lưu",
        err instanceof Error ? err.message : "Không thể tạo bản sao lưu."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl refreshing={loading} onRefresh={() => void loadAll()} />
      }
    >
      <View style={styles.tabRow}>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "audit" && styles.tabBtnActive]}
          onPress={() => setSubTab("audit")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "audit" && styles.tabBtnTextActive,
            ]}
          >
            🛡️ Nhật ký ({auditEvents.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "tenant" && styles.tabBtnActive]}
          onPress={() => setSubTab("tenant")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "tenant" && styles.tabBtnTextActive,
            ]}
          >
            🏢 Doanh nghiệp
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.tabBtn,
            subTab === "notifications" && styles.tabBtnActive,
          ]}
          onPress={() => setSubTab("notifications")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "notifications" && styles.tabBtnTextActive,
            ]}
          >
            🔔 Hộp thư ({inbox.length})
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabBtn, subTab === "backups" && styles.tabBtnActive]}
          onPress={() => setSubTab("backups")}
        >
          <Text
            style={[
              styles.tabBtnText,
              subTab === "backups" && styles.tabBtnTextActive,
            ]}
          >
            💾 Sao lưu
          </Text>
        </TouchableOpacity>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color="#0F172A" style={{ marginTop: 28 }} />
      ) : subTab === "audit" ? (
        <>
          <Text style={styles.subHeader}>
            Nhật ký Kiểm soát &amp; Tuân thủ Bất biến
          </Text>
          <View style={styles.chipRow}>
            {(
              [
                { k: "ALL", l: "Tất cả" },
                { k: "bill", l: "Vận đơn" },
                { k: "cost", l: "Chi phí" },
                { k: "revenue", l: "Doanh thu" },
                { k: "accounts_payable", l: "AP" },
                { k: "accounts_receivable", l: "AR" },
              ] as const
            ).map((f) => (
              <TouchableOpacity
                key={f.k}
                style={[styles.chip, auditFilter === f.k && styles.chipActive]}
                onPress={() => setAuditFilter(f.k)}
              >
                <Text
                  style={[
                    styles.chipText,
                    auditFilter === f.k && styles.chipTextActive,
                  ]}
                >
                  {f.l}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {auditEvents.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>
                Chưa có bản ghi kiểm toán phù hợp
              </Text>
            </View>
          ) : (
            auditEvents.map((ev) => (
              <View key={ev.id} style={styles.card}>
                <View style={styles.rowBetween}>
                  <Text style={styles.cardCode}>
                    {ev.action} • {ev.objectType}
                  </Text>
                  <Text style={styles.metaText}>
                    {(ev.eventTime ?? ev.occurredAt ?? "").slice(0, 16)}
                  </Text>
                </View>
                <Text style={styles.metaText}>
                  Thực hiện bởi: {ev.actorName ?? ev.actorId ?? "System"} • Mã đối tượng:{" "}
                  {ev.objectId ? ev.objectId.slice(0, 8) : "—"}
                </Text>
                {ev.changesJson ? (
                  <Text style={styles.jsonPreview} numberOfLines={3}>
                    {ev.changesJson}
                  </Text>
                ) : null}
              </View>
            ))
          )}
        </>
      ) : subTab === "tenant" ? (
        <View style={styles.card}>
          <Text style={styles.formTitle}>
            Hồ sơ Thuê bao &amp; Chính sách Tài chính
          </Text>
          <Text style={styles.metaText}>
            • Tên đơn vị: {profile?.name ?? bootstrap?.tenant.name ?? "LCMS Tenant"}
          </Text>
          <Text style={styles.metaText}>
            • Pháp nhân: {profile?.legalName ?? "—"} | MST:{" "}
            {profile?.taxId ?? "—"}
          </Text>
          <Text style={styles.metaText}>
            • Đồng tiền báo cáo gốc:{" "}
            {profile?.defaultCurrencyCode ??
              bootstrap?.tenant.defaultCurrencyCode ??
              "VND"}
          </Text>
          <Text style={styles.metaText}>
            • Múi giờ:{" "}
            {profile?.timeZoneId ??
              bootstrap?.tenant.timeZoneId ??
              "Asia/Ho_Chi_Minh"}
          </Text>
          <Text style={styles.metaText}>
            • Định dạng ngày:{" "}
            {profile?.dateFormat ?? bootstrap?.tenant.dateFormat ?? "dd/MM/yyyy"}
          </Text>
          <Text style={styles.metaText}>
            • Lưu trữ chứng từ số: Hệ thống lưu trữ bảo mật trên máy chủ chuyên dụng
          </Text>

          {readiness ? (
            <View style={styles.infoBox}>
              <Text style={styles.subHeader}>
                Trạng thái Sẵn sàng Vận hành:{" "}
                {readiness.isReady ? "✓ Đã hoàn tất" : "Đang cấu hình"}
              </Text>
              {(readiness.missingSteps ?? []).map((s, i) => (
                <Text key={i} style={styles.metaText}>
                  • {s}
                </Text>
              ))}
            </View>
          ) : null}
        </View>
      ) : subTab === "notifications" ? (
        <>
          <View style={styles.card}>
            <Text style={styles.subHeader}>Thiết bị Push Notification Hiện tại</Text>
            <Text style={styles.metaText}>
              Push Token: {pushToken ?? "Chưa cấp quyền Push Token"}
            </Text>
          </View>

          <View style={styles.rowBetween}>
            <Text style={styles.subHeader}>
              Hộp thư Cảnh báo Hệ thống ({inbox.length})
            </Text>
            <TouchableOpacity
              style={styles.primaryBtn}
              onPress={handleMarkAllRead}
              disabled={submitting}
            >
              <Text style={styles.primaryBtnText}>✓ Đọc tất cả</Text>
            </TouchableOpacity>
          </View>

          {inbox.length === 0 ? (
            <View style={styles.emptyCard}>
              <Text style={styles.emptyTitle}>Hộp thư thông báo trống</Text>
            </View>
          ) : (
            inbox.map((n) => (
              <View
                key={n.id}
                style={[
                  styles.card,
                  !n.isRead && { borderColor: "#93C5FD", backgroundColor: "#EFF6FF" },
                ]}
              >
                <Text style={styles.cardCode}>
                  {n.title ?? n.category ?? "Thông báo hệ thống"}
                </Text>
                <Text style={styles.metaText}>
                  {n.message ?? n.body ?? ""}
                </Text>
              </View>
            ))
          )}
        </>
      ) : (
        <>
          <View style={styles.card}>
            <Text style={styles.subHeader}>
              Trạng thái Đồng bộ &amp; Tích hợp Dữ liệu
            </Text>
            <Text style={styles.metaText}>
              • Hàng đợi đồng bộ: {jobHealth?.outboxPending ?? 0} | Lỗi xử lý:{" "}
              {jobHealth?.integrationErrorsPending ?? 0} | Tồn đọng:{" "}
              {jobHealth?.integrationErrorsDeadLetter ?? 0}
            </Text>
          </View>

          <View style={styles.card}>
            <Text style={styles.formTitle}>
              Sao lưu Dữ liệu Doanh nghiệp An toàn
            </Text>
            <TextInput
              style={styles.input}
              placeholder="Ghi chú bản sao lưu (VD: Trước khi chốt sổ tháng 09)..."
              value={backupNote}
              onChangeText={setBackupNote}
            />
            <TouchableOpacity
              style={styles.submitBtn}
              onPress={handleCreateBackup}
              disabled={submitting}
            >
              <Text style={styles.submitBtnText}>
                💾 Tạo Bản Sao lưu Ngay
              </Text>
            </TouchableOpacity>
          </View>

          {backups.map((b) => (
            <View key={b.id} style={styles.card}>
              <View style={styles.rowBetween}>
                <Text style={styles.cardCode}>Backup #{b.id.slice(0, 8)}</Text>
                <Text style={styles.metaText}>{b.status ?? "completed"}</Text>
              </View>
              <Text style={styles.metaText}>
                Thời gian: {b.createdAt ? b.createdAt.slice(0, 16) : "—"} •{" "}
                {b.note ?? "Sao lưu định kỳ"}
              </Text>
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
  subHeader: {
    fontSize: 13,
    fontWeight: "800",
    color: "#1E293B",
    marginVertical: 6,
  },
  formTitle: {
    fontSize: 15,
    fontWeight: "800",
    color: "#0F172A",
    marginBottom: 6,
  },
  chipRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginBottom: 10,
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
    marginBottom: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  infoBox: {
    backgroundColor: "#F8FAFC",
    padding: 10,
    borderRadius: 8,
    marginTop: 10,
    borderWidth: 1,
    borderColor: "#E2E8F0",
  },
  rowBetween: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  cardCode: {
    fontSize: 13,
    fontWeight: "800",
    color: "#0F172A",
  },
  metaText: {
    fontSize: 12,
    color: "#475569",
    marginTop: 4,
  },
  jsonPreview: {
    fontSize: 11,
    color: "#334155",
    backgroundColor: "#F8FAFC",
    padding: 6,
    borderRadius: 6,
    marginTop: 6,
    fontFamily: "monospace",
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
    marginTop: 6,
  },
  primaryBtn: {
    backgroundColor: "#2563EB",
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 8,
  },
  primaryBtnText: {
    color: "#FFFFFF",
    fontSize: 12,
    fontWeight: "700",
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

