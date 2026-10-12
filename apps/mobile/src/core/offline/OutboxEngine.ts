import * as SQLite from "expo-sqlite";
import * as FileSystem from "expo-file-system/legacy";
import { Platform } from "react-native";
import { createIdempotencyKey } from "@lcms/shared";
import {
  configureDatabaseSecurity,
  getOrCreateDatabaseEncryptionKey,
} from "./DatabaseEncryption";
import { runOutboxDatabaseMigrations } from "./OutboxDatabaseMigration";

export type OutboxItemStatus =
  | "pending"
  | "syncing"
  | "conflict"
  | "failed"
  | "synced";

export interface OfflineOutboxItem {
  id: string;
  titleVi: string;
  endpoint: string;
  method: "POST" | "PUT" | "PATCH" | "DELETE";
  payloadJson: string;
  ifMatch?: string | null;
  idempotencyKey: string;
  status: OutboxItemStatus;
  errorMessageVi?: string | null;
  retryCount: number;
  localFileUri?: string | null;
  fileContentType?: string | null;
  fileName?: string | null;
  nextRetryAt?: string | null;
  lastAttemptAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface EnqueueOfflineMutationParams {
  titleVi: string;
  endpoint: string;
  method?: "POST" | "PUT" | "PATCH" | "DELETE";
  payload?: unknown;
  ifMatch?: string | null;
  idempotencyPrefix?: string;
  localFileUri?: string | null;
  fileContentType?: string | null;
  fileName?: string | null;
}

export interface OutboxStats {
  pendingCount: number;
  failedCount: number;
  conflictCount: number;
  syncedCount: number;
  totalCount: number;
}

const DB_NAME = "lcms_mobile_offline.db";
const ATTACHMENT_SUBDIR = "lcms_offline_attachments/";

let dbInstance: SQLite.SQLiteDatabase | null = null;
const memoryOutbox: OfflineOutboxItem[] = [];

export class OutboxEngine {
  private static instance: OutboxEngine;

  public static getInstance(): OutboxEngine {
    if (!OutboxEngine.instance) {
      OutboxEngine.instance = new OutboxEngine();
    }
    return OutboxEngine.instance;
  }

  /**
   * Khởi tạo kết nối SQLite với khóa bảo mật 256-bit và tự động chạy migration.
   */
  public async getDb(): Promise<SQLite.SQLiteDatabase | null> {
    if (Platform.OS === "web") {
      return null;
    }
    if (dbInstance) {
      return dbInstance;
    }

    try {
      const encryptionKey = await getOrCreateDatabaseEncryptionKey();
      dbInstance = await SQLite.openDatabaseAsync(DB_NAME);
      await configureDatabaseSecurity(dbInstance, encryptionKey);
      await runOutboxDatabaseMigrations(dbInstance);
      return dbInstance;
    } catch {
      return null;
    }
  }

  /**
   * Lưu tệp hóa đơn/chứng từ vào FileSystem của thiết bị (Anti-Blob-Bloat).
   * Tuyệt đối không lưu chuỗi Base64 vào cơ sở dữ liệu.
   */
  public async saveLocalAttachmentFile(
    sourceUri: string,
    originalFileName: string
  ): Promise<string> {
    if (Platform.OS === "web") {
      return sourceUri;
    }

    const docDir = FileSystem.documentDirectory;
    if (!docDir) {
      return sourceUri;
    }

    const targetDir = `${docDir}${ATTACHMENT_SUBDIR}`;
    const dirInfo = await FileSystem.getInfoAsync(targetDir);
    if (!dirInfo.exists) {
      await FileSystem.makeDirectoryAsync(targetDir, { intermediates: true });
    }

    const safeName = originalFileName.replace(/[^a-zA-Z0-9._-]/g, "_");
    const uniqueFileName = `${Date.now()}_${createIdempotencyKey("att")}_${safeName}`;
    const destinationUri = `${targetDir}${uniqueFileName}`;

    await FileSystem.copyAsync({
      from: sourceUri,
      to: destinationUri,
    });

    return destinationUri;
  }

  /**
   * Xóa tệp vật lý cục bộ sau khi đã đồng bộ thành công hoặc bị hủy.
   */
  public async cleanupLocalFile(localFileUri?: string | null): Promise<void> {
    if (!localFileUri || Platform.OS === "web") return;

    try {
      const fileInfo = await FileSystem.getInfoAsync(localFileUri);
      if (fileInfo.exists) {
        await FileSystem.deleteAsync(localFileUri, { idempotent: true });
      }
    } catch {
      // Bỏ qua lỗi xóa tệp để không gián đoạn luồng chính
    }
  }

