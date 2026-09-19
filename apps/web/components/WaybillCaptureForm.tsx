"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { captureWaybill, computeWaybillTotals } from "@/lib/waybill";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
};

function num(fd: FormData, name: string): number {
  const raw = String(fd.get(name) ?? "").trim().replaceAll(",", "");
  if (!raw) return 0;
  const n = Number(raw);
  return Number.isFinite(n) ? n : 0;
}

function optNum(fd: FormData, name: string): number | null {
  const raw = String(fd.get(name) ?? "").trim().replaceAll(",", "");
  if (!raw) return null;
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

function dateIso(fd: FormData, name: string): string | null {
  const raw = String(fd.get(name) ?? "").trim();
  if (!raw) return null;
  return `${raw}T00:00:00+07:00`;
}

function str(fd: FormData, name: string): string | null {
  const raw = String(fd.get(name) ?? "").trim();
  return raw || null;
}

export function WaybillCaptureForm({ terms }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [basePostage, setBasePostage] = useState("");
  const [vatPostage, setVatPostage] = useState("");
  const [surcharge, setSurcharge] = useState("");
  const [codFee, setCodFee] = useState("");
  const [otherFee, setOtherFee] = useState("");
  const [totalCollect, setTotalCollect] = useState("");

  const billLabel = term(terms, "BILL", "Bill");
  const totals = useMemo(() => {
    const parse = (v: string) => {
      const n = Number(v.replaceAll(",", "").trim());
      return Number.isFinite(n) ? n : 0;
    };
    return computeWaybillTotals({
      basePostage: parse(basePostage),
      vatPostage: parse(vatPostage),
      surcharge: parse(surcharge),
      codFee: parse(codFee),
      otherFee: parse(otherFee),
      totalCollect: parse(totalCollect),
    });
  }, [basePostage, vatPostage, surcharge, codFee, otherFee, totalCollect]);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const billNo = String(fd.get("billNo") ?? "").trim();
    if (!billNo) {
      setError("Nhập số vận đơn.");
      setSubmitting(false);
      return;
    }
    const senderName = String(fd.get("senderName") ?? "").trim();
    const consigneeName = String(fd.get("consigneeName") ?? "").trim();
    if (!senderName || !consigneeName) {
      setError("Nhập họ tên người gửi và người nhận.");
      setSubmitting(false);
      return;
    }

    const body = {
      billNo,
      billType: str(fd, "billType") ?? "parcel",
      sourceSystem: str(fd, "sourceSystem") ?? "lcms_waybill",
      carrierName: str(fd, "carrierName"),
      itemFormCode: str(fd, "itemFormCode"),
      sender: {
        name: senderName,
        phone: str(fd, "senderPhone"),
        email: str(fd, "senderEmail"),
        address: str(fd, "senderAddress"),
        customerCode: str(fd, "senderCustomerCode"),
        postalCode: str(fd, "senderPostalCode"),
      },
      consignee: {
        name: consigneeName,
        phone: str(fd, "consigneePhone"),
        email: str(fd, "consigneeEmail"),
        address: str(fd, "consigneeAddress"),
        deliveryCode: str(fd, "consigneeDeliveryCode"),
        postalCode: str(fd, "consigneePostalCode"),
      },
      packageKind: str(fd, "packageKind") ?? "goods",
      contentsDescription: str(fd, "contentsDescription"),
      contentsQuantity: optNum(fd, "contentsQuantity"),
      declaredValue: optNum(fd, "declaredValue"),
      accompanyingDocs: str(fd, "accompanyingDocs"),
      vatServicesNote: str(fd, "vatServicesNote"),
      nonDeliveryAction: str(fd, "nonDeliveryAction"),
      senderCommitAccepted: fd.get("senderCommitAccepted") === "on",
      sentAt: dateIso(fd, "sentAt"),
      parcelCount: optNum(fd, "parcelCount") ?? 1,
      actualWeightKg: optNum(fd, "actualWeightKg"),
      chargeableWeightKg: optNum(fd, "chargeableWeightKg"),
      basePostage: num(fd, "basePostage"),
      vatPostage: num(fd, "vatPostage"),
      surcharge: num(fd, "surcharge"),
      codFee: num(fd, "codFee"),
      otherFee: num(fd, "otherFee"),
      totalPostageInclVat: totals.totalPostageInclVat,
      totalCollect: num(fd, "totalCollect"),
      grandTotal: totals.grandTotal,
      currencyCode: str(fd, "currencyCode") ?? "VND",
      postagePayer: str(fd, "postagePayer") ?? "sender",
      chargeEconomicRole: str(fd, "chargeEconomicRole") ?? "cost",
      codCollectAmount: num(fd, "codCollectAmount"),
      operationsNote: str(fd, "operationsNote"),
      acceptingOffice: str(fd, "acceptingOffice"),
      acceptedAt: dateIso(fd, "acceptedAt"),
      acceptedBy: str(fd, "acceptedBy"),
      receivedAt: dateIso(fd, "receivedAt"),
      receivedBy: str(fd, "receivedBy"),
    };

    const result = await captureWaybill(body);
    if (!result.ok) {
      setError(result.message);
      setSubmitting(false);
      return;
    }
    startTransition(() => router.push(`/bills/${result.id}`));
    router.refresh();
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form waybill-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Vận đơn giấy là {billLabel} — neo tài chính. Cước ghi trên giấy thành{" "}
        {term(terms, "EXPECTED_COST", "chi phí dự kiến")} (chưa tất toán). Thu hộ
        người nhận không tự thành doanh thu.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="waybill-sheet">
        <fieldset className="group-box">
          <legend>1. Người gửi</legend>
          <div className="form-grid">
            <div className="field field-span">
              <label htmlFor="senderName">Họ tên</label>
              <input id="senderName" name="senderName" required maxLength={256} disabled={busy} autoComplete="name" />
            </div>
            <div className="field">
              <label htmlFor="senderPhone">Điện thoại</label>
              <input id="senderPhone" name="senderPhone" maxLength={32} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="senderEmail">Email</label>
              <input id="senderEmail" name="senderEmail" type="email" maxLength={128} disabled={busy} />
            </div>
            <div className="field field-span">
              <label htmlFor="senderAddress">Địa chỉ</label>
              <input id="senderAddress" name="senderAddress" maxLength={512} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="senderCustomerCode">Mã khách hàng</label>
              <input id="senderCustomerCode" name="senderCustomerCode" maxLength={64} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="senderPostalCode">Mã bưu chính</label>
              <input id="senderPostalCode" name="senderPostalCode" maxLength={32} disabled={busy} />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>2. Người nhận</legend>
          <div className="form-grid">
            <div className="field field-span">
              <label htmlFor="consigneeName">Họ tên</label>
              <input id="consigneeName" name="consigneeName" required maxLength={256} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="consigneePhone">Điện thoại</label>
              <input id="consigneePhone" name="consigneePhone" maxLength={32} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="consigneeEmail">Email</label>
              <input id="consigneeEmail" name="consigneeEmail" type="email" maxLength={128} disabled={busy} />
            </div>
            <div className="field field-span">
              <label htmlFor="consigneeAddress">Địa chỉ</label>
              <input id="consigneeAddress" name="consigneeAddress" maxLength={512} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="consigneeDeliveryCode">Mã ĐH / phát hàng</label>
              <input id="consigneeDeliveryCode" name="consigneeDeliveryCode" maxLength={64} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="consigneePostalCode">Mã bưu chính</label>
              <input id="consigneePostalCode" name="consigneePostalCode" maxLength={32} disabled={busy} />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>Định danh vận đơn</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="billNo">Số vận đơn ({billLabel})</label>
              <input id="billNo" name="billNo" required maxLength={64} disabled={busy} placeholder="VD: EE5556576340VN" autoComplete="off" />
            </div>
            <div className="field">
              <label htmlFor="itemFormCode">Mẫu</label>
              <input id="itemFormCode" name="itemFormCode" maxLength={16} disabled={busy} placeholder="BD1" />
            </div>
            <div className="field">
              <label htmlFor="carrierName">Đơn vị vận chuyển</label>
              <input id="carrierName" name="carrierName" maxLength={128} disabled={busy} placeholder="Vietnam Post" />
            </div>
            <div className="field">
              <label htmlFor="sourceSystem">Hệ thống nguồn</label>
              <input id="sourceSystem" name="sourceSystem" maxLength={64} disabled={busy} defaultValue="lcms_waybill" />
            </div>
            <div className="field">
              <label htmlFor="billType">Loại {billLabel}</label>
              <select id="billType" name="billType" disabled={busy} defaultValue="parcel">
                <option value="parcel">Bưu kiện</option>
                <option value="freight">Hàng hóa / freight</option>
                <option value="road">Đường bộ</option>
                <option value="air">Hàng không</option>
                <option value="sea">Đường biển</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="currencyCode">Tiền tệ</label>
              <input id="currencyCode" name="currencyCode" maxLength={3} disabled={busy} defaultValue="VND" />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>3–4. Loại hàng &amp; nội dung</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="packageKind">Loại hàng gửi</label>
              <select id="packageKind" name="packageKind" disabled={busy} defaultValue="goods">
                <option value="goods">Hàng hóa</option>
                <option value="document">Tài liệu</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="contentsQuantity">Số lượng</label>
              <input id="contentsQuantity" name="contentsQuantity" type="number" min={0} disabled={busy} />
            </div>
            <div className="field field-span">
              <label htmlFor="contentsDescription">Nội dung</label>
              <input id="contentsDescription" name="contentsDescription" maxLength={512} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="declaredValue">Trị giá khai</label>
              <input id="declaredValue" name="declaredValue" inputMode="decimal" disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="accompanyingDocs">Giấy tờ đính kèm</label>
              <input id="accompanyingDocs" name="accompanyingDocs" maxLength={256} disabled={busy} />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>5–8. Dịch vụ &amp; chỉ dẫn</legend>
          <div className="form-grid">
            <div className="field field-span">
              <label htmlFor="vatServicesNote">Dịch vụ GTGT sử dụng</label>
              <input id="vatServicesNote" name="vatServicesNote" maxLength={512} disabled={busy} />
            </div>
            <div className="field field-span">
              <label htmlFor="nonDeliveryAction">Chỉ dẫn không phát được</label>
              <select id="nonDeliveryAction" name="nonDeliveryAction" disabled={busy} defaultValue="">
                <option value="">— Không chọn —</option>
                <option value="return_immediately">Chuyển hoàn ngay</option>
                <option value="call_sender">Gọi người gửi / Báo gửi</option>
                <option value="hold_until_pickup">Chuyển hoàn khi hết thời gian lưu trữ</option>
                <option value="destroy">Hủy</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="sentAt">Ngày gửi</label>
              <input id="sentAt" name="sentAt" type="date" disabled={busy} />
            </div>
            <div className="field">
              <label className="check-label">
                <input type="checkbox" name="senderCommitAccepted" disabled={busy} />
                Người gửi cam kết nội dung hợp lệ
              </label>
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>9. Số lượng bưu gửi</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="parcelCount">Số kiện</label>
              <input id="parcelCount" name="parcelCount" type="number" min={1} defaultValue={1} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="actualWeightKg">Khối lượng thực (kg)</label>
              <input id="actualWeightKg" name="actualWeightKg" inputMode="decimal" disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="chargeableWeightKg">Khối lượng tính cước (kg)</label>
              <input id="chargeableWeightKg" name="chargeableWeightKg" inputMode="decimal" disabled={busy} />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>10–11. Cước &amp; thu người nhận</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="basePostage">Cước chính</label>
              <input id="basePostage" name="basePostage" inputMode="decimal" disabled={busy} value={basePostage} onChange={(e) => setBasePostage(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="vatPostage">Cước GTGT</label>
              <input id="vatPostage" name="vatPostage" inputMode="decimal" disabled={busy} value={vatPostage} onChange={(e) => setVatPostage(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="surcharge">Phụ cước</label>
              <input id="surcharge" name="surcharge" inputMode="decimal" disabled={busy} value={surcharge} onChange={(e) => setSurcharge(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="codFee">Phí COD</label>
              <input id="codFee" name="codFee" inputMode="decimal" disabled={busy} value={codFee} onChange={(e) => setCodFee(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="otherFee">Thu khác</label>
              <input id="otherFee" name="otherFee" inputMode="decimal" disabled={busy} value={otherFee} onChange={(e) => setOtherFee(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="totalCollect">Tổng thu</label>
              <input id="totalCollect" name="totalCollect" inputMode="decimal" disabled={busy} value={totalCollect} onChange={(e) => setTotalCollect(e.target.value)} />
            </div>
            <div className="field">
              <span className="field-static-label">Tổng cước (gồm VAT)</span>
              <p className="field-static-value">{totals.totalPostageInclVat.toLocaleString("vi-VN")}</p>
            </div>
            <div className="field">
              <span className="field-static-label">Tổng</span>
              <p className="field-static-value">{totals.grandTotal.toLocaleString("vi-VN")}</p>
            </div>
            <div className="field">
              <label htmlFor="codCollectAmount">Thu của người nhận (thu hộ)</label>
              <input id="codCollectAmount" name="codCollectAmount" inputMode="decimal" disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="postagePayer">Người trả cước</label>
              <select id="postagePayer" name="postagePayer" disabled={busy} defaultValue="sender">
                <option value="sender">Người gửi</option>
                <option value="consignee">Người nhận</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="chargeEconomicRole">Ghi nhận kinh tế</label>
              <select id="chargeEconomicRole" name="chargeEconomicRole" disabled={busy} defaultValue="cost">
                <option value="cost">Chi phí dự kiến (trả nhà xe)</option>
                <option value="revenue">Doanh thu dự kiến (đơn vị vận chuyển)</option>
              </select>
            </div>
          </div>
          <p className="muted small">
            Thu hộ chưa phải doanh thu. Cước chỉ ghi lớp Dự kiến — xác nhận/thực tế làm trên tab chi phí.
          </p>
        </fieldset>

        <fieldset className="group-box">
          <legend>12–14. Chấp nhận &amp; nhận hàng</legend>
          <div className="form-grid">
            <div className="field field-span">
              <label htmlFor="operationsNote">Chú dẫn nghiệp vụ</label>
              <input id="operationsNote" name="operationsNote" maxLength={512} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="acceptingOffice">Bưu cục / điểm chấp nhận</label>
              <input id="acceptingOffice" name="acceptingOffice" maxLength={256} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="acceptedAt">Ngày chấp nhận</label>
              <input id="acceptedAt" name="acceptedAt" type="date" disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="acceptedBy">GDV / người chấp nhận</label>
              <input id="acceptedBy" name="acceptedBy" maxLength={128} disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="receivedAt">Ngày nhận</label>
              <input id="receivedAt" name="receivedAt" type="date" disabled={busy} />
            </div>
            <div className="field">
              <label htmlFor="receivedBy">Người nhận ký</label>
              <input id="receivedBy" name="receivedBy" maxLength={128} disabled={busy} />
            </div>
          </div>
        </fieldset>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang lưu…" : "Lưu vận đơn"}
        </button>
      </div>
    </form>
  );
}
