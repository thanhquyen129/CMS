import { SPECIAL_FLAGS } from "@/lib/create-workspace";

/** Shared cargo measurement block for Order / Bill / Shipment create. */
export function CreateCargoFields({
  disabled,
  readOnlyTotals,
}: {
  disabled: boolean;
  readOnlyTotals?: boolean;
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
        <label htmlFor="commodity">Loại hàng / Commodity</label>
        <input id="commodity" name="commodity" disabled={disabled} placeholder="Chọn loại hàng" />
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
    </div>
  );
}