  /**
   * Đưa một mutation vào hàng đợi Outbox.
   * Duy trì khóa Idempotency-Key (UUIDv4) bất biến.
   */
  public async enqueue(params: EnqueueOfflineMutationParams): Promise<OfflineOutboxItem> {
    const now = new Date().toISOString();
    const item: OfflineOutboxItem = {
      id: createIdempotencyKey("outbox"),
      titleVi: params.titleVi,
      endpoint: params.endpoint,
      method: params.method ?? "POST",
      payloadJson: JSON.stringify(params.payload ?? {}),
      ifMatch: params.ifMatch ?? null,
      idempotencyKey: createIdempotencyKey(params.idempotencyPrefix ?? "mob"),
      status: "pending",
      errorMessageVi: null,
      retryCount: 0,
      localFileUri: params.localFileUri ?? null,
      fileContentType: params.fileContentType ?? null,
      fileName: params.fileName ?? null,
      nextRetryAt: now,
      lastAttemptAt: null,
      createdAt: now,
      updatedAt: now,
    };

    const db = await this.getDb();
    if (!db) {
      memoryOutbox.unshift(item);
      return item;
    }

    await db.runAsync(
      `INSERT INTO offline_outbox (
        id, title_vi, endpoint, method, payload_json, if_match, idempotency_key,
        status, error_message_vi, retry_count, local_file_uri, file_content_type,
        file_name, next_retry_at, last_attempt_at, created_at, updated_at
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      item.id,
      item.titleVi,
      item.endpoint,
      item.method,
      item.payloadJson,
      item.ifMatch ?? null,
      item.idempotencyKey,
      item.status,
      item.errorMessageVi ?? null,
      item.retryCount,
      item.localFileUri ?? null,
      item.fileContentType ?? null,
      item.fileName ?? null,
      item.nextRetryAt ?? null,
      item.lastAttemptAt ?? null,
      item.createdAt,
      item.updatedAt
    );

    return item;
  }

  /**
   * Lấy danh sách các bản ghi trong Outbox.
   */
  public async listItems(options?: {
    status?: OutboxItemStatus;
    includeSynced?: boolean;
    onlyReadyToRetry?: boolean;
  }): Promise<OfflineOutboxItem[]> {
    const db = await this.getDb();
    if (!db) {
      let filtered = [...memoryOutbox];
      if (options?.status) {
        filtered = filtered.filter((i) => i.status === options.status);
      } else if (!options?.includeSynced) {
        filtered = filtered.filter((i) => i.status !== "synced");
      }
      return filtered;
    }

    let query = "SELECT * FROM offline_outbox";
    const whereClauses: string[] = [];
    const params: any[] = [];

    if (options?.status) {
      whereClauses.push("status = ?");
      params.push(options.status);
    } else if (!options?.includeSynced) {
      whereClauses.push("status != 'synced'");
    }

    if (options?.onlyReadyToRetry) {
      const nowIso = new Date().toISOString();
      whereClauses.push("(next_retry_at IS NULL OR next_retry_at <= ?)");
      params.push(nowIso);
    }

    if (whereClauses.length > 0) {
      query += ` WHERE ${whereClauses.join(" AND ")}`;
    }

    query += " ORDER BY created_at ASC";

    const rows = await db.getAllAsync<any>(query, ...params);
    return rows.map((r: any) => ({
      id: r.id,
      titleVi: r.title_vi,
      endpoint: r.endpoint,
      method: r.method,
      payloadJson: r.payload_json,
      ifMatch: r.if_match,
      idempotencyKey: r.idempotency_key,
      status: r.status,
      errorMessageVi: r.error_message_vi,
      retryCount: r.retry_count,
      localFileUri: r.local_file_uri,
      fileContentType: r.file_content_type,
      fileName: r.file_name,
      nextRetryAt: r.next_retry_at,
      lastAttemptAt: r.last_attempt_at,
      createdAt: r.created_at,
      updatedAt: r.updated_at,
    }));
  }

  /**
   * Lấy thông tin chi tiết một bản ghi theo ID.
   */
  public async getItemById(id: string): Promise<OfflineOutboxItem | null> {
    const db = await this.getDb();
    if (!db) {
      return memoryOutbox.find((x) => x.id === id) ?? null;
    }

    const row = await db.getFirstAsync<any>(
      "SELECT * FROM offline_outbox WHERE id = ?",
      id
    );
    if (!row) return null;

    return {
      id: row.id,
      titleVi: row.title_vi,
      endpoint: row.endpoint,
      method: row.method,
      payloadJson: row.payload_json,
      ifMatch: row.if_match,
      idempotencyKey: row.idempotency_key,
      status: row.status,
      errorMessageVi: row.error_message_vi,
      retryCount: row.retry_count,
      localFileUri: row.local_file_uri,
      fileContentType: row.file_content_type,
      fileName: row.file_name,
      nextRetryAt: row.next_retry_at,
      lastAttemptAt: row.last_attempt_at,
      createdAt: row.created_at,
      updatedAt: row.updated_at,
    };
  }

  /**
   * Cập nhật trạng thái và thông số retry của một bản ghi.
   */
  public async updateItem(
    id: string,
    updates: Partial<OfflineOutboxItem>
  ): Promise<void> {
    const now = new Date().toISOString();
    const db = await this.getDb();

    if (!db) {
      const item = memoryOutbox.find((x) => x.id === id);
      if (item) {
        Object.assign(item, updates, { updatedAt: now });
      }
      return;
    }

    const setClauses: string[] = ["updated_at = ?"];
    const params: any[] = [now];

    if (updates.status !== undefined) {
      setClauses.push("status = ?");
      params.push(updates.status);
    }
    if (updates.errorMessageVi !== undefined) {
      setClauses.push("error_message_vi = ?");
      params.push(updates.errorMessageVi);
    }
    if (updates.retryCount !== undefined) {
      setClauses.push("retry_count = ?");
      params.push(updates.retryCount);
    }
    if (updates.nextRetryAt !== undefined) {
      setClauses.push("next_retry_at = ?");
      params.push(updates.nextRetryAt);
    }
    if (updates.lastAttemptAt !== undefined) {
      setClauses.push("last_attempt_at = ?");
      params.push(updates.lastAttemptAt);
    }

    params.push(id);
    await db.runAsync(
      `UPDATE offline_outbox SET ${setClauses.join(", ")} WHERE id = ?`,
      ...params
    );
  }

  /**
   * Xóa bản ghi và dọn dẹp file vật lý cục bộ.
   */
  public async removeItem(id: string): Promise<void> {
    const item = await this.getItemById(id);
    if (item?.localFileUri) {
      await this.cleanupLocalFile(item.localFileUri);
    }

    const db = await this.getDb();
    if (!db) {
      const idx = memoryOutbox.findIndex((x) => x.id === id);
      if (idx >= 0) memoryOutbox.splice(idx, 1);
      return;
    }

    await db.runAsync("DELETE FROM offline_outbox WHERE id = ?", id);
  }

  /**
   * Dọn dẹp các bản ghi đã đồng bộ thành công sau một khoảng thời gian (mặc định 7 ngày).
   */
  public async purgeSynced(olderThanDays = 7): Promise<number> {
    const thresholdDate = new Date(
      Date.now() - olderThanDays * 24 * 60 * 60 * 1000
    ).toISOString();

    const db = await this.getDb();
    if (!db) {
      const initial = memoryOutbox.length;
      const keep = memoryOutbox.filter(
        (i) => i.status !== "synced" || i.updatedAt > thresholdDate
      );
      memoryOutbox.length = 0;
      memoryOutbox.push(...keep);
      return initial - memoryOutbox.length;
    }

    const result = await db.runAsync(
      "DELETE FROM offline_outbox WHERE status = 'synced' AND updated_at < ?",
      thresholdDate
    );
    return result.changes ?? 0;
  }

  /**
   * Thống kê số lượng theo từng trạng thái.
   */
  public async getStats(): Promise<OutboxStats> {
    const items = await this.listItems({ includeSynced: true });
    return {
      pendingCount: items.filter((i) => i.status === "pending").length,
      failedCount: items.filter((i) => i.status === "failed").length,
      conflictCount: items.filter((i) => i.status === "conflict").length,
      syncedCount: items.filter((i) => i.status === "synced").length,
      totalCount: items.length,
    };
  }
}
