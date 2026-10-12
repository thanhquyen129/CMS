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
  writeAsStringAsync: jest.fn().mockResolvedValue(undefined),
  copyAsync: jest.fn().mockResolvedValue(undefined),
  deleteAsync: jest.fn().mockResolvedValue(undefined),
}));

jest.mock("react-native", () => ({
  Platform: { OS: "ios" },
}));

jest.mock("@react-native-community/netinfo", () => ({
  addEventListener: jest.fn(),
  fetch: jest.fn().mockResolvedValue({ isConnected: true, isInternetReachable: true }),
}));

jest.mock("../../../api/client", () => ({
  apiRequest: jest.fn(),
}));

import { PoDAdapter, computeSha256Hex } from "../PoDAdapter";

describe("PoDAdapter (Proof of Delivery Adapter Tests)", () => {
  describe("computeSha256Hex", () => {
    it("phải tính toán hash SHA-256 chính xác cho chuỗi văn bản", () => {
      expect(computeSha256Hex("")).toBe(
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      );

      const hash = computeSha256Hex("LCMS-POD");
      expect(hash).toHaveLength(64);
      expect(hash).toMatch(/^[0-9a-f]{64}$/);
    });
  });

  describe("adaptPoDSubmission", () => {
    it("phải đóng gói đầy đủ ghi chú vận hành và chữ ký số khi GPS hợp lệ", () => {
      const adapted = PoDAdapter.adaptPoDSubmission({
        billId: "bill-guid-001",
        billNo: "BILL-HCM-099",
        recipientName: "Nguyễn Văn Hùng",
        recipientPhone: "0908123456",
        signatureLocalUri: "file:///documents/sig_123.svg",
        signatureSvgContent: "<svg>signature</svg>",
        cargoImageLocalUris: [
          "file:///documents/cargo_1.jpg",
          "file:///documents/cargo_2.jpg",
        ],
        geostamp: {
          latitude: 10.776889,
          longitude: 106.700897,
          accuracy: 8.5,
          status: "GPS_ACQUIRED",
        },
        deliveredAt: "12/10/2026 09:30:00",
        customNotes: "Giao đủ 10 kiện carton nguyên vẹn",
      });

      expect(adapted.operationalReferencePayload.reason).toContain("Proof of Delivery - PoD");
      const notes = adapted.operationalReferencePayload.changes.notes;
      expect(notes).toContain("[XÁC NHẬN BÀN GIAO HÀNG HÓA (e-PoD)]");
      expect(notes).toContain("Nguyễn Văn Hùng (0908123456)");
      expect(notes).toContain("10.776889, 106.700897 (±9m)");
      expect(notes).toContain("12/10/2026 09:30:00");
      expect(notes).toContain("Giao đủ 10 kiện carton nguyên vẹn");

      expect(adapted.attachments).toHaveLength(3);
      const sigAtt = adapted.attachments[0];
      expect(sigAtt.fileName).toBe("pod-signature-BILL-HCM-099.svg");
      expect(sigAtt.objectType).toBe("bill");
      expect(sigAtt.fileContentType).toBe("image/svg+xml");
      expect(sigAtt.sha256Hash).toBe(computeSha256Hex("<svg>signature</svg>"));

      const cargo1 = adapted.attachments[1];
      expect(cargo1.fileName).toBe("pod-cargo-1-BILL-HCM-099.jpg");
      expect(cargo1.fileContentType).toBe("image/jpeg");
    });

    it("phải đánh dấu cờ GPS_UNAVAILABLE khi không lấy được tọa độ GPS", () => {
      const adapted = PoDAdapter.adaptPoDSubmission({
        billId: "bill-guid-002",
        billNo: "BILL-DN-050",
        recipientName: "Trần Thị Lan",
        recipientPhone: "0912345678",
        signatureLocalUri: "file:///documents/sig_456.svg",
        cargoImageLocalUris: [],
        geostamp: {
          latitude: null,
          longitude: null,
          accuracy: null,
          status: "GPS_UNAVAILABLE",
        },
        deliveredAt: "12/10/2026 10:00:00",
      });

      const notes = adapted.operationalReferencePayload.changes.notes;
      expect(notes).toContain("GPS_UNAVAILABLE");
      expect(adapted.attachments).toHaveLength(1);
    });

    it("phải từ chối khi thiếu họ tên người nhận hoặc số điện thoại không hợp lệ", () => {
      expect(() =>
        PoDAdapter.adaptPoDSubmission({
          billId: "bill-1",
          billNo: "B-1",
          recipientName: "",
          recipientPhone: "0908123456",
          signatureLocalUri: "file:///sig.svg",
          cargoImageLocalUris: [],
          geostamp: {
            latitude: null,
            longitude: null,
            accuracy: null,
            status: "GPS_UNAVAILABLE",
          },
          deliveredAt: "2026-10-12",
        })
      ).toThrow("Vui lòng nhập họ tên người nhận hàng");

      expect(() =>
        PoDAdapter.adaptPoDSubmission({
          billId: "bill-1",
          billNo: "B-1",
          recipientName: "Anh Nam",
          recipientPhone: "123",
          signatureLocalUri: "file:///sig.svg",
          cargoImageLocalUris: [],
          geostamp: {
            latitude: null,
            longitude: null,
            accuracy: null,
            status: "GPS_UNAVAILABLE",
          },
          deliveredAt: "2026-10-12",
        })
      ).toThrow("số điện thoại người nhận hàng hợp lệ");
    });
  });
});
