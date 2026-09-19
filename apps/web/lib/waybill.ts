/** Types and BFF calls for Bill waybill profile (ADR-0018). */

export type WaybillParty = {
  name: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  customerCode: string | null;
  postalCode: string | null;
  deliveryCode?: string | null;
};

export type WaybillCharges = {
  basePostage: number;
  vatPostage: number;
  surcharge: number;
  codFee: number;
  otherFee: number;
  totalPostageInclVat: number;
  totalCollect: number;
  grandTotal: number;
  currencyCode: string;
  postagePayer: string;
  chargeEconomicRole: string;
  codCollectAmount: number;
  amountsRedacted: boolean;
};

export type BillWaybill = {
  id: string;
  billId: string;
  billNo: string;
  carrierName: string | null;
  itemFormCode: string | null;
  sender: WaybillParty;
  consignee: WaybillParty;
  packageKind: string;
  contentsDescription: string | null;
  contentsQuantity: number | null;
  declaredValue: number | null;
  accompanyingDocs: string | null;
  vatServicesNote: string | null;
  nonDeliveryAction: string | null;
  senderCommitAccepted: boolean;
  sentAt: string | null;
  parcelCount: number;
  actualWeightKg: number | null;
  chargeableWeightKg: number | null;
  charges: WaybillCharges;
  operationsNote: string | null;
  acceptingOffice: string | null;
  acceptedAt: string | null;
  receivedAt: string | null;
  acceptedBy: string | null;
  receivedBy: string | null;
};

export type WaybillWritePayload = {
  billNo?: string;
  billType?: string;
  sourceSystem?: string | null;
  externalId?: string | null;
  carrierName?: string | null;
  itemFormCode?: string | null;
  sender?: Partial<WaybillParty>;
  consignee?: Partial<WaybillParty>;
  packageKind?: string;
  contentsDescription?: string | null;
  contentsQuantity?: number | null;
  declaredValue?: number | null;
  accompanyingDocs?: string | null;
  vatServicesNote?: string | null;
  nonDeliveryAction?: string | null;
  senderCommitAccepted?: boolean;
  sentAt?: string | null;
  parcelCount?: number | null;
  actualWeightKg?: number | null;
  chargeableWeightKg?: number | null;
  basePostage?: number;
  vatPostage?: number;
  surcharge?: number;
  codFee?: number;
  otherFee?: number;
  totalPostageInclVat?: number;
  totalCollect?: number;
  grandTotal?: number;
  currencyCode?: string;
  postagePayer?: string;
  chargeEconomicRole?: string;
  codCollectAmount?: number;
  operationsNote?: string | null;
  acceptingOffice?: string | null;
  acceptedAt?: string | null;
  acceptedBy?: string | null;
  receivedAt?: string | null;
  receivedBy?: string | null;
};

function roundMoney(n: number): number {
  return Math.round(n * 10000) / 10000;
}

/** Documentary totals from component postage lines (must match API). */
export function computeWaybillTotals(input: {
  basePostage: number;
  vatPostage: number;
  surcharge: number;
  codFee: number;
  otherFee: number;
  totalCollect: number;
}): { totalPostageInclVat: number; grandTotal: number } {
  const totalPostageInclVat = roundMoney(
    input.basePostage +
      input.vatPostage +
      input.surcharge +
      input.codFee +
      input.otherFee
  );
  return {
    totalPostageInclVat,
    grandTotal: roundMoney(totalPostageInclVat + input.totalCollect),
  };
}

export function packageKindLabel(kind: string): string {
  switch (kind) {
    case "document":
      return "Tài liệu";
    case "goods":
      return "Hàng hóa";
    default:
      return kind || "—";
  }
}

export function nonDeliveryLabel(code: string | null | undefined): string {
  switch (code) {
    case "return_immediately":
      return "Chuyển hoàn ngay";
    case "call_sender":
      return "Gọi người gửi/Báo gửi";
    case "hold_until_pickup":
      return "Chuyển hoàn khi hết thời gian lưu trữ";
    case "destroy":
      return "Hủy";
    default:
      return "—";
  }
}

export function postagePayerLabel(code: string): string {
  return code === "consignee" ? "Người nhận" : "Người gửi";
}

export async function captureWaybill(
  body: WaybillWritePayload
): Promise<
  | { ok: true; id: string }
  | { ok: false; status: number; message: string }
> {
  try {
    const res = await fetch("/bff/bills/waybills", {
      method: "POST",
      headers: { Accept: "application/json", "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (res.status === 401) {
      window.location.href = "/login";
      return { ok: false, status: 401, message: "Phiên đăng nhập đã hết." };
    }
    if (!res.ok) {
      const payload = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message:
          payload.message ||
          (res.status === 409
            ? "Số vận đơn đã tồn tại trong thuê bao."
            : "Không lưu được vận đơn."),
      };
    }
    const created = (await res.json()) as { id?: string };
    if (!created.id) {
      return { ok: false, status: res.status, message: "Máy chủ không trả id Bill." };
    }
    return { ok: true, id: created.id };
  } catch {
    return { ok: false, status: 0, message: "Không kết nối được máy chủ. Thử lại sau." };
  }
}
