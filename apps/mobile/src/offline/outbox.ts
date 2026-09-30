import * as SQLite from "expo-sqlite";
import { Platform } from "react-native";
import { createIdempotencyKey } from "@lcms/shared";
import { apiRequest, LcmsApiException } from "../api/client";

export type OutboxItemStatus = "pending" | "syncing" | "conflict" | "failed" | "synced";

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
  createdAt: string;
  updatedAt: string;
}

let dbInstance: SQLite.SQLiteDatabase | null = null;
const memoryOutbox: OfflineOutboxItem[] = [];

async function getDb(): Promise<SQLite.SQLiteDatabase | null> {
  if (Platform.OS === "web") {
    return null;
  }
  if (dbInstance) {
    return dbInstance;
  }
  dbInstance = await SQLite.openDatabaseAsync("lcms_mobile_offline.db");
  await dbInstance.execAsync(`
    CREATE TABLE IF NOT EXISTS offline_outbox (
      id TEXT PRIMARY KEY NOT NULL,
      title_vi TEXT NOT NULL,
      endpoint TEXT NOT NULL,
      method TEXT NOT NULL,
      payload_json TEXT NOT NULL,
      if_match TEXT,
      idempotency_key TEXT NOT NULL,
      status TEXT NOT NULL,
      error_message_vi TEXT,
      retry_count INTEGER NOT NULL DEFAULT 0,
      created_at TEXT NOT NULL,
      updated_at TEXT NOT NULL
    );
  `);
  return dbInstance;
}

export async function enqueueOfflineMutation(params: {
  titleVi: string;
  endpoint: string;
  method?: "POST" | "PUT" | "PATCH" | "DELETE";
  payload: unknown;
  ifMatch?: string | null;
  idempotencyPrefix?: string;
}): Promise<OfflineOutboxItem> {
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
    createdAt: now,
    updatedAt: now,
  };

  const db = await getDb();
  if (!db) {
    memoryOutbox.unshift(item);
    return item;
  }

  await db.runAsync(
    `INSERT INTO offline_outbox (
      id, title_vi, endpoint, method, payload_json, if_match, idempotency_key,
      status, error_message_vi, retry_count, created_at, updated_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
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
    item.createdAt,
    item.updatedAt
  );

  return item;
}

export async function listOfflineOutbox(): Promise<OfflineOutboxItem[]> {
  const db = await getDb();
  if (!db) {
    return [...memoryOutbox];
  }

  const rows = await db.getAllAsync<{
    id: string;
    title_vi: string;
    endpoint: string;
    method: "POST" | "PUT" | "PATCH" | "DELETE";
    payload_json: string;
    if_match: string | null;
    idempotency_key: string;
    status: OutboxItemStatus;
    error_message_vi: string | null;
    retry_count: number;
    created_at: string;
    updated_at: string;
  }>(`SELECT * FROM offline_outbox WHERE status != 'synced' ORDER BY created_at DESC`);

  return rows.map((r) => ({
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
    createdAt: r.created_at,
    updatedAt: r.updated_at,
  }));
}

export async function removeOfflineOutboxItem(id: string): Promise<void> {
  const db = await getDb();
  if (!db) {
    const idx = memoryOutbox.findIndex((x) => x.id === id);
    if (idx >= 0) memoryOutbox.splice(idx, 1);
    return;
  }
  await db.runAsync(`DELETE FROM offline_outbox WHERE id = ?`, id);
}

export interface OutboxReplaySummary {
  syncedCount: number;
  conflictCount: number;
  failedCount: number;
}

/**
 * Replays pending offline mutations in chronological order with deterministic Idempotency-Key
 * and If-Match (rowVersion). Marks 409 concurrency conflicts as 'conflict' so the operator
 * can inspect and resolve rather than overwriting newer server state.
 */
export async function replayOfflineOutbox(): Promise<OutboxReplaySummary> {
  const items = await listOfflineOutbox();
  const pending = items
    .filter((i) => i.status === "pending" || i.status === "failed")
    .reverse();

  let syncedCount = 0;
  let conflictCount = 0;
  let failedCount = 0;
  const db = await getDb();

  for (const item of pending) {
    try {
      const payload = JSON.parse(item.payloadJson);
      await apiRequest(item.endpoint, {
        method: item.method,
        body: payload,
        ifMatch: item.ifMatch,
        idempotencyKey: item.idempotencyKey,
      });

      syncedCount++;
      if (!db) {
        const idx = memoryOutbox.findIndex((x) => x.id === item.id);
        if (idx >= 0) memoryOutbox.splice(idx, 1);
      } else {
        await db.runAsync(
          `UPDATE offline_outbox SET status = 'synced', updated_at = ? WHERE id = ?`,
          new Date().toISOString(),
          item.id
        );
      }
    } catch (err) {
      const now = new Date().toISOString();
      if (err instanceof LcmsApiException && err.parsed.isConcurrencyConflict) {
        conflictCount++;
        const msg = err.parsed.messageVi;
        if (!db) {
          const target = memoryOutbox.find((x) => x.id === item.id);
          if (target) {
            target.status = "conflict";
            target.errorMessageVi = msg;
            target.updatedAt = now;
          }
        } else {
          await db.runAsync(
            `UPDATE offline_outbox SET status = 'conflict', error_message_vi = ?, retry_count = retry_count + 1, updated_at = ? WHERE id = ?`,
            msg,
            now,
            item.id
          );
        }
      } else {
        failedCount++;
        const msg = err instanceof Error ? err.message : "Mất kết nối mạng hoặc lỗi máy chủ.";
        if (!db) {
          const target = memoryOutbox.find((x) => x.id === item.id);
          if (target) {
            target.status = "failed";
            target.errorMessageVi = msg;
            target.retryCount += 1;
            target.updatedAt = now;
          }
        } else {
          await db.runAsync(
            `UPDATE offline_outbox SET status = 'failed', error_message_vi = ?, retry_count = retry_count + 1, updated_at = ? WHERE id = ?`,
            msg,
            now,
            item.id
          );
        }
      }
    }
  }

  return { syncedCount, conflictCount, failedCount };
}

