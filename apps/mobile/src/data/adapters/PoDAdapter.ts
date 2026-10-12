import { createIdempotencyKey } from "@lcms/shared";
import { apiRequest } from "../../api/client";
import { enqueueOfflineMutation, OutboxEngine } from "../../core/offline";

export interface PoDGeostamp {
  latitude: number | null;
  longitude: number | null;
  accuracy: number | null;
  status: "GPS_ACQUIRED" | "GPS_UNAVAILABLE" | "PERMISSION_DENIED";
}

export interface PoDSubmissionInput {
  billId: string;
  billNo: string;
  recipientName: string;
  recipientPhone: string;
  signatureLocalUri: string;
  signatureSvgContent?: string;
  cargoImageLocalUris: string[];
  geostamp: PoDGeostamp;
  deliveredAt: string;
  customNotes?: string;
}

export interface AdaptedPoDResult {
  operationalReferencePayload: {
    changes: Record<string, string>;
    reason: string;
  };
  attachments: Array<{
    fileName: string;
    objectType: "bill";
    objectId: string;
    localFileUri: string;
    fileContentType: string;
    notes: string;
    titleVi: string;
    sha256Hash?: string;
  }>;
}

/**
 * Thuật toán SHA-256 thuần JavaScript (Pure JS SHA-256) đảm bảo chạy được trên mọi môi trường
 * (Node, Hermes, Web, Native) mà không phụ thuộc thư viện native bên ngoài.
 */
