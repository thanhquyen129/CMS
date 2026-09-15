"use client";

import { useState } from "react";
import {
  inferAddressScheme,
  VN_PROVINCES_NEW,
  type VnAddressScheme,
} from "@/lib/vn-admin";

type Props = {
  disabled?: boolean;
  defaults?: {
    addressLine1?: string | null;
    addressLine2?: string | null;
    ward?: string | null;
    district?: string | null;
    city?: string | null;
    province?: string | null;
    countryCode?: string | null;
    postalCode?: string | null;
  };
};

export function VnAddressFields({ disabled, defaults }: Props) {
  const [scheme, setScheme] = useState<VnAddressScheme>(() =>
    inferAddressScheme(defaults?.district)
  );

  const provinceDefault =
    defaults?.province?.trim() ||
    defaults?.city?.trim() ||
    "";

  return (
    <>
      <div className="address-scheme" role="radiogroup" aria-label="Kiểu địa chỉ">
        <label className="address-scheme-option">
          <input
            type="radio"
            name="addressSchemeUi"
            checked={scheme === "new"}
            disabled={disabled}
            onChange={() => setScheme("new")}
          />
          <span>
            <strong>Địa chỉ mới</strong>
            <span className="muted"> Phường/Xã → Tỉnh/Thành phố</span>
          </span>
        </label>
        <label className="address-scheme-option">
          <input
            type="radio"
            name="addressSchemeUi"
            checked={scheme === "legacy"}
            disabled={disabled}
            onChange={() => setScheme("legacy")}
          />
          <span>
            <strong>Địa chỉ cũ</strong>
            <span className="muted">
              {" "}
              Phường/Xã → Quận/Huyện → Tỉnh/Thành phố
            </span>
          </span>
        </label>
      </div>

      <div className="form-grid">
        <div className="field">
          <label htmlFor="editAddr1">Số nhà, đường</label>
          <input
            id="editAddr1"
            name="addressLine1"
            defaultValue={defaults?.addressLine1 ?? ""}
            disabled={disabled}
            placeholder="Số nhà, tên đường…"
          />
        </div>
        <div className="field">
          <label htmlFor="editAddr2">Địa chỉ bổ sung</label>
          <input
            id="editAddr2"
            name="addressLine2"
            defaultValue={defaults?.addressLine2 ?? ""}
            disabled={disabled}
            placeholder="Tòa nhà, khu vực… (không bắt buộc)"
          />
        </div>
        <div className="field">
          <label htmlFor="editWard">Phường/Xã</label>
          <input
            id="editWard"
            name="ward"
            defaultValue={defaults?.ward ?? ""}
            disabled={disabled}
          />
        </div>

        {scheme === "legacy" ? (
          <div className="field">
            <label htmlFor="editDistrict">Quận/Huyện</label>
            <input
              id="editDistrict"
              name="district"
              defaultValue={defaults?.district ?? ""}
              disabled={disabled}
            />
          </div>
        ) : (
          <input type="hidden" name="district" value="" />
        )}

        <div className="field">
          <label htmlFor="editProvince">Tỉnh/Thành phố</label>
          <input
            id="editProvince"
            name="province"
            list="vn-provinces-datalist"
            defaultValue={provinceDefault}
            disabled={disabled}
            placeholder="Chọn hoặc nhập tỉnh/TP…"
          />
          <datalist id="vn-provinces-datalist">
            {VN_PROVINCES_NEW.map((p) => (
              <option key={p} value={p} />
            ))}
          </datalist>
        </div>

        {/* city đồng bộ với tỉnh/TP khi lưu — không hiển thị trùng */}
        <input type="hidden" name="city" value="" />

        <div className="field">
          <label htmlFor="editCountry">Quốc gia</label>
          <input
            id="editCountry"
            name="countryCode"
            maxLength={2}
            defaultValue={defaults?.countryCode ?? "VN"}
            disabled={disabled}
          />
        </div>
        <div className="field">
          <label htmlFor="editPostal">Mã bưu chính</label>
          <input
            id="editPostal"
            name="postalCode"
            defaultValue={defaults?.postalCode ?? ""}
            disabled={disabled}
          />
        </div>
      </div>
    </>
  );
}
