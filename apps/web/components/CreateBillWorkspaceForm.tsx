"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { CreateCargoFields } from "@/components/CreateCargoFields";
import { CreateSection, CreateWorkspace } from "@/components/CreateWorkspace";
import { LocationField } from "@/components/LocationField";
import { PartyTypeahead } from "@/components/PartyTypeahead";
import { RefTagPicker, type RefHit } from "@/components/RefTagPicker";
import {
  BILL_TYPES,
  INCOTERMS,
  TRANSPORT_MODES,
  composeRoute,
  formChecks,
  formDateIso,
  formInt,
  formNum,
  formStr,
  generateBusinessNo,
  manualSource,
  mergeCatalog,
  type CatalogOption,
} from "@/lib/create-workspace";

type Props = {
  users: CatalogOption[];
  modes: CatalogOption[];
  locations: CatalogOption[];
  services: CatalogOption[];
  currencies: CatalogOption[];
  vendors: CatalogOption[];
  orderHits: RefHit[];
  shipmentHits: RefHit[];
};

/** UI-02 Tạo Bill — Financial Anchor; does not create cost/revenue. */
export function CreateBillWorkspaceForm({
  users,
  modes,
  locations,
  services,
  currencies,
  vendors,
  orderHits,
  shipmentHits,
}: Props) {
  const router = useRouter();
  const today = new Date().toISOString().slice(0, 10);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const [billNo, setBillNo] = useState("");
  const [mode, setMode] = useState("air");
  const [origin, setOrigin] = useState("");
  const [dest, setDest] = useState("");
  const [customerLabel, setCustomerLabel] = useState("Chưa chọn");
  const [orders, setOrders] = useState<RefHit[]>([]);
  const [shipments, setShipments] = useState<RefHit[]>([]);
  const modeOpts = mergeCatalog(modes, TRANSPORT_MODES);
  const route = useMemo(() => composeRoute(origin || null, dest || null), [origin, dest]);
  const submitting = busy || pending;

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    await save();
  }

  async function save() {
    setError(null);
    const form = document.getElementById("create-bill-form") as HTMLFormElement | null;
    if (!form) return;
    const fd = new FormData(form);
    const number = String(fd.get("billNo") ?? "").trim();
    if (!number) {
      setError("Nhập số Bill.");
      return;
    }
    const customerPartyId = formStr(fd, "customerPartyId");
    if (!customerPartyId) {
      setError("Chọn khách hàng.");
      return;
    }
    const originCode = formStr(fd, "originCode");
    const destinationCode = formStr(fd, "destinationCode");
    if (!originCode || !destinationCode) {
      setError("Chọn điểm đi và điểm đến.");
      return;
    }

    setBusy(true);
    try {
      const createdRes = await fetch("/bff/bills", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          billNo: number,
          billType: formStr(fd, "billType") || "house",
          sourceSystem: manualSource(),
          externalId: number,
          customerPartyId,
        }),
      });
      if (createdRes.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!createdRes.ok) {
        const payload = (await createdRes.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không tạo được Bill.");
        return;
      }
      const created = (await createdRes.json()) as { id?: string };
      const billId = created.id;
      if (!billId) {
        setError("Máy chủ không trả về mã Bill.");
        return;
      }

      const patch = await fetch(`/bff/bills/${billId}/context`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          customerPartyId,
          routeCode: route,
          etdAt: formDateIso(fd, "etdAt"),
          etaAt: formDateIso(fd, "etaAt"),
          assignedUserId: formStr(fd, "assignedUserId"),
          description: formStr(fd, "description"),
          transportMode: formStr(fd, "transportMode") || "air",
          originCode,
          destinationCode,
          customerReference: formStr(fd, "customerReference"),
          context: {
            serviceType: formStr(fd, "serviceType"),
            incoterm: formStr(fd, "incoterm"),
            preferredCurrency: formStr(fd, "preferredCurrency") || "VND",
            vendorPartyId: formStr(fd, "vendorPartyId"),
            rateDatePolicy: formStr(fd, "rateDatePolicy"),
            shipperName: formStr(fd, "shipperName"),
            consigneeName: formStr(fd, "consigneeName"),
            packageCount: formInt(fd, "packageCount"),
            grossWeightKg: formNum(fd, "grossWeightKg"),
            volumeCbm: formNum(fd, "volumeCbm"),
            chargeableWeightKg: formNum(fd, "chargeableWeightKg"),
            containerCount: formInt(fd, "containerCount"),
            teu: formNum(fd, "teu"),
            commodity: formStr(fd, "commodity"),
            specialFlags: formChecks(fd, "specialFlags"),
            cargoDescription: formStr(fd, "cargoDescription"),
            masterBillNo: formStr(fd, "masterBillNo"),
          },
        }),
      });
      if (!patch.ok && patch.status !== 204) {
        const payload = (await patch.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Bill đã tạo nhưng chưa lưu đủ ngữ cảnh.");
        startTransition(() => router.push(`/bills/${billId}`));
        return;
      }

      const shipper = formStr(fd, "shipperName");
      const consignee = formStr(fd, "consigneeName");
      const pkgs = formInt(fd, "packageCount");
      const weight = formNum(fd, "grossWeightKg");
      if (shipper || consignee || pkgs || weight) {
        await fetch(`/bff/bills/${billId}/waybill`, {
          method: "PUT",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify({
            sender: { name: shipper },
            consignee: { name: consignee },
            parcelCount: pkgs ?? 1,
            actualWeightKg: weight,
            chargeableWeightKg: formNum(fd, "chargeableWeightKg"),
            contentsDescription: formStr(fd, "cargoDescription"),
            contentsQuantity: pkgs,
            currencyCode: formStr(fd, "preferredCurrency") || "VND",
          }),
        });
      }

      for (const o of orders) {
        await fetch(`/bff/orders/${encodeURIComponent(o.id)}/bills/${encodeURIComponent(billId)}`, {
          method: "POST",
          headers: { Accept: "application/json" },
        });
      }
      for (const s of shipments) {
        await fetch(`/bff/shipments/${encodeURIComponent(s.id)}/bills/${encodeURIComponent(billId)}`, {
          method: "POST",
          headers: { Accept: "application/json" },
        });
      }

      startTransition(() => router.push(`/bills/${billId}`));
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const actions = (
    <>
      <button className="btn btn-ghost" type="button" disabled={submitting} onClick={() => router.push("/bills")}>
        Hủy
      </button>
      <button className="btn" type="submit" form="create-bill-form" disabled={submitting}>
        {submitting ? "Đang lưu…" : "✓  Lưu Bill"}
      </button>
    </>
  );

  return (
    <form id="create-bill-form" onSubmit={onSubmit} noValidate>
      <CreateWorkspace
        breadcrumbs={[
          { href: "/dashboard", label: "Trang chủ" },
          { href: "/bills", label: "Đơn hàng vận chuyển" },
          { label: "Tạo Bill" },
        ]}
        title="Tạo Bill"
        lede="Tạo vận đơn và khai báo thông tin tham chiếu phục vụ tính giá, chi phí, doanh thu và kiểm soát tài chính"
        actions={actions}
        steps={[
          { id: "1", label: "Thông tin chung" },
          { id: "2", label: "Hành trình & hàng hóa" },
          { id: "3", label: "Liên kết nghiệp vụ" },
          { id: "4", label: "Thông tin tính giá" },
          { id: "5", label: "Kiểm tra & hoàn tất" },
        ]}
        currentStep="1"
        error={error}
        summary={
          <>
            <h2>Tóm tắt Bill</h2>
            <div className="create-sumrow"><span>Số Bill</span><b>{billNo || "Chưa nhập"}</b></div>
            <div className="create-sumrow"><span>Khách hàng</span><b>{customerLabel}</b></div>
            <div className="create-sumrow"><span>Phương thức</span><b>{modeOpts.find((m) => m.value === mode)?.label || mode}</b></div>
            <div className="create-sumrow"><span>Tuyến</span><b>{route || "—"}</b></div>
            <div className="create-sumrow"><span>Order liên kết</span><b className="blue">{orders.length}</b></div>
            <div className="create-sumrow"><span>Shipment liên kết</span><b>{shipments.length}</b></div>
            <div className="create-sumrow"><span>Nguồn dữ liệu</span><b>Manual</b></div>
            <div className="create-statusbox">
              <strong>Trạng thái khi lưu</strong>
              <span className="create-tag">Đang xử lý</span>
              <p className="muted" style={{ fontSize: 11, lineHeight: 1.5, margin: "8px 0 0" }}>
                Bill được tạo trong LCMS được đánh dấu nguồn thủ công để phân biệt với Bill nhận qua Import/API.
              </p>
            </div>
            <div className="create-notice">
              <b>ⓘ Lưu ý</b>
              <br />
              Bill là tham chiếu vận hành tối thiểu và Financial Anchor. Việc tạo Bill không tự động tạo chi phí hoặc doanh thu thực tế. Rating chỉ phát sinh khi đủ dữ liệu và người dùng thực hiện nghiệp vụ tính giá.
            </div>
            <button className="btn" type="submit" disabled={submitting}>✓  Lưu Bill</button>
          </>
        }
      >
        <CreateSection title="1. Thông tin chung" hint="Thông tin nhận diện và chủ thể của Bill" badge={<span className="create-tag">Nguồn: Nhập thủ công</span>}>
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="billNo">Số Bill <span className="req">*</span></label>
              <input id="billNo" name="billNo" maxLength={64} value={billNo} onChange={(e) => setBillNo(e.target.value)} disabled={submitting} placeholder="Nhập số Bill" />
              <p className="cw-hint">Có thể nhập HAWB/HBL hoặc số Bill nội bộ theo cấu hình.</p>
              <div className="create-checks" style={{ marginTop: 7 }}>
                <label>
                  <input type="checkbox" onChange={(e) => { if (e.target.checked && !billNo) setBillNo(generateBusinessNo("BILL")); }} />
                  Tự động tạo mã
                </label>
              </div>
            </div>
            <div className="cw-field">
              <label htmlFor="billType">Loại Bill <span className="req">*</span></label>
              <select id="billType" name="billType" disabled={submitting} defaultValue="house">
                {BILL_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="transportMode">Phương thức vận chuyển <span className="req">*</span></label>
              <select id="transportMode" name="transportMode" value={mode} onChange={(e) => setMode(e.target.value)} disabled={submitting}>
                {modeOpts.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <PartyTypeahead name="customerPartyId" label="Khách hàng" roleCode="customer" required disabled={submitting} onSelect={(p) => setCustomerLabel(p ? `${p.code} — ${p.name}` : "Chưa chọn")} />
            </div>
            <div className="cw-field s6">
              <label htmlFor="assignedUserId">Nhân viên phụ trách</label>
              <select id="assignedUserId" name="assignedUserId" disabled={submitting} defaultValue="">
                <option value="">Chưa chọn</option>
                {users.map((u) => <option key={u.value} value={u.value}>{u.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="shipperName">Người gửi hàng (Shipper)</label>
              <input id="shipperName" name="shipperName" disabled={submitting} placeholder="Chọn hoặc nhập người gửi hàng" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="consigneeName">Người nhận hàng (Consignee)</label>
              <input id="consigneeName" name="consigneeName" disabled={submitting} placeholder="Chọn hoặc nhập người nhận hàng" />
            </div>
            <div className="cw-field s12">
              <label htmlFor="description">Mô tả / Ghi chú Bill</label>
              <textarea id="description" name="description" disabled={submitting} placeholder="Nhập mô tả ngắn hoặc ghi chú nghiệp vụ..." />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="2. Hành trình & thời gian" hint="Thông tin tuyến và mốc thời gian dự kiến">
          <div className="create-grid">
            <LocationField id="originCode" name="originCode" label="Điểm đi" required value={origin} onChange={setOrigin} options={locations} disabled={submitting} />
            <LocationField id="destinationCode" name="destinationCode" label="Điểm đến" required value={dest} onChange={setDest} options={locations} disabled={submitting} />
            <div className="cw-field">
              <label>Tuyến vận chuyển</label>
              <input readOnly value={route || "Tự xác định từ điểm đi → điểm đến"} />
            </div>
            <div className="cw-field">
              <label htmlFor="etdAt">Ngày ETD</label>
              <input id="etdAt" name="etdAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label htmlFor="etaAt">Ngày ETA</label>
              <input id="etaAt" name="etaAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label>Ngày tạo Bill</label>
              <input type="date" defaultValue={today} readOnly />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="3. Thông tin hàng hóa & đo lường" hint="Dữ liệu phục vụ rating context và phân bổ chi phí/doanh thu">
          <CreateCargoFields disabled={submitting} />
          <div className="create-grid" style={{ marginTop: 11 }}>
            <div className="cw-field s12">
              <label htmlFor="cargoDescription">Mô tả hàng hóa</label>
              <textarea id="cargoDescription" name="cargoDescription" disabled={submitting} placeholder="Tên hàng, quy cách đóng gói, đặc tính cần lưu ý..." />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="4. Liên kết nghiệp vụ" hint="Bill có thể liên kết nhiều Order và nhiều Shipment">
          <div className="create-grid">
            <RefTagPicker entityType="order" label="Order liên quan" placeholder="Tìm Order để liên kết" selected={orders} onChange={setOrders} initialHits={orderHits} />
            <RefTagPicker entityType="shipment" label="Shipment liên quan" placeholder="Tìm Shipment để liên kết" hint="Có thể để trống khi tạo Bill và gán Shipment sau." selected={shipments} onChange={setShipments} initialHits={shipmentHits} />
            <div className="cw-field s6">
              <label htmlFor="customerReference">Reference khách hàng</label>
              <input id="customerReference" name="customerReference" disabled={submitting} placeholder="Nhập PO / Booking / Reference..." />
            </div>
            <div className="cw-field s6">
              <label htmlFor="masterBillNo">Master Bill</label>
              <input id="masterBillNo" name="masterBillNo" disabled={submitting} placeholder="Nhập/chọn MAWB / MBL nếu có" />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="5. Thông tin phục vụ tính giá" hint="Rating Engine sử dụng dữ liệu này cùng Rate Card phù hợp để tính Expected Cost/Revenue" badge={<span className="create-tag">Không tạo Cost/Revenue trực tiếp</span>}>
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="serviceType">Loại dịch vụ</label>
              <select id="serviceType" name="serviceType" disabled={submitting} defaultValue="">
                <option value="">Chọn dịch vụ</option>
                {services.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="incoterm">Điều kiện thương mại</label>
              <select id="incoterm" name="incoterm" disabled={submitting} defaultValue="">
                <option value="">Chọn Incoterm</option>
                {INCOTERMS.map((i) => <option key={i.value} value={i.value}>{i.label}</option>)}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="preferredCurrency">Tiền tệ ưu tiên</label>
              <select id="preferredCurrency" name="preferredCurrency" disabled={submitting} defaultValue="VND">
                {(currencies.length ? currencies : [{ value: "VND", label: "VND" }, { value: "USD", label: "USD" }]).map((c) => (
                  <option key={c.value} value={c.value}>{c.label}</option>
                ))}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="vendorPartyId">Nhà cung cấp / Hãng dự kiến</label>
              <select id="vendorPartyId" name="vendorPartyId" disabled={submitting} defaultValue="">
                <option value="">Chọn nhà cung cấp/hãng nếu đã biết</option>
                {vendors.map((v) => <option key={v.value} value={v.value}>{v.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="rateDatePolicy">Ngày áp dụng giá</label>
              <select id="rateDatePolicy" name="rateDatePolicy" disabled={submitting} defaultValue="etd">
                <option value="etd">Theo ETD</option>
                <option value="created">Theo ngày tạo</option>
                <option value="tenant">Theo cấu hình Tenant</option>
              </select>
            </div>
          </div>
        </CreateSection>

        <div className="create-footer">
          <span className="muted">Các trường có dấu <span className="req">*</span> là bắt buộc.</span>
          <div className="right">{actions}</div>
        </div>
      </CreateWorkspace>
    </form>
  );
}
