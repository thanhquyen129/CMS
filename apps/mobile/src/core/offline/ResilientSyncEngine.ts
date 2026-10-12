import NetInfo, { NetInfoState } from "@react-native-community/netinfo";
import { Platform } from "react-native";
import { apiRequest, LcmsApiException } from "../../api/client";
import { OfflineOutboxItem, OutboxEngine } from "./OutboxEngine";

export interface SyncResult {
  totalProcessed: number;
  syncedCount: number;
  conflictCount: number;
  authFailedCount: number;
  retryScheduledCount: number;
  errors: Array<{ id: string; errorVi: string }>;
}

export type SyncEventListener = (result: {
  isSyncing: boolean;
  lastSyncAt: string | null;
  lastResult?: SyncResult;
}) => void;

export class ResilientSyncEngine {
  private static instance: ResilientSyncEngine;
  private readonly outbox: OutboxEngine;
  private isReplaying = false;
  private unsubscribeNetInfo: (() => void) | null = null;
  private lastSyncAt: string | null = null;
  private readonly listeners: Set<SyncEventListener> = new Set();

  constructor(outbox?: OutboxEngine) {
    this.outbox = outbox ?? OutboxEngine.getInstance();
  }

  public static getInstance(): ResilientSyncEngine {
    if (!ResilientSyncEngine.instance) {
      ResilientSyncEngine.instance = new ResilientSyncEngine();
    }
    return ResilientSyncEngine.instance;
  }

  /**
   * Tính toán thời gian trễ theo công thức Exponential Backoff & Jitter:
   * T_wait = min(60s, 1.5s * 2^retry_count) + random_jitter (0-1000ms)
   */
  public computeBackoffDelay(retryCount: number): number {
    const baseDelayMs = 1500 * Math.pow(2, Math.max(0, retryCount));
    const cappedDelayMs = Math.min(60_000, baseDelayMs);
    const randomJitterMs = Math.floor(Math.random() * 1000);
    return Math.floor(cappedDelayMs + randomJitterMs);
  }

  /**
   * Đăng ký lắng nghe sự kiện mạng (Auto Background Sync).
   * Tự động kích hoạt đồng bộ khi thiết bị chuyển từ Offline -> Online.
   */
  public startAutoBackgroundSync(): () => void {
    if (this.unsubscribeNetInfo) {
      return this.unsubscribeNetInfo;
    }

    let wasOffline = false;

    this.unsubscribeNetInfo = NetInfo.addEventListener((state: NetInfoState) => {
      const isOnline = Boolean(state.isConnected && state.isInternetReachable !== false);

      if (!isOnline) {
        wasOffline = true;
      } else if (wasOffline) {
        wasOffline = false;
        // Mạng vừa phục hồi -> kích hoạt worker đồng bộ ngầm
        void this.triggerSync("network_restored");
      }
    });

    return () => {
      if (this.unsubscribeNetInfo) {
        this.unsubscribeNetInfo();
        this.unsubscribeNetInfo = null;
      }
    };
  }

  public subscribe(listener: SyncEventListener): () => void {
    this.listeners.add(listener);
    listener({
      isSyncing: this.isReplaying,
      lastSyncAt: this.lastSyncAt,
    });
    return () => {
      this.listeners.delete(listener);
    };
  }

  private notifyListeners(lastResult?: SyncResult) {
    for (const listener of this.listeners) {
      listener({
        isSyncing: this.isReplaying,
        lastSyncAt: this.lastSyncAt,
        lastResult,
      });
    }
  }

  /**
   * Kích hoạt tiến trình đồng bộ các bản ghi trong Outbox.
   * Áp dụng khóa Concurrency Lock để đảm bảo tính tuần tự.
   */
  public async triggerSync(
    triggerSource: "auto" | "network_restored" | "manual" = "manual"
  ): Promise<SyncResult> {
    if (this.isReplaying) {
      return {
        totalProcessed: 0,
        syncedCount: 0,
        conflictCount: 0,
        authFailedCount: 0,
        retryScheduledCount: 0,
        errors: [],
      };
    }

    this.isReplaying = true;
    this.notifyListeners();

    const summary: SyncResult = {
      totalProcessed: 0,
      syncedCount: 0,
      conflictCount: 0,
      authFailedCount: 0,
      retryScheduledCount: 0,
      errors: [],
    };

    try {
      // Chỉ lấy các bản ghi sẵn sàng retry (next_retry_at <= now hoặc null)
      const allItems = await this.outbox.listItems({ onlyReadyToRetry: true });
      const pendingItems = allItems.filter(
        (i) => i.status === "pending" || i.status === "failed"
      );

      summary.totalProcessed = pendingItems.length;

      for (const item of pendingItems) {
        const itemResult = await this.syncSingleItem(item);
        if (itemResult.success) {
          summary.syncedCount++;
        } else if (itemResult.isConflict) {
          summary.conflictCount++;
          summary.errors.push({ id: item.id, errorVi: itemResult.errorVi });
        } else if (itemResult.isAuthError) {
          summary.authFailedCount++;
          summary.errors.push({ id: item.id, errorVi: itemResult.errorVi });
          // Khi lỗi xác thực (401), dừng toàn bộ đợt replay vì các request sau cũng sẽ 401
          break;
        } else {
          summary.retryScheduledCount++;
          summary.errors.push({ id: item.id, errorVi: itemResult.errorVi });
        }
      }

      this.lastSyncAt = new Date().toISOString();
      return summary;
    } finally {
      this.isReplaying = false;
      this.notifyListeners(summary);
    }
  }

