import { SPECIAL_FLAGS, type CatalogOption } from "@/lib/create-workspace";

/** Shared cargo measurement block for Order / Bill / Shipment create. */
export function CreateCargoFields({
  disabled,
  readOnlyTotals,
  commodities,
  descriptionPlaceholder,
}: {
  disabled: boolean;
  readOnlyTotals?: boolean;
  commodities: CatalogOption[];
  descriptionPlaceholder?: string;
}) {
  return (
    <div className="create-grid">
      <div className="cw-field">
        <label htmlFor="packageCount">Số kiện</label>
        <input id="packageCount" name="packageCount" inputMode="numeric" placeholder="0" disabled={disabled} readOnly={readOnlyTotals} />
      </div>
      <div className="cw-field">
        <label htmlFor="grossWeightKg">Trọng lượng thực (kg)</label>
        <input id="grossWeightKg" name="grossWeightKg" inputMode="decimal" placeholder="0,00" disabled={disabled} readOnly={readOnlyTotals} />
      </div>
      <div className="cw-field">
        <label htmlFor="volumeCbm">Thể tích (CBM)</label>
        <input id="volumeCbm" name="volumeCbm" inputMode="decimal" placeholder="0,000" disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="chargeableWeightKg">Trọng lượng tính cước (kg)</label>
        <input id="chargeableWeightKg" name="chargeableWeightKg" inputMode="decimal" placeholder="0,00" disabled={disabled} />
        <p className="cw-hint">Có thể được hệ thống tính theo cấu hình nghiệp vụ.</p>
      </div>
      <div className="cw-field">
        <label htmlFor="containerCount">Số lượng container</label>
        <input id="containerCount" name="containerCount" inputMode="numeric" placeholder="0" disabled={disabled} />
      </div>
      <div className="cw-field">
        <label htmlFor="teu">TEU</label>
        <input id="teu" name="teu" inputMode="decimal" placeholder="0,00" disabled={disabled} />
      </div>
      <div className="cw-field s6">
        <label htmlFor="commodityTypeId">Loại hàng / Commodity</label>
        <select id="commodityTypeId" name="commodityTypeId" disabled={disabled} defaultValue="">
          <option value="">Chọn loại hàng</option>
          {commodities.map((c) => (
            <option key={c.value} value={c.value}>{c.label}</option>
          ))}
        </select>
      </div>
      <div className="cw-field s6">
        <label>Thuộc tính đặc biệt</label>
        <div className="create-checks">
          {SPECIAL_FLAGS.map((f) => (
            <label key={f.value}>
              <input type="checkbox" name="specialFlags" value={f.value} disabled={disabled} />
              {f.label}
            </label>
          ))}
        </div>
      </div>
      {descriptionPlaceholder ? (
        <div className="cw-field s12">
          <label htmlFor="cargoDescription">Mô tả hàng hóa</label>
          <textarea id="cargoDescription" name="cargoDescription" disabled={disabled} placeholder={descriptionPlaceholder} />
        </div>
      ) : null}
      <div className="cw-field s6">
        <label className="create-checks">
          <input type="checkbox" name="chargeableConfirmed" value="true" disabled={disabled} />
          Đã xác nhận trọng lượng tính cước
        </label>
      </div>
      <div className="cw-field s6">
        <label htmlFor="chargeableOverrideReason">Lý do ghi đè</label>
        <input id="chargeableOverrideReason" name="chargeableOverrideReason" disabled={disabled} placeholder="Bắt buộc khi sửa số đã xác nhận hoặc trường thuộc hệ thống nguồn" />
      </div>
    </div>
  );
}
