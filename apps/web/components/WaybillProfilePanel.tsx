import { formatMoney } from "@/lib/money";
import {
  nonDeliveryLabel,
  packageKindLabel,
  postagePayerLabel,
  type BillWaybill,
} from "@/lib/waybill";

function dash(value: string | number | null | undefined): string {
  if (value == null || value === "") return "—";
  return String(value);
}

function dateOnly(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString("vi-VN");
  } catch {
    return iso;
  }
}

type Props = {
  waybill: BillWaybill | null | undefined;
  costLabel: string;
  revenueLabel: string;
};

/** Read-only waybill sheet on Bill Financial View. */
export function WaybillProfilePanel({ waybill, costLabel, revenueLabel }: Props) {
  if (!waybill) {
    return (
      <div className="empty-state" role="status">
        Bill này chưa có hồ sơ vận đơn giấy. Tạo vận đơn mới từ menu Đơn hàng vận chuyển.
      </div>
    );
  }

  const c = waybill.charges;
  const money = (n: number) =>
    c.amountsRedacted ? "••••" : formatMoney(n, c.currencyCode);

  return (
    <div className="waybill-sheet waybill-sheet-readonly">
      <fieldset className="group-box">
        <legend>Người gửi</legend>
        <dl className="info-grid">
          <div>
            <dt>Họ tên</dt>
            <dd>{dash(waybill.sender.name)}</dd>
          </div>
          <div>
            <dt>Điện thoại</dt>
            <dd>{dash(waybill.sender.phone)}</dd>
          </div>
          <div className="info-grid-span">
            <dt>Địa chỉ</dt>
            <dd>{dash(waybill.sender.address)}</dd>
          </div>
          <div>
            <dt>Mã KH / bưu chính</dt>
            <dd>
              {dash(waybill.sender.customerCode)} / {dash(waybill.sender.postalCode)}
            </dd>
          </div>
        </dl>
      </fieldset>
      <fieldset className="group-box">
        <legend>Người nhận</legend>
        <dl className="info-grid">
          <div>
            <dt>Họ tên</dt>
            <dd>{dash(waybill.consignee.name)}</dd>
          </div>
          <div>
            <dt>Điện thoại</dt>
            <dd>{dash(waybill.consignee.phone)}</dd>
          </div>
          <div className="info-grid-span">
            <dt>Địa chỉ</dt>
            <dd>{dash(waybill.consignee.address)}</dd>
          </div>
          <div>
            <dt>Mã ĐH / bưu chính</dt>
            <dd>
              {dash(waybill.consignee.deliveryCode)} /{" "}
              {dash(waybill.consignee.postalCode)}
            </dd>
          </div>
        </dl>
      </fieldset>
      <fieldset className="group-box">
        <legend>Hàng gửi</legend>
        <dl className="info-grid">
          <div>
            <dt>Loại</dt>
            <dd>{packageKindLabel(waybill.packageKind)}</dd>
          </div>
          <div>
            <dt>Nội dung</dt>
            <dd>
              {dash(waybill.contentsDescription)}
              {waybill.contentsQuantity != null
                ? ` × ${waybill.contentsQuantity}`
                : ""}
            </dd>
          </div>
          <div>
            <dt>Số kiện / kg</dt>
            <dd>
              {waybill.parcelCount} / {dash(waybill.actualWeightKg)} kg (tính cước{" "}
              {dash(waybill.chargeableWeightKg)})
            </dd>
          </div>
          <div>
            <dt>Chỉ dẫn không phát</dt>
            <dd>{nonDeliveryLabel(waybill.nonDeliveryAction)}</dd>
          </div>
        </dl>
      </fieldset>
      <fieldset className="group-box">
        <legend>Cước</legend>
        {c.amountsRedacted ? (
          <p className="muted small">
            Số cước ẩn vì thiếu quyền xem{" "}
            {c.chargeEconomicRole === "revenue" ? revenueLabel : costLabel}.
          </p>
        ) : null}
        <dl className="info-grid">
          <div>
            <dt>Cước chính</dt>
            <dd>{money(c.basePostage)}</dd>
          </div>
          <div>
            <dt>Phụ cước</dt>
            <dd>{money(c.surcharge)}</dd>
          </div>
          <div>
            <dt>Cước GTGT</dt>
            <dd>{money(c.vatPostage)}</dd>
          </div>
          <div>
            <dt>Thu khác</dt>
            <dd>{money(c.otherFee)}</dd>
          </div>
          <div>
            <dt>Tổng cước</dt>
            <dd>{money(c.totalPostageInclVat)}</dd>
          </div>
          <div>
            <dt>Người trả cước</dt>
            <dd>{postagePayerLabel(c.postagePayer)}</dd>
          </div>
          <div>
            <dt>Thu hộ người nhận</dt>
            <dd>
              {money(c.codCollectAmount)}
              <span className="muted small"> — chưa phải doanh thu</span>
            </dd>
          </div>
        </dl>
      </fieldset>
      <fieldset className="group-box">
        <legend>Chấp nhận</legend>
        <dl className="info-grid">
          <div>
            <dt>Đơn vị / mẫu</dt>
            <dd>
              {dash(waybill.carrierName)} {waybill.itemFormCode ? `(${waybill.itemFormCode})` : ""}
            </dd>
          </div>
          <div>
            <dt>Ngày gửi / chấp nhận</dt>
            <dd>
              {dateOnly(waybill.sentAt)} / {dateOnly(waybill.acceptedAt)}
            </dd>
          </div>
          <div>
            <dt>Điểm chấp nhận</dt>
            <dd>{dash(waybill.acceptingOffice)}</dd>
          </div>
          <div>
            <dt>GDV</dt>
            <dd>{dash(waybill.acceptedBy)}</dd>
          </div>
        </dl>
      </fieldset>
    </div>
  );
}