  /**
   * Đồng bộ một mutation đơn lẻ với xử lý phân loại lỗi và Exponential Backoff.
   */
  private async syncSingleItem(
    item: OfflineOutboxItem
  ): Promise<{
    success: boolean;
    isConflict?: boolean;
    isAuthError?: boolean;
    errorVi: string;
  }> {
    const nowIso = new Date().toISOString();
    await this.outbox.updateItem(item.id, {
      status: "syncing",
      lastAttemptAt: nowIso,
    });

    try {
      // 1. Nếu có tệp đính kèm cục bộ (local_file_uri), gửi dưới dạng multipart upload
      if (item.localFileUri) {
        await this.uploadLocalAttachment(item);
      } else {
        // 2. Mutation JSON thông thường (/api/costs, /api/bills, v.v.)
        const payload = JSON.parse(item.payloadJson || "{}");
        await apiRequest(item.endpoint, {
          method: item.method,
          body: payload,
          ifMatch: item.ifMatch,
          idempotencyKey: item.idempotencyKey, // Duy trì khóa UUIDv4 bất biến
        });
      }

      // Thành công -> cập nhật trạng thái 'synced' và dọn dẹp file vật lý
      await this.outbox.updateItem(item.id, {
        status: "synced",
        errorMessageVi: null,
      });

      if (item.localFileUri) {
        await this.outbox.cleanupLocalFile(item.localFileUri);
      }

      return { success: true, errorVi: "" };
    } catch (err: unknown) {
      // Xử lý phân loại lỗi:
      const httpStatus =
        err instanceof LcmsApiException
          ? err.parsed.status
          : (err as any)?.status ?? 0;

      const isConflict =
        (err instanceof LcmsApiException && err.parsed.isConcurrencyConflict) ||
        httpStatus === 409;

      const isAuthError = httpStatus === 401;

      // 1. Lỗi HTTP 401 (Unauthorized): Hết phiên đăng nhập -> DỪNG RETRY
      if (isAuthError) {
        const errorVi =
          "Phiên làm việc đã hết hạn (HTTP 401). Đã tạm dừng đồng bộ để tránh khóa tài khoản. Vui lòng đăng nhập lại.";
        await this.outbox.updateItem(item.id, {
          status: "failed",
          errorMessageVi: errorVi,
          nextRetryAt: null, // Không tự động retry
        });
        return { success: false, isAuthError: true, errorVi };
      }

      // 2. Lỗi HTTP 409 (Conflict): Xung đột tương tranh Optimistic Concurrency -> DỪNG RETRY
      if (isConflict) {
        const errorVi =
          "Xung đột phiên bản dữ liệu (HTTP 409). Bản ghi trên máy chủ đã được cập nhật bởi phiên làm việc khác. Cần kiểm tra đối soát thủ công.";
        await this.outbox.updateItem(item.id, {
          status: "conflict",
          errorMessageVi: errorVi,
          retryCount: item.retryCount + 1,
          nextRetryAt: null, // Không tự động retry đè
        });
        return { success: false, isConflict: true, errorVi };
      }

      // 3. Lỗi mạng / 5xx / 429 -> Áp dụng Exponential Backoff & Jitter
      const nextRetryCount = item.retryCount + 1;
      const delayMs = this.computeBackoffDelay(nextRetryCount);
      const nextRetryAt = new Date(Date.now() + delayMs).toISOString();

      const errorVi =
        err instanceof Error
          ? err.message
          : "Lỗi kết nối máy chủ hoặc gián đoạn đường truyền mạng.";

      await this.outbox.updateItem(item.id, {
        status: "failed",
        errorMessageVi: `${errorVi} (Thử lại sau ${Math.round(delayMs / 1000)}s)`,
        retryCount: nextRetryCount,
        nextRetryAt,
      });

      return { success: false, errorVi };
    }
  }

  /**
   * Upload tệp đính kèm vật lý lưu trữ trong FileSystem lên API /api/attachments.
   */
  private async uploadLocalAttachment(item: OfflineOutboxItem): Promise<void> {
    const payload = JSON.parse(item.payloadJson || "{}");
    const formData = new FormData();

    if (payload.objectType) formData.append("objectType", payload.objectType);
    if (payload.objectId) formData.append("objectId", payload.objectId);
    if (payload.notes) formData.append("notes", payload.notes);

    const fileUri = item.localFileUri!;
    const fileName = item.fileName || "attachment.jpg";
    const fileType = item.fileContentType || "image/jpeg";

    formData.append("file", {
      uri: fileUri,
      name: fileName,
      type: fileType,
    } as any);

    await apiRequest(item.endpoint, {
      method: "POST",
      body: formData,
      idempotencyKey: item.idempotencyKey,
    });
  }
}
