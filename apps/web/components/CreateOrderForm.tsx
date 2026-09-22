"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { CreateCargoFields } from "@/components/CreateCargoFields";
import { CreateCargoLineGrid } from "@/components/CreateCargoLineGrid";
import { CreateSection, CreateWorkspace } from "@/components/CreateWorkspace";
import { LocationField } from "@/components/LocationField";
import { PartyTypeahead } from "@/components/PartyTypeahead";
import { RefTagPicker, type RefHit } from "@/components/RefTagPicker";
import {
  submitCargoLines,
  validateCargoLines,
  type ContainerLineDraft,
  type PackageLineDraft,
} from "@/lib/cargo-lines";
import {
  EXTRA_SERVICES,
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
  routes: CatalogOption[];
  services: CatalogOption[];
  billHits: RefHit[];
  shipmentHits: RefHit[];
  commodities: CatalogOption[];
};

/** UI-02 Tạo đơn hàng — persists operational reference + context, no financial facts. */
export function CreateOrderForm({
  users,
  modes,
  locations,
  routes: _routes,
  services,
  billHits,
  shipmentHits,
  commodities,
}: Props) {
  const router = useRouter();
  const today = new Date().toISOString().slice(0, 10);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const [autoNo, setAutoNo] = useState(false);
  const [orderNo, setOrderNo] = useState("");
  const [mode, setMode] = useState("air");
  const [origin, setOrigin] = useState("");
  const [dest, setDest] = useState("");
  const [customerLabel, setCustomerLabel] = useState("Chưa chọn");
  const [bills, setBills] = useState<RefHit[]>([]);
  const [shipments, setShipments] = useState<RefHit[]>([]);
  const [packages, setPackages] = useState<PackageLineDraft[]>([]);
  const [containers, setContainers] = useState<ContainerLineDraft[]>([]);

  const modeOpts = mergeCatalog(modes, TRANSPORT_MODES);
  const route = useMemo(() => composeRoute(origin || null, dest || null), [origin, dest]);
  const submitting = busy || pending;

  async function submit(status: "draft" | "active") {
    setError(null);
    const form = document.getElementById("create-order-form") as HTMLFormElement | null;
    if (!form) return;
    const fd = new FormData(form);
    let number = String(fd.get("orderNo") ?? "").trim();
    if (autoNo && !number) {
      number = generateBusinessNo("ORD");
      setOrderNo(number);
    }
    if (!number) {
      setError("Nhập mã đơn hàng.");
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
    const cargoErr = validateCargoLines(packages, containers);
    if (cargoErr) {
      setError(cargoErr);
      return;
    }

    const body = {
      orderNo: number,
      sourceSystem: manualSource(),
      externalId: formStr(fd, "externalId") || number,
      operationalStatus: status,
      isActive: status !== "draft",
      applyContext: true,
      customerPartyId,
      assignedUserId: formStr(fd, "assignedUserId"),
      transportMode: formStr(fd, "transportMode") || "air",
      originCode,
      destinationCode,
      routeCode: route,
      etdAt: formDateIso(fd, "etdAt"),
      etaAt: formDateIso(fd, "etaAt"),
      customerReference: formStr(fd, "customerReference"),
      description: formStr(fd, "description"),
      context: {
        serviceType: formStr(fd, "serviceType"),
        incoterm: formStr(fd, "incoterm"),
        requestedAt: formDateIso(fd, "requestedAt"),
        pickupLocation: formStr(fd, "pickupLocation"),
        deliveryLocation: formStr(fd, "deliveryLocation"),
        packageCount: formInt(fd, "packageCount"),
        grossWeightKg: formNum(fd, "grossWeightKg"),
        volumeCbm: formNum(fd, "volumeCbm"),
        chargeableWeightKg: formNum(fd, "chargeableWeightKg"),
        containerCount: formInt(fd, "containerCount"),
        teu: formNum(fd, "teu"),
        commodityTypeId: formStr(fd, "commodityTypeId"),
        chargeableConfirmed: fd.get("chargeableConfirmed") === "true",
        chargeableOverrideReason: formStr(fd, "chargeableOverrideReason"),
        specialFlags: formChecks(fd, "specialFlags"),
        cargoDescription: formStr(fd, "cargoDescription"),
        quoteReference: formStr(fd, "quoteReference"),
        shipperName: formStr(fd, "shipperName"),
        consigneeName: formStr(fd, "consigneeName"),
        extraServices: formChecks(fd, "extraServices"),
        specialInstructions: formStr(fd, "specialInstructions"),
        contactName: formStr(fd, "contactName"),
        contactChannel: formStr(fd, "contactChannel"),
      },
    };

    setBusy(true);
    try {
      const res = await fetch("/bff/orders", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được đơn hàng.");
        return;
      }
      const created = (await res.json()) as { id?: string };
      if (created.id) {
        await linkRefs(`/bff/orders/${created.id}/bills`, bills);
        const cargo = await submitCargoLines("order", created.id, packages, containers);
        if (!cargo.ok) {
          setError(
            `Đơn hàng đã lưu nhưng chưa lưu đủ kiện/container: ${cargo.message} Mở hồ sơ đơn hàng để kiểm tra.`
          );
          startTransition(() => router.push(`/operations/orders/${created.id}`));
          return;
        }
        startTransition(() => router.push(`/operations/orders/${created.id}`));
      } else {
        startTransition(() => router.push("/orders"));
      }
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    await submit("active");
  }

  const actions = (
    <>
      <button className="btn btn-ghost" type="button" disabled={submitting} onClick={() => router.push("/orders")}>
        Hủy
      </button>
      <button className="btn" type="button" disabled={submitting} onClick={() => void submit("draft")}>
        Lưu nháp
      </button>
      <button className="btn" type="submit" form="create-order-form" disabled={submitting}>
        {submitting ? "Đang lưu…" : "✓  Lưu đơn hàng"}
      </button>
    </>
  );

  return (
    <form id="create-order-form" onSubmit={onSubmit} noValidate>
      <CreateWorkspace
        breadcrumbs={[
          { href: "/dashboard", label: "Trang chủ" },
          { href: "/bills", label: "Đơn hàng vận chuyển" },
          { label: "Tạo đơn hàng" },
        ]}
        title="Tạo đơn hàng"
        lede="Ghi nhận yêu cầu vận chuyển của khách hàng làm tham chiếu cho Bill, Shipment và các nghiệp vụ liên quan"
        actions={actions}
        steps={[
          { id: "1", label: "Thông tin chung" },
          { id: "2", label: "Hành trình & dịch vụ" },
          { id: "3", label: "Hàng hóa" },
          { id: "4", label: "Yêu cầu & tham chiếu" },
          { id: "5", label: "Kiểm tra & hoàn tất" },
        ]}
        currentStep="1"
        error={error}
        summary={
          <>
            <h2>Tóm tắt đơn hàng</h2>
            <div className="create-sumrow"><span>Mã đơn hàng</span><b>{orderNo || "Chưa nhập"}</b></div>
            <div className="create-sumrow"><span>Khách hàng</span><b>{customerLabel}</b></div>
            <div className="create-sumrow"><span>Phương thức</span><b>{modeOpts.find((m) => m.value === mode)?.label || mode}</b></div>
            <div className="create-sumrow"><span>Tuyến</span><b>{route || "—"}</b></div>
            <div className="create-sumrow"><span>Bill liên kết</span><b>{bills.length}</b></div>
            <div className="create-sumrow"><span>Shipment liên kết</span><b>{shipments.length}</b></div>
            <div className="create-sumrow"><span>Nguồn dữ liệu</span><b>MANUAL</b></div>
            <div className="create-statusbox">
              <strong>Trạng thái khi lưu</strong>
              <span className="create-tag">Nháp / Đang xử lý</span>
              <p className="muted" style={{ fontSize: 11, lineHeight: 1.5, margin: "8px 0 0" }}>
                Mã nghiệp vụ không đồng nhất với ID nội bộ LCMS. Lưu nháp giữ trạng thái Nháp; Lưu đơn hàng đưa sang Đang xử lý để liên kết Bill.
              </p>
            </div>
            <div className="create-notice">
              <b>ⓘ Lưu ý</b>
              <br />
              Đơn hàng là Operational Reference. Tạo đơn hàng không tự động tạo chi phí, doanh thu, AP/AR hay giao dịch tài chính.
            </div>
            <button className="btn" type="submit" disabled={submitting}>
              ✓  Lưu đơn hàng
            </button>
            <button className="btn btn-ghost" type="button" disabled={submitting} onClick={() => void submit("draft")}>
              Lưu nháp
            </button>
          </>
        }
      >
        <CreateSection title="1. Thông tin chung" hint="Thông tin nhận diện đơn hàng và khách hàng yêu cầu vận chuyển" badge={<span className="create-tag">Nguồn: Nhập thủ công</span>}>
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="orderNo">Mã đơn hàng <span className="req">*</span></label>
              <input id="orderNo" name="orderNo" maxLength={64} value={orderNo} onChange={(e) => setOrderNo(e.target.value)} disabled={submitting} placeholder="Nhập mã đơn hàng" />
              <p className="cw-hint">Có thể nhập theo mã nghiệp vụ hiện có hoặc chọn tự động tạo.</p>
              <div className="create-checks" style={{ marginTop: 7 }}>
                <label>
                  <input type="checkbox" checked={autoNo} onChange={(e) => {
                    const on = e.target.checked;
                    setAutoNo(on);
                    if (on && !orderNo) setOrderNo(generateBusinessNo("ORD"));
                  }} />
                  Tự động tạo mã
                </label>
              </div>
            </div>
            <div className="cw-field">
              <label htmlFor="createdDate">Ngày tạo đơn <span className="req">*</span></label>
              <input id="createdDate" type="date" defaultValue={today} readOnly />
            </div>
            <div className="cw-field">
              <label>Trạng thái</label>
              <input readOnly defaultValue="Nháp" />
            </div>
            <div className="cw-field">
              <label>Nguồn dữ liệu</label>
              <input readOnly defaultValue="Nhập thủ công (MANUAL)" />
            </div>
            <div className="cw-field s6">
              <label>Hệ thống nguồn</label>
              <input readOnly defaultValue="LCMS" />
              <p className="cw-hint">Với API/Import, lưu hệ thống nguồn tương ứng.</p>
            </div>
            <div className="cw-field s6">
              <label htmlFor="externalId">ID tham chiếu hệ thống nguồn</label>
              <input id="externalId" name="externalId" maxLength={128} disabled={submitting} placeholder="Không bắt buộc khi nhập thủ công" />
            </div>
            <div className="cw-field s6">
              <PartyTypeahead
                name="customerPartyId"
                label="Khách hàng"
                roleCode="customer"
                required
                disabled={submitting}
                onSelect={(p) => setCustomerLabel(p ? `${p.code} — ${p.name}` : "Chưa chọn")}
              />
            </div>
            <div className="cw-field s6">
              <label htmlFor="assignedUserId">Nhân viên phụ trách</label>
              <select id="assignedUserId" name="assignedUserId" disabled={submitting} defaultValue="">
                <option value="">Chưa chọn</option>
                {users.map((u) => (
                  <option key={u.value} value={u.value}>{u.label}</option>
                ))}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="contactName">Người liên hệ</label>
              <input id="contactName" name="contactName" disabled={submitting} placeholder="Chọn / nhập người liên hệ" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="contactChannel">Điện thoại / Email</label>
              <input id="contactChannel" name="contactChannel" disabled={submitting} placeholder="Thông tin liên hệ" />
            </div>
            <div className="cw-field s12">
              <label htmlFor="description">Mô tả / Ghi chú đơn hàng</label>
              <textarea id="description" name="description" disabled={submitting} placeholder="Nhập nội dung yêu cầu hoặc ghi chú nghiệp vụ..." />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="2. Hành trình & dịch vụ vận chuyển" hint="Tuyến, phương thức và thời gian khách hàng yêu cầu">
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="transportMode">Phương thức vận chuyển <span className="req">*</span></label>
              <select id="transportMode" name="transportMode" value={mode} onChange={(e) => setMode(e.target.value)} disabled={submitting}>
                {modeOpts.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="serviceType">Loại dịch vụ <span className="req">*</span></label>
              <select id="serviceType" name="serviceType" required disabled={submitting} defaultValue="">
                <option value="" disabled>Chọn dịch vụ</option>
                {services.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
                {services.length === 0 ? <option value="freight">Vận tải</option> : null}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="incoterm">Điều kiện thương mại</label>
              <select id="incoterm" name="incoterm" disabled={submitting} defaultValue="">
                <option value="">Chọn Incoterm</option>
                {INCOTERMS.map((i) => <option key={i.value} value={i.value}>{i.label}</option>)}
              </select>
            </div>
            <LocationField id="originCode" name="originCode" label="Điểm đi" required value={origin} onChange={setOrigin} options={locations} disabled={submitting} />
            <LocationField id="destinationCode" name="destinationCode" label="Điểm đến" required value={dest} onChange={setDest} options={locations} disabled={submitting} />
            <div className="cw-field">
              <label>Tuyến vận chuyển</label>
              <input readOnly value={route || "Tự xác định từ điểm đi → điểm đến"} />
            </div>
            <div className="cw-field">
              <label htmlFor="etdAt">Ngày dự kiến đi (ETD)</label>
              <input id="etdAt" name="etdAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label htmlFor="etaAt">Ngày dự kiến đến (ETA)</label>
              <input id="etaAt" name="etaAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label htmlFor="requestedAt">Ngày khách hàng yêu cầu</label>
              <input id="requestedAt" name="requestedAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field s6">
              <label htmlFor="pickupLocation">Địa điểm lấy hàng</label>
              <input id="pickupLocation" name="pickupLocation" disabled={submitting} placeholder="Nhập/chọn địa điểm lấy hàng" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="deliveryLocation">Địa điểm giao hàng</label>
              <input id="deliveryLocation" name="deliveryLocation" disabled={submitting} placeholder="Nhập/chọn địa điểm giao hàng" />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="3. Thông tin hàng hóa" hint="Dữ liệu ban đầu phục vụ tạo Bill, Shipment và Rating Context">
          <CreateCargoFields
            disabled={submitting}
            commodities={commodities}
            descriptionPlaceholder="Tên hàng, quy cách đóng gói, kích thước, đặc tính cần lưu ý..."
          />
          <div style={{ marginTop: 16 }}>
            <h3 style={{ margin: "0 0 8px", fontSize: "0.9rem" }}>Kiện / Container</h3>
            <CreateCargoLineGrid
              disabled={submitting}
              packages={packages}
              containers={containers}
              onPackagesChange={setPackages}
              onContainersChange={setContainers}
            />
          </div>
        </CreateSection>

        <CreateSection title="4. Yêu cầu dịch vụ & tham chiếu" hint="Thông tin khách hàng cung cấp và các yêu cầu bổ sung">
          <div className="create-grid">
            <div className="cw-field s6">
              <label htmlFor="customerReference">Reference khách hàng</label>
              <input id="customerReference" name="customerReference" disabled={submitting} placeholder="Nhập PO / Booking / Reference..." />
            </div>
            <div className="cw-field s6">
              <label htmlFor="quoteReference">Số báo giá / Rate Reference</label>
              <input id="quoteReference" name="quoteReference" disabled={submitting} placeholder="Nhập hoặc chọn báo giá liên quan nếu có" />
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
              <label>Yêu cầu dịch vụ bổ sung</label>
              <div className="create-checks">
                {EXTRA_SERVICES.map((s) => (
                  <label key={s.value}>
                    <input type="checkbox" name="extraServices" value={s.value} disabled={submitting} />
                    {s.label}
                  </label>
                ))}
              </div>
            </div>
            <div className="cw-field s12">
              <label htmlFor="specialInstructions">Yêu cầu đặc biệt / Chỉ dẫn xử lý</label>
              <textarea id="specialInstructions" name="specialInstructions" disabled={submitting} placeholder="Nhập SLA, thời hạn, chứng từ yêu cầu hoặc chỉ dẫn đặc biệt..." />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="5. Liên kết Bill & Shipment" hint="Đơn hàng có thể liên kết nhiều Bill; Bill cũng có thể liên kết nhiều đơn hàng" badge={<span className="create-tag">Có thể thực hiện sau</span>}>
          <div className="create-grid">
            <RefTagPicker entityType="bill" label="Bill liên quan" placeholder="Tìm Bill để liên kết" hint="Có thể tạo/gán Bill sau." selected={bills} onChange={setBills} initialHits={billHits} />
            <RefTagPicker entityType="shipment" label="Shipment liên quan" placeholder="Tìm Shipment để tham chiếu" hint="Shipment thường hình thành sau khi có Bill. Liên kết Shipment thực hiện trên hồ sơ Shipment." selected={shipments} onChange={setShipments} initialHits={shipmentHits} />
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

async function linkRefs(base: string, refs: RefHit[]) {
  for (const r of refs) {
    await fetch(`${base}/${encodeURIComponent(r.id)}`, {
      method: "POST",
      headers: { Accept: "application/json" },
    });
  }
}