export function computeSha256Hex(ascii: string): string {
  function rightRotate(value: number, amount: number) {
    return (value >>> amount) | (value << (32 - amount));
  }

  let result = "";
  const words: number[] = [];
  const asciiBitLength = ascii.length * 8;

  let hash = [
    0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
    0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
  ];

  const k = [
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
  ];

  for (let i = 0; i < ascii.length; i++) {
    const code = ascii.charCodeAt(i);
    words[i >> 2] |= code << ((3 - (i % 4)) * 8);
  }

  words[asciiBitLength >> 5] |= 0x80 << ((3 - ((asciiBitLength >> 3) % 4)) * 8);
  words[(((asciiBitLength + 64) >> 9) << 4) + 15] = asciiBitLength;

  const w = new Array(64);
  for (let i = 0; i < words.length; i += 16) {
    let a = hash[0], b = hash[1], c = hash[2], d = hash[3],
        e = hash[4], f = hash[5], g = hash[6], h = hash[7];

    for (let j = 0; j < 64; j++) {
      if (j < 16) {
        w[j] = words[i + j] | 0;
      } else {
        const s0 = rightRotate(w[j - 15], 7) ^ rightRotate(w[j - 15], 18) ^ (w[j - 15] >>> 3);
        const s1 = rightRotate(w[j - 2], 17) ^ rightRotate(w[j - 2], 19) ^ (w[j - 2] >>> 10);
        w[j] = (w[j - 16] + s0 + w[j - 7] + s1) | 0;
      }

      const s1 = rightRotate(e, 6) ^ rightRotate(e, 11) ^ rightRotate(e, 25);
      const ch = (e & f) ^ (~e & g);
      const temp1 = (h + s1 + ch + k[j] + w[j]) | 0;
      const s0 = rightRotate(a, 2) ^ rightRotate(a, 13) ^ rightRotate(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const temp2 = (s0 + maj) | 0;

      h = g; g = f; f = e; e = (d + temp1) | 0;
      d = c; c = b; b = a; a = (temp1 + temp2) | 0;
    }

    hash[0] = (hash[0] + a) | 0; hash[1] = (hash[1] + b) | 0;
    hash[2] = (hash[2] + c) | 0; hash[3] = (hash[3] + d) | 0;
    hash[4] = (hash[4] + e) | 0; hash[5] = (hash[5] + f) | 0;
    hash[6] = (hash[6] + g) | 0; hash[7] = (hash[7] + h) | 0;
  }

  for (let i = 0; i < 8; i++) {
    for (let j = 3; j >= 0; j--) {
      const byte = (hash[i] >> (8 * j)) & 255;
      result += (byte < 16 ? "0" : "") + byte.toString(16);
    }
  }

  return result;
}

export class PoDAdapter {
  /**
   * Biến đổi dữ liệu ký nhận PoD hiện trường thành các payload hợp lệ của API Backend hiện hữu:
   * 1. PATCH /api/operational-references/bill/{id}
   * 2. POST /api/attachments (Chữ ký & Ảnh hàng hóa)
   */
  public static adaptPoDSubmission(input: PoDSubmissionInput): AdaptedPoDResult {
    if (!input.billId || input.billId.trim() === "") {
      throw new Error("Mã Vận đơn (billId) bắt buộc phải có.");
    }
    if (!input.recipientName || input.recipientName.trim().length < 2) {
      throw new Error("Vui lòng nhập họ tên người nhận hàng (tối thiểu 2 ký tự).");
    }
    if (!input.recipientPhone || input.recipientPhone.trim().length < 9) {
      throw new Error("Vui lòng nhập số điện thoại người nhận hàng hợp lệ (tối thiểu 9 số).");
    }
    if (!input.signatureLocalUri) {
      throw new Error("Chưa có chữ ký điện tử của người nhận hàng.");
    }

    // 1. Định dạng tọa độ GPS
    let gpsDescription = "GPS_UNAVAILABLE";
    if (input.geostamp.status === "GPS_ACQUIRED" && input.geostamp.latitude && input.geostamp.longitude) {
      const lat = input.geostamp.latitude.toFixed(6);
      const lng = input.geostamp.longitude.toFixed(6);
      const acc = input.geostamp.accuracy ? ` (±${Math.round(input.geostamp.accuracy)}m)` : "";
      gpsDescription = `${lat}, ${lng}${acc}`;
    } else if (input.geostamp.status === "PERMISSION_DENIED") {
      gpsDescription = "GPS_PERMISSION_DENIED";
    }

    // 2. Tạo ghi chú vận hành hợp nhất
    const deliveryNotes = `[XÁC NHẬN BÀN GIAO HÀNG HÓA (e-PoD)] Người nhận: ${input.recipientName.trim()} (${input.recipientPhone.trim()}) | Thời gian: ${input.deliveredAt} | Tọa độ GPS: ${gpsDescription} | Đã chốt chữ ký điện tử & ảnh hiện trường.${
      input.customNotes ? ` Ghi chú: ${input.customNotes.trim()}` : ""
    }`;

    const operationalReferencePayload = {
      changes: {
        notes: deliveryNotes,
        remarks: `PoD Verified by ${input.recipientName.trim()} @ ${input.deliveredAt}`,
      },
      reason: "Xác nhận bàn giao hàng hóa điện tử (Proof of Delivery - PoD) tại hiện trường",
    };

    // 3. Tính toán hash SHA-256 cho chữ ký số
    const signatureSha256 = input.signatureSvgContent
      ? computeSha256Hex(input.signatureSvgContent)
      : computeSha256Hex(input.signatureLocalUri);

    const attachments: AdaptedPoDResult["attachments"] = [
      {
        fileName: `pod-signature-${input.billNo || "bill"}.svg`,
        objectType: "bill",
        objectId: input.billId,
        localFileUri: input.signatureLocalUri,
        fileContentType: "image/svg+xml",
        notes: `Chữ ký nhận của ${input.recipientName} (SĐT: ${input.recipientPhone})`,
        titleVi: `Chữ ký PoD - ${input.billNo}`,
        sha256Hash: signatureSha256,
      },
    ];

    // Đính kèm các ảnh hàng hóa chụp trước cửa kho (nếu có)
    input.cargoImageLocalUris.forEach((uri, idx) => {
      attachments.push({
        fileName: `pod-cargo-${idx + 1}-${input.billNo || "bill"}.jpg`,
        objectType: "bill",
        objectId: input.billId,
        localFileUri: uri,
        fileContentType: "image/jpeg",
        notes: `Ảnh chụp hàng hóa thực tế tại điểm giao (${idx + 1})`,
        titleVi: `Ảnh hiện trường PoD #${idx + 1} - ${input.billNo}`,
        sha256Hash: computeSha256Hex(uri + idx),
      });
    });

    return {
      operationalReferencePayload,
      attachments,
    };
  }

  /**
   * Thực thi gửi dữ liệu PoD về Backend.
   * Nếu có mạng -> gửi trực tiếp; Nếu mất mạng -> tự động đưa vào hàng đợi Outbox.
   */
  public static async submitPoD(
    input: PoDSubmissionInput,
    forceOffline = false
  ): Promise<{ success: boolean; isOffline: boolean; messageVi: string }> {
    const adapted = this.adaptPoDSubmission(input);

    if (forceOffline) {
      await this.queuePoDOffline(adapted);
      return {
        success: true,
        isOffline: true,
        messageVi: "Đã lưu thông tin bàn giao (e-PoD) vào Outbox. Hệ thống sẽ tự động đồng bộ khi có sóng.",
      };
    }

    try {
      // 1. Cập nhật ghi chú bàn giao vận hành
      await apiRequest(`/api/operational-references/bill/${input.billId}`, {
        method: "PATCH",
        body: adapted.operationalReferencePayload,
      });

      // 2. Tải các file đính kèm chữ ký & ảnh hiện trường
      for (const att of adapted.attachments) {
        await enqueueOfflineMutation({
          titleVi: att.titleVi,
          endpoint: "/api/attachments",
          method: "POST",
          payload: {
            objectType: att.objectType,
            objectId: att.objectId,
            notes: att.notes,
          },
          localFileUri: att.localFileUri,
          fileContentType: att.fileContentType,
          fileName: att.fileName,
          idempotencyPrefix: "att-pod",
        });
      }

      return {
        success: true,
        isOffline: false,
        messageVi: `Đã xác nhận bàn giao hàng hóa (e-PoD) thành công cho Vận đơn ${input.billNo}.`,
      };
    } catch {
      // Khi mất kết nối mạng, tự động fallback toàn bộ vào hàng đợi Outbox
      await this.queuePoDOffline(adapted);
      return {
        success: true,
        isOffline: true,
        messageVi: "Mạng chập chờn: Thông tin bàn giao (e-PoD) đã được lưu trữ an toàn trong Outbox.",
      };
    }
  }

  private static async queuePoDOffline(adapted: AdaptedPoDResult): Promise<void> {
    // Lưu mutation cập nhật vận đơn
    await enqueueOfflineMutation({
      titleVi: `Xác nhận giao hàng e-PoD: ${adapted.operationalReferencePayload.reason}`,
      endpoint: `/api/operational-references/bill/${adapted.attachments[0]?.objectId}`,
      method: "PATCH",
      payload: adapted.operationalReferencePayload,
      idempotencyPrefix: "op-pod",
    });

    // Lưu các attachments chữ ký và ảnh
    for (const att of adapted.attachments) {
      await enqueueOfflineMutation({
        titleVi: att.titleVi,
        endpoint: "/api/attachments",
        method: "POST",
        payload: {
          objectType: att.objectType,
          objectId: att.objectId,
          notes: att.notes,
        },
        localFileUri: att.localFileUri,
        fileContentType: att.fileContentType,
        fileName: att.fileName,
        idempotencyPrefix: "att-pod",
      });
    }
  }
}
