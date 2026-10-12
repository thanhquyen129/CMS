jest.mock("@react-native-community/netinfo", () => ({
  addEventListener: jest.fn(),
  fetch: jest.fn().mockResolvedValue({ isConnected: true, isInternetReachable: true }),
}));

jest.mock("react-native", () => ({
  Platform: { OS: "ios" },
}));

jest.mock("expo-secure-store", () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));

jest.mock("expo-sqlite", () => ({
  openDatabaseAsync: jest.fn(),
}));

jest.mock("expo-file-system/legacy", () => ({
  documentDirectory: "file:///mock/documents/",
  getInfoAsync: jest.fn().mockResolvedValue({ exists: true }),
  makeDirectoryAsync: jest.fn().mockResolvedValue(undefined),
  copyAsync: jest.fn().mockResolvedValue(undefined),
  deleteAsync: jest.fn().mockResolvedValue(undefined),
}));

export class MockLcmsApiException extends Error {
  public readonly parsed: any;
  constructor(parsed: any) {
    super(parsed.messageVi);
    this.name = "LcmsApiException";
    this.parsed = parsed;
  }
}

jest.mock("../../../api/client", () => ({
  apiRequest: jest.fn(),
  LcmsApiException: MockLcmsApiException,
}));

import { ResilientSyncEngine } from "../ResilientSyncEngine";
import { OutboxEngine, OfflineOutboxItem } from "../OutboxEngine";
import { apiRequest } from "../../../api/client";

const mockApiRequest = apiRequest as jest.MockedFunction<typeof apiRequest>;

