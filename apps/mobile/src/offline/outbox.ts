/**
 * Backward compatibility facade.
 * Delegates to the production-grade core offline engine in apps/mobile/src/core/offline.
 */
export * from "../core/offline";

import {
  enqueueOfflineMutation as coreEnqueue,
  listOfflineOutbox as coreList,
  removeOfflineOutboxItem as coreRemove,
  replayOfflineOutbox as coreReplay,
} from "../core/offline";

export interface OutboxReplaySummary {
  syncedCount: number;
  conflictCount: number;
  failedCount: number;
}

export async function replayOfflineOutbox(): Promise<OutboxReplaySummary> {
  const res = await coreReplay();
  return {
    syncedCount: res.syncedCount,
    conflictCount: res.conflictCount,
    failedCount: res.authFailedCount + res.retryScheduledCount,
  };
}

export const enqueueOfflineMutation = coreEnqueue;
export const listOfflineOutbox = coreList;
export const removeOfflineOutboxItem = coreRemove;
