"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { PartyTypeahead } from "@/components/PartyTypeahead";

type CardOption = { id: string; label: string };
type ChargeTypeOption = { id: string; code: string; name: string };
export type ActivationServiceOption = {
  serviceCode: string;
  displayName: string;
  description: string | null;
  isActive: boolean;
  allowedContextTypes: string[];
  defaultUom: string;
};

export type SurchargeFormValues = {
  code?: string;
  name?: string;
  direction?: string;
  calculationMode?: string;
  basis?: string | null;
  currencyCode?: string;
  rateAmountPercent?: number;
  transportMode?: string | null;
  routeCode?: string | null;
  dangerousGoods?: boolean | null;
  rateCardId?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  vatRate?: number | null;
  economicChargeTypeId?: string | null;
  vendorPartyId?: string | null;
  customerPartyId?: string | null;
  customerGroupCode?: string | null;
  activationPolicy?: string | null;
  approvalRef?: string | null;
  serviceCode?: string | null;
  serviceDisplayName?: string | null;
  selectionScope?: string | null;
  predicateSourceField?: string | null;
  predicateOperator?: string | null;
  predicateExpectedValue?: string | null;
};

export function SurchargeForm({
  cards,
  chargeTypes,
  modes,
  activationServices = [],
  initial,
  surchargeId,
  versionId,
  lockCode,
}: {
  cards: CardOption[];
  modes: { value: string; label: string }[];
  chargeTypes: ChargeTypeOption[];
  activationServices?: ActivationServiceOption[];
  initial?: SurchargeFormValues;
  surchargeId?: string;
  versionId?: string;
  lockCode?: boolean;
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [warnings, setWarnings] = useState<string[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [activationPolicy, setActivationPolicy] = useState(initial?.activationPolicy ?? "OPTIONAL_OPT_IN");
  const [serviceCode, setServiceCode] = useState(initial?.serviceCode ?? "");
  const [selectionScope, setSelectionScope] = useState(initial?.selectionScope ?? "BILL");
  const [basis, setBasis] = useState(initial?.basis ?? (activationPolicy === "OPTIONAL_OPT_IN" ? "per_bill" : "chargeable_weight"));
  const editing = Boolean(surchargeId && versionId);

  // Filter or augment activation services
  const serviceOptions = [...activationServices];
  if (serviceCode && !serviceOptions.some((s) => s.serviceCode.toUpperCase() === serviceCode.toUpperCase())) {
    serviceOptions.unshift({
      serviceCode,
      displayName: initial?.serviceDisplayName || serviceCode,
      description: null,
      isActive: true,
      allowedContextTypes: ["BILL", "SHIPMENT", "LEG"],
      defaultUom: "PACKAGE",
    });
  }

  function buildRequestBody(fd: FormData, willPublish: boolean) {
    const amount = Number(String(fd.get("rateAmountPercent") ?? "").replace(",", "."));
    if (!Number.isFinite(amount) || amount < 0) {
      setError("Đơn giá, số tiền hoặc phần trăm không hợp lệ.");
      return null;
    }
    const dg = String(fd.get("dangerousGoods") ?? "");
    const economicChargeTypeId = String(fd.get("economicChargeTypeId") ?? "").trim();
    if (!economicChargeTypeId) {
      setError("Chọn khoản mục kinh tế.");
      return null;
    }
    const direction = String(fd.get("direction") ?? "buy");
    const vendorPartyId = direction === "sell" ? null : String(fd.get("vendorPartyId") ?? "").trim() || null;
    const customerPartyId = direction === "buy" ? null : String(fd.get("customerPartyId") ?? "").trim() || null;
    const customerGroupCode = direction === "buy" ? null : String(fd.get("customerGroupCode") ?? "").trim() || null;
    if (customerPartyId && customerGroupCode) {
      setError("Chọn khách hàng hoặc nhóm khách hàng, không cả hai.");
      return null;
    }
    const policy = String(fd.get("activationPolicy") ?? activationPolicy);
    const selectedSvcCode = String(fd.get("serviceCode") ?? serviceCode ?? "").trim() || null;
    const selectedScope = String(fd.get("selectionScope") ?? selectionScope ?? "").trim() || null;
    const predicateSourceField = String(fd.get("predicateSourceField") ?? "").trim();
    const predicates = predicateSourceField
      ? [{
          sourceField: predicateSourceField,
          operator: String(fd.get("predicateOperator") ?? "eq"),
          expectedValue: String(fd.get("predicateExpectedValue") ?? "true"),
          unknownBehavior: "BLOCK_NEEDS_CONTEXT",
        }]
      : [];

    return {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      direction,
      calculationMode: String(fd.get("calculationMode") ?? "fixed_rate"),
      basis: String(fd.get("basis") ?? basis ?? "").trim() || null,
      currencyCode: String(fd.get("currencyCode") ?? "VND").trim().toUpperCase(),
      rateAmountPercent: amount,
      transportMode: String(fd.get("transportMode") ?? "").trim() || null,
      routeCode: String(fd.get("routeCode") ?? "").trim() || null,
      dangerousGoods: dg === "" ? null : dg === "true",
      rateCardId: String(fd.get("rateCardId") ?? "").trim() || null,
      validFrom: toOffset(fd.get("validFrom")),
      validTo: toOffset(fd.get("validTo")),
      publish: willPublish,
      vatRate: parseVat(fd.get("vatRate")),
      economicChargeTypeId,
      vendorPartyId,
      customerPartyId,
      customerGroupCode,
      activationPolicy: policy,
      approvalRef: String(fd.get("approvalRef") ?? "").trim() || null,
      serviceCode: selectedSvcCode,
      selectionScope: selectedScope,
      activationConfig: {
        serviceCode: selectedSvcCode,
        selectionScope: selectedScope,
        predicates,
      },
    };
  }

  async function handleSaveDraft(form: HTMLFormElement) {
    setError(null);
    setFieldErrors({});
    setWarnings([]);
    setNotice(null);
    setBusy(true);

    const fd = new FormData(form);
    const body = buildRequestBody(fd, false);
    if (!body) {
      setBusy(false);
      return;
    }

    try {
      const url = editing
        ? `/bff/surcharges/${surchargeId}/versions/${versionId}`
        : "/bff/surcharges";
      const res = await fetch(url, {
        method: editing ? "PUT" : "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      const payload = (await res.json().catch(() => ({}))) as {
        message?: string;
        code?: string;
        field?: string;
        warnings?: string[];
        surchargeId?: string;
        versionId?: string;
      };

      if (!res.ok) {
        if (payload.field) {
          setFieldErrors({ [payload.field]: payload.message || "Dữ liệu không hợp lệ." });
        }
        setError(payload.message || "Không lưu được bản nháp phụ phí.");
        return;
      }

      if (payload.warnings && payload.warnings.length > 0) {
        setWarnings(payload.warnings);
        setNotice("Bản nháp đã lưu thành công nhưng chưa đủ điều kiện phát hành.");
      } else {
        setNotice("Đã lưu bản nháp phụ phí thành công.");
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  async function handlePublish(form: HTMLFormElement) {
    setError(null);
    setFieldErrors({});
    setWarnings([]);
    setNotice(null);
    setBusy(true);

    const fd = new FormData(form);
    const policy = String(fd.get("activationPolicy") ?? activationPolicy);
    const selectedSvcCode = String(fd.get("serviceCode") ?? serviceCode ?? "").trim();

    // Field-level client check before publish
    if (policy === "OPTIONAL_OPT_IN" && !selectedSvcCode) {
      setFieldErrors({ service_code: "Phụ phí tùy chọn cần mã dịch vụ liên kết (service_code)." });
      setError("Phụ phí tùy chọn cần mã dịch vụ liên kết (service_code).");
      setBusy(false);
      return;
    }

    const body = buildRequestBody(fd, !editing);
    if (!body) {
      setBusy(false);
      return;
    }

    try {
      if (editing) {
        // First save updated values
        const putRes = await fetch(`/bff/surcharges/${surchargeId}/versions/${versionId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify(body),
        });
        if (!putRes.ok) {
          const putErr = (await putRes.json().catch(() => ({}))) as { message?: string; field?: string };
          if (putErr.field) {
            setFieldErrors({ [putErr.field]: putErr.message || "Lỗi lưu bản nháp." });
          }
          setError(putErr.message || "Không lưu được bản nháp trước khi phát hành.");
          setBusy(false);
          return;
        }

        // Now call publish endpoint
        const pubRes = await fetch(`/bff/surcharges/${surchargeId}/versions/${versionId}/publish`, {
          method: "POST",
          headers: { Accept: "application/json" },
        });
        const pubPayload = (await pubRes.json().catch(() => ({}))) as {
          message?: string;
          code?: string;
          field?: string;
        };

        if (!pubRes.ok) {
          const field = pubPayload.field || (pubPayload.code === "REQUIRED_SERVICE_CODE" || pubPayload.code === "UNKNOWN_SERVICE" || pubPayload.code === "INACTIVE_SERVICE" ? "service_code" : undefined);
          if (field) {
            setFieldErrors({ [field]: pubPayload.message || "Mã dịch vụ không hợp lệ." });
          }
          setError(pubPayload.message || "Không thể phát hành phiên bản phụ phí.");
          setBusy(false);
          return;
        }

        router.push(`/rate-cards/surcharges/${surchargeId}`);
        router.refresh();
      } else {
        // Creating new with publish=true
        const res = await fetch("/bff/surcharges", {
          method: "POST",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify(body),
        });
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
          code?: string;
          field?: string;
          surchargeId?: string;
        };

        if (!res.ok) {
          if (payload.field) {
            setFieldErrors({ [payload.field]: payload.message || "Dữ liệu không hợp lệ." });
          }
          setError(payload.message || "Không tạo được phụ phí.");
          setBusy(false);
          return;
        }

        router.push(`/rate-cards/surcharges/${payload.surchargeId}`);
        router.refresh();
      }
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const dgValue = initial?.dangerousGoods == null ? "" : initial.dangerousGoods ? "true" : "false";

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void handleSaveDraft(e.currentTarget);
      }}
    >
      <div className="form-grid">
        <div className="field">
          <label htmlFor="code">Mã <span className="req">*</span></label>
          <input id="code" name="code" required maxLength={64} defaultValue={initial?.code ?? ""} readOnly={lockCode} />
        </div>
        <div className="field">
          <label htmlFor="name">Tên <span className="req">*</span></label>
          <input id="name" name="name" required maxLength={256} defaultValue={initial?.name ?? ""} />
        </div>
        <div className="field">
          <label htmlFor="economicChargeTypeId">Khoản mục kinh tế <span className="req">*</span></label>
          <select id="economicChargeTypeId" name="economicChargeTypeId" required defaultValue={initial?.economicChargeTypeId ?? ""}>
            <option value="">Chọn khoản mục</option>
            {chargeTypes.map((type) => (
              <option key={type.id} value={type.id}>{type.code} — {type.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="direction">Chiều <span className="req">*</span></label>
          <select id="direction" name="direction" defaultValue={initial?.direction ?? "buy"}>
            <option value="buy">Mua — chi phí dự kiến</option>
            <option value="sell">Bán — doanh thu dự kiến</option>
            <option value="both">Cả hai — khi chính sách cho phép</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="transportMode">Phương thức</label>
          <select id="transportMode" name="transportMode" defaultValue={(initial?.transportMode ?? "").trim().toUpperCase()}>
            <option value="">Không giới hạn</option>
            {(() => {
              const currentMode = (initial?.transportMode ?? "").trim().toUpperCase();
              const options = currentMode && !modes.some((mode) => mode.value === currentMode)
                ? [{ value: currentMode, label: `${currentMode} (Mã hiện tại)` }, ...modes]
                : modes;
              return options.map((mode) => (
                <option key={mode.value} value={mode.value}>{mode.label}</option>
              ));
            })()}
          </select>
        </div>
        <div className="field">
          <label htmlFor="activationPolicy">Chính sách kích hoạt <span className="req">*</span></label>
          <select
            id="activationPolicy"
            name="activationPolicy"
            value={activationPolicy}
            onChange={(e) => {
              const nextPolicy = e.target.value;
              setActivationPolicy(nextPolicy);
              setFieldErrors({});
              if (nextPolicy === "OPTIONAL_OPT_IN" && !basis) {
                setBasis("per_bill");
              }
            }}
          >
            <option value="OPTIONAL_OPT_IN">Tùy chọn có xác nhận (OPTIONAL_OPT_IN)</option>
            <option value="AUTO_CONDITIONAL">Tự động theo điều kiện dữ liệu (AUTO_CONDITIONAL)</option>
            <option value="MANDATORY_BY_POLICY">Bắt buộc theo chính sách (MANDATORY_BY_POLICY)</option>
          </select>
        </div>
        {activationPolicy === "MANDATORY_BY_POLICY" ? (
          <div className="field">
            <label htmlFor="approvalRef">Căn cứ chính sách / Mã phê duyệt <span className="req">*</span></label>
            <input id="approvalRef" name="approvalRef" required maxLength={128} defaultValue={initial?.approvalRef ?? ""} />
          </div>
        ) : null}
        {activationPolicy === "OPTIONAL_OPT_IN" ? (
          <>
            <div className={`field ${fieldErrors.service_code ? "field-has-error" : ""}`}>
              <label htmlFor="serviceCode">Dịch vụ kích hoạt <span className="req">*</span></label>
              <select
                id="serviceCode"
                name="serviceCode"
                value={serviceCode}
                onChange={(e) => {
                  setServiceCode(e.target.value);
                  setFieldErrors((prev) => ({ ...prev, service_code: "" }));
                  setWarnings([]);
                }}
              >
                <option value="">-- Chọn dịch vụ kích hoạt --</option>
                {serviceOptions.map((svc) => (
                  <option key={svc.serviceCode} value={svc.serviceCode}>
                    {svc.serviceCode} — {svc.displayName} ({svc.allowedContextTypes.join(", ")} | {svc.defaultUom})
                  </option>
                ))}
              </select>
              {fieldErrors.service_code ? (
                <div id="service_code_error" className="field-error" style={{ color: "#dc2626", fontSize: "0.875rem", marginTop: "0.25rem", fontWeight: 600 }}>
                  {fieldErrors.service_code}
                </div>
              ) : !serviceCode ? (
                <div className="alert alert-warning sm" style={{ marginTop: "0.25rem", padding: "0.35rem 0.5rem", fontSize: "0.85rem" }}>
                  ⚠️ Cần chọn Dịch vụ kích hoạt trước khi phát hành phiên bản này. Bạn vẫn có thể Lưu nháp.
                </div>
              ) : null}
            </div>
            <div className={`field ${fieldErrors.selection_scope ? "field-has-error" : ""}`}>
              <label htmlFor="selectionScope">Phạm vi lựa chọn <span className="req">*</span></label>
              <select
                id="selectionScope"
                name="selectionScope"
                value={selectionScope}
                onChange={(e) => {
                  setSelectionScope(e.target.value);
                  setFieldErrors((prev) => ({ ...prev, selection_scope: "" }));
                }}
              >
                <option value="BILL">Theo Vận đơn (BILL)</option>
                <option value="SHIPMENT">Theo Lô hàng (SHIPMENT)</option>
                <option value="LEG">Theo Chặng (LEG)</option>
              </select>
              {fieldErrors.selection_scope ? (
                <div id="selection_scope_error" className="field-error" style={{ color: "#dc2626", fontSize: "0.875rem", marginTop: "0.25rem", fontWeight: 600 }}>
                  {fieldErrors.selection_scope}
                </div>
              ) : null}
            </div>
          </>
        ) : null}
        {activationPolicy === "AUTO_CONDITIONAL" ? (
          <>
            <div className="field">
              <label htmlFor="predicateSourceField">Trường dữ liệu nguồn <span className="req">*</span></label>
              <input id="predicateSourceField" name="predicateSourceField" required defaultValue={initial?.predicateSourceField ?? "dangerous_goods"} />
            </div>
            <div className="field">
              <label htmlFor="predicateOperator">Toán tử</label>
              <select id="predicateOperator" name="predicateOperator" defaultValue={initial?.predicateOperator ?? "eq"}>
                <option value="eq">Bằng</option>
                <option value="neq">Khác</option>
                <option value="in">Thuộc danh sách</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="predicateExpectedValue">Giá trị kỳ vọng</label>
              <input id="predicateExpectedValue" name="predicateExpectedValue" defaultValue={initial?.predicateExpectedValue ?? "true"} />
            </div>
          </>
        ) : null}
        <div className="field">
          <label htmlFor="calculationMode">Cách tính <span className="req">*</span></label>
          <select id="calculationMode" name="calculationMode" defaultValue={initial?.calculationMode ?? "fixed_rate"}>
            <option value="fixed_rate">Số tiền cố định (FIXED_RATE)</option>
            <option value="unit_rate">Đơn giá × số lượng (UNIT_RATE)</option>
            <option value="container_rate">Theo container (CONTAINER_RATE)</option>
            <option value="weight_break">Bậc trọng lượng (WEIGHT_BREAK)</option>
            <option value="percentage">Phần trăm cước chính (PERCENTAGE)</option>
            <option value="composite">Công thức có sàn/trần (COMPOSITE)</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="basis">Cơ sở tính / Đơn vị tính số lượng</label>
          <select id="basis" name="basis" value={basis} onChange={(e) => setBasis(e.target.value)}>
            <option value="per_bill">Theo Vận đơn (PER_BILL)</option>
            <option value="per_unit">Theo Đơn vị / Kiện (PER_UNIT)</option>
            <option value="per_package">Theo Kiện bao gói (PER_PACKAGE)</option>
            <option value="per_shipment">Theo Lô hàng (PER_SHIPMENT)</option>
            <option value="per_leg">Theo Chặng (PER_LEG)</option>
            <option value="chargeable_weight">Trọng lượng tính cước (CHARGEABLE_WEIGHT)</option>
            <option value="gross_weight">Trọng lượng thực (GROSS_WEIGHT)</option>
            <option value="quantity">Số lượng (QUANTITY)</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="rateAmountPercent">Đơn giá / số tiền / % <span className="req">*</span></label>
          <input id="rateAmountPercent" name="rateAmountPercent" inputMode="decimal" required defaultValue={initial?.rateAmountPercent ?? ""} />
        </div>
        <div className="field">
          <label htmlFor="currencyCode">Tiền tệ <span className="req">*</span></label>
          <input id="currencyCode" name="currencyCode" required maxLength={3} defaultValue={initial?.currencyCode ?? "VND"} />
        </div>
        <div className="field">
          <label htmlFor="routeCode">Tuyến</label>
          <input id="routeCode" name="routeCode" maxLength={64} defaultValue={initial?.routeCode ?? ""} placeholder="Để trống = mọi tuyến" />
        </div>
        <div className="field">
          <label htmlFor="dangerousGoods">Hàng nguy hiểm</label>
          <select id="dangerousGoods" name="dangerousGoods" defaultValue={dgValue}>
            <option value="">Không giới hạn</option>
            <option value="true">Chỉ hàng nguy hiểm</option>
            <option value="false">Chỉ hàng thường</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="rateCardId">Bảng giá</label>
          <select id="rateCardId" name="rateCardId" defaultValue={initial?.rateCardId ?? ""}>
            <option value="">Mọi bảng giá phù hợp</option>
            {cards.map((card) => (
              <option key={card.id} value={card.id}>{card.label}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="vatRate">Thuế suất VAT (%)</label>
          <input id="vatRate" name="vatRate" inputMode="decimal" placeholder="Để trống nếu chưa khai báo" defaultValue={initial?.vatRate ?? ""} />
        </div>
        <div className="field">
          <PartyTypeahead name="vendorPartyId" label="Nhà cung cấp (mua)" roleCode="vendor" defaultId={initial?.vendorPartyId} hint="Chỉ dùng cho chiều mua. Để trống nếu giá chung." />
        </div>
        <div className="field">
          <PartyTypeahead name="customerPartyId" label="Khách hàng (bán)" roleCode="customer" defaultId={initial?.customerPartyId} hint="Chỉ dùng cho chiều bán. Không chọn cùng nhóm khách hàng." />
        </div>
        <div className="field">
          <label htmlFor="customerGroupCode">Nhóm khách hàng</label>
          <input id="customerGroupCode" name="customerGroupCode" maxLength={64} defaultValue={initial?.customerGroupCode ?? ""} placeholder="Mã nhóm trên đối tác" />
        </div>
        <div className="field">
          <label htmlFor="validFrom">Hiệu lực từ</label>
          <input id="validFrom" name="validFrom" type="date" defaultValue={dateInput(initial?.validFrom)} />
        </div>
        <div className="field">
          <label htmlFor="validTo">Hiệu lực đến</label>
          <input id="validTo" name="validTo" type="date" defaultValue={dateInput(initial?.validTo)} />
        </div>
      </div>

      {editing ? null : (
        <label className="checkbox-field" style={{ marginTop: "1rem" }}>
          <input type="checkbox" name="publish" /> Phát hành ngay sau khi tạo
        </label>
      )}

      {notice ? <div className="alert alert-success" role="status" style={{ marginTop: "1rem" }}>{notice}</div> : null}
      {warnings.length > 0 ? (
        <div className="alert alert-warning" role="alert" style={{ marginTop: "1rem" }}>
          {warnings.map((w, idx) => (
            <p key={idx} style={{ margin: "0.25rem 0" }}>⚠️ {w}</p>
          ))}
        </div>
      ) : null}
      {error ? <div className="alert alert-error" role="alert" style={{ marginTop: "1rem" }}>{error}</div> : null}

      <div className="cta-row" style={{ marginTop: "1.5rem", display: "flex", gap: "0.75rem", alignItems: "center" }}>
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? "Đang xử lý…" : editing ? "Lưu phiên bản nháp" : "Tạo phụ phí"}
        </button>
        {editing ? (
          <button
            className="btn btn-success"
            type="button"
            disabled={busy}
            onClick={(e) => {
              const form = e.currentTarget.closest("form");
              if (form) void handlePublish(form);
            }}
          >
            Phát hành phiên bản
          </button>
        ) : null}
      </div>
    </form>
  );
}

function parseVat(value: FormDataEntryValue | null) {
  const raw = String(value ?? "").trim().replace(",", ".");
  if (!raw) return null;
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

function dateInput(value?: string | null): string {
  if (!value) return "";
  return value.slice(0, 10);
}

function toOffset(value: FormDataEntryValue | null): string | null {
  const text = String(value ?? "").trim();
  if (!text) return null;
  return text.length === 10 ? `${text}T00:00:00Z` : text;
}