describe("ResilientSyncEngine (Commercial Offline Outbox Tests)", () => {
  let syncEngine: ResilientSyncEngine;
  let mockOutbox: OutboxEngine;
  let inMemoryItems: OfflineOutboxItem[];

  beforeEach(() => {
    jest.clearAllMocks();

    inMemoryItems = [];

    mockOutbox = {
      listItems: jest.fn().mockImplementation(async () => [...inMemoryItems]),
      updateItem: jest.fn().mockImplementation(async (id: string, updates: Partial<OfflineOutboxItem>) => {
        const item = inMemoryItems.find((x) => x.id === id);
        if (item) Object.assign(item, updates);
      }),
      cleanupLocalFile: jest.fn().mockResolvedValue(undefined),
    } as unknown as OutboxEngine;

    syncEngine = new ResilientSyncEngine(mockOutbox);
  });

  describe("Thuật toán Exponential Backoff & Jitter", () => {
    it("phải tính toán độ trễ cơ sở 1.5s và cộng thêm Jitter ngẫu nhiên cho retryCount = 0", () => {
      for (let i = 0; i < 20; i++) {
        const delay = syncEngine.computeBackoffDelay(0);
        // Base: 1500ms, Jitter: [0, 1000) => [1500, 2500]
        expect(delay).toBeGreaterThanOrEqual(1500);
        expect(delay).toBeLessThanOrEqual(2500);
      }
    });

    it("phải tăng độ trễ lũy tiến theo số mũ: 3s cho lần 1, 6s cho lần 2", () => {
      const delay1 = syncEngine.computeBackoffDelay(1);
      expect(delay1).toBeGreaterThanOrEqual(3000);
      expect(delay1).toBeLessThanOrEqual(4000);

      const delay2 = syncEngine.computeBackoffDelay(2);
      expect(delay2).toBeGreaterThanOrEqual(6000);
      expect(delay2).toBeLessThanOrEqual(7000);
    });

    it("phải kẹp trần tối đa ở 60s cho các lần thử lớn (retryCount >= 6)", () => {
      const delay6 = syncEngine.computeBackoffDelay(6); // 1.5 * 64 = 96s -> kẹp 60s
      expect(delay6).toBeGreaterThanOrEqual(60000);
      expect(delay6).toBeLessThanOrEqual(61000);

      const delay10 = syncEngine.computeBackoffDelay(10);
      expect(delay10).toBeGreaterThanOrEqual(60000);
      expect(delay10).toBeLessThanOrEqual(61000);
    });
  });

  describe("Replay Logic & Phân loại lỗi nghiêm ngặt", () => {
    it("phải đồng bộ thành công và giữ nguyên Idempotency-Key (UUIDv4)", async () => {
      const fixedIdempotencyKey = "mob-550e8400-e29b-41d4-a716-446655440000";
      inMemoryItems = [
        {
          id: "item-1",
          titleVi: "Tạo phiếu chi 2,500,000 VND",
          endpoint: "/api/costs",
          method: "POST",
          payloadJson: JSON.stringify({ amount: 2500000 }),
          idempotencyKey: fixedIdempotencyKey,
          status: "pending",
          retryCount: 0,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ];

      mockApiRequest.mockResolvedValueOnce({ id: "server-cost-1" });

      const result = await syncEngine.triggerSync("manual");

      expect(result.syncedCount).toBe(1);
      expect(result.conflictCount).toBe(0);
      expect(mockApiRequest).toHaveBeenCalledWith("/api/costs", {
        method: "POST",
        body: { amount: 2500000 },
        ifMatch: undefined,
        idempotencyKey: fixedIdempotencyKey, // Duy trì khóa UUIDv4 bất biến
      });

      expect(mockOutbox.updateItem).toHaveBeenCalledWith(
        "item-1",
        expect.objectContaining({ status: "synced", errorMessageVi: null })
      );
    });

    it("phải DỪNG RETRY ngay lập tức khi gặp lỗi HTTP 401 (Unauthorized)", async () => {
      inMemoryItems = [
        {
          id: "item-auth-fail",
          titleVi: "Chi phí",
          endpoint: "/api/costs",
          method: "POST",
          payloadJson: "{}",
          idempotencyKey: "mob-key-1",
          status: "pending",
          retryCount: 0,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ];

      const authError = new MockLcmsApiException({
        status: 401,
        code: "unauthorized",
        messageVi: "Hết phiên",
        isConcurrencyConflict: false,
        isRatingNotReady: false,
      });

      mockApiRequest.mockRejectedValueOnce(authError);

      const result = await syncEngine.triggerSync("manual");

      expect(result.authFailedCount).toBe(1);
      expect(result.syncedCount).toBe(0);
      expect(mockOutbox.updateItem).toHaveBeenCalledWith(
        "item-auth-fail",
        expect.objectContaining({
          status: "failed",
          nextRetryAt: null, // Không tự động retry tiếp
        })
      );
    });

    it("phải DỪNG RETRY và đánh dấu 'conflict' khi gặp lỗi HTTP 409 (Optimistic Locking Conflict)", async () => {
      inMemoryItems = [
        {
          id: "item-conflict",
          titleVi: "Cập nhật chi phí",
          endpoint: "/api/costs/123/confirm",
          method: "POST",
          payloadJson: "{}",
          ifMatch: "AAAAA123",
          idempotencyKey: "mob-key-conflict",
          status: "pending",
          retryCount: 0,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ];

      const conflictError = new MockLcmsApiException({
        status: 409,
        code: "concurrency_conflict",
        messageVi: "Dữ liệu đã bị cập nhật",
        isConcurrencyConflict: true,
        isRatingNotReady: false,
      });

      mockApiRequest.mockRejectedValueOnce(conflictError);

      const result = await syncEngine.triggerSync("manual");

      expect(result.conflictCount).toBe(1);
      expect(mockOutbox.updateItem).toHaveBeenCalledWith(
        "item-conflict",
        expect.objectContaining({
          status: "conflict",
          nextRetryAt: null, // Không tự động retry đè
        })
      );
    });

    it("phải lập lịch Retry có Backoff khi gặp lỗi mạng tạm thời", async () => {
      inMemoryItems = [
        {
          id: "item-network-err",
          titleVi: "Giao dịch",
          endpoint: "/api/costs",
          method: "POST",
          payloadJson: "{}",
          idempotencyKey: "mob-net-key",
          status: "pending",
          retryCount: 1,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ];

      mockApiRequest.mockRejectedValueOnce(new Error("Network connection failed"));

      const result = await syncEngine.triggerSync("manual");

      expect(result.retryScheduledCount).toBe(1);
      expect(mockOutbox.updateItem).toHaveBeenCalledWith(
        "item-network-err",
        expect.objectContaining({
          status: "failed",
          retryCount: 2,
          nextRetryAt: expect.any(String),
        })
      );
    });
  });
});
