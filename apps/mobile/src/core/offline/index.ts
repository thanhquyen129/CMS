export * from "./DatabaseEncryption";
export * from "./OutboxDatabaseMigration";
export * from "./OutboxEngine";
export * from "./ResilientSyncEngine";

import {
  EnqueueOfflineMutationParams,
  OfflineOutboxItem,
  OutboxEngine,
} from "./OutboxEngine";
import { ResilientSyncEngine, SyncResult } from "./ResilientSyncEngine";

const defaultOutbox = OutboxEngine.getInstance();
const defaultSync = ResilientSyncEngine.getInstance();

/**
 * Helper thêm mutation vào hàng đợi ngoại tuyến.
 */
export async function enqueueOfflineMutation(
  params: EnqueueOfflineMutationParams
): Promise<OfflineOutboxItem> {
  return await defaultOutbox.enqueue(params);
}

/**
 * Helper lấy danh sách các giao dịch trong Outbox.
 */
export async function listOfflineOutbox(): Promise<OfflineOutboxItem[]> {
  return await defaultOutbox.listItems({ includeSynced: false });
}

/**
 * Helper xóa giao dịch khỏi Outbox.
 */
export async function removeOfflineOutboxItem(id: string): Promise<void> {
  await defaultOutbox.removeItem(id);
}

/**
 * Helper thực hiện replay đồng bộ lại Outbox với thuật toán Backoff.
 */
export async function replayOfflineOutbox(): Promise<SyncResult> {
  return await defaultSync.triggerSync("manual");
}

/**
 * Kích hoạt lắng nghe mạng để tự động đồng bộ (Auto Background Sync).
 */
export function startAutoBackgroundSync(): () => void {
  return defaultSync.startAutoBackgroundSync();
}

