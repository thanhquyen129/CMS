"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { CreateCargoFields } from "@/components/CreateCargoFields";
import { CreateSection, CreateWorkspace } from "@/components/CreateWorkspace";
import { LocationField } from "@/components/LocationField";
import { RefTagPicker, type RefHit } from "@/components/RefTagPicker";
import {
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
  rateCards: CatalogOption[];
  billHits: RefHit[];
};

/** UI-02 Tạo Shipment — cost collection point; does not seed cost lines. */
export function CreateShipmentForm({
  users,
  modes,
  locations,
  services,
  currencies,
  vendors,
  rateCards,
  billHits,
}: Props) {
  const router = useRouter();
  const today = new Date().toISOString().slice(0, 10);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const [autoNo, setAutoNo] = useState(false);
  const [shipmentNo, setShipmentNo] = useState("");
  const [mode, setMode] = useState("air");
  const [origin, setOrigin] = useState("");
  const [dest, setDest] = useState("");
  const [bills, setBills] = useState<RefHit[]>([]);
  const modeOpts = mergeCatalog(modes, TRANSPORT_MODES);
  const route = useMemo(() => composeRoute(origin || null, dest || null), [origin, dest]);
  const submitting = busy || pending;

  async function submit(status: "draft" | "active") {
    setError(null);
    const form = document.getElementById("create-shipment-form") as HTMLFormElement | null;
    if (!form) return;
    const fd = new FormData(form);
    let number = String(fd.get("shipmentNo") ?? "").trim();
    if (autoNo && !number) {
      number = generateBusinessNo("SHP");
      setShipmentNo(number);
    }
    if (!number) {
      setError("Nhập mã Shipment.");
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
      const res = await fetch("/bff/shipments", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          shipmentNo: number,
          sourceSystem: manualSource(),
          externalId: formStr(fd, "externalId") || number,
          operationalStatus: status,
          isActive: status !== "draft",
          applyContext: true,
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
            carrierName: formStr(fd, "carrierName"),
            packageCount: formInt(fd, "packageCount"),
            grossWeightKg: formNum(fd, "grossWeightKg"),
            volumeCbm: formNum(fd, "volumeCbm"),
            chargeableWeightKg: formNum(fd, "chargeableWeightKg"),
            teu: formNum(fd, "teu"),
            commodity: formStr(fd, "commodity"),
            specialFlags: formChecks(fd, "specialFlags"),
            preferredCurrency: formStr(fd, "preferredCurrency") || "VND",
            vendorPartyId: formStr(fd, "vendorPartyId"),
            buyRateCardId: formStr(fd, "buyRateCardId"),
            rateDate: formDateIso(fd, "rateDate"),
            ratingNote: formStr(fd, "ratingNote"),
          },
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được Shipment.");
        return;
      }
      const created = (await res.json()) as { id?: string };
      if (created.id) {
        for (const b of bills) {
          await fetch(`/bff/shipments/${created.id}/bills/${encodeURIComponent(b.id)}`, {
            method: "POST",
            headers: { Accept: "application/json" },
          });
        }
        startTransition(() => router.push(`/operations/shipments/${created.id}`));
      } else {
        startTransition(() => router.push("/shipments"));
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
      <button className="btn btn-ghost" type="button" disabled={submitting} onClick={() => router.push("/shipments")}>
        Hủy
      </button>
      <button className="btn" type="button" disabled={submitting} onClick={() => void submit("draft")}>
        Lưu nháp
      </button>
      <button className="btn" type="submit" form="create-shipment-form" disabled={submitting}>
        {submitting ? "Đang lưu…" : "✓  Lưu Shipment"}
      </button>
    </>
  );

  return (
    <form id="create-shipment-form" onSubmit={onSubmit} noValidate>
      <CreateWorkspace
        breadcrumbs={[
          { href: "/dashboard", label: "Trang chủ" },
          { href: "/bills", label: "Đơn hàng vận chuyển" },
          { label: "Tạo Shipment" },
        ]}
        title="Tạo Shipment"
        lede="Tạo chuyến gom/lô vận chuyển và liên kết các Bill cùng hành trình để quản lý chi phí, phân bổ và truy vết tài chính"
        actions={actions}
        steps={[
          { id: "1", label: "Thông tin chung" },
          { id: "2", label: "Hành trình" },
          { id: "3", label: "Bill liên kết" },
          { id: "4", label: "Hàng hóa & đo lường" },
          { id: "5", label: "Kiểm tra & hoàn tất" },
        ]}
        currentStep="1"
        error={error}
        summary={
          <>
            <h2>Tóm tắt Shipment</h2>
            <div className="create-sumrow"><span>Mã Shipment</span><b>{shipmentNo || "Chưa nhập"}</b></div>
            <div className="create-sumrow"><span>Phương thức</span><b>{modeOpts.find((m) => m.value === mode)?.label || mode}</b></div>
            <div className="create-sumrow"><span>Tuyến</span><b>{route || "—"}</b></div>
            <div className="create-sumrow"><span>Số Bill</span><b>{bills.length}</b></div>
            <div className="create-sumrow"><span>Số Chặng</span><b>0</b></div>
            <div className="create-sumrow"><span>Số Chuyến</span><b>0</b></div>
            <div className="create-sumrow"><span>Nguồn dữ liệu</span><b>MANUAL</b></div>
            <div className="create-statusbox">
              <strong>Trạng thái khi lưu</strong>
              <span className="create-tag">Nháp / Đang xử lý</span>
              <p className="muted" style={{ fontSize: 11, lineHeight: 1.5, margin: "8px 0 0" }}>
                Mã nghiệp vụ không đồng nhất với ID nội bộ LCMS. Chặng/Chuyến thêm sau trên hồ sơ Shipment — không điều phối TMS.
              </p>
            </div>
            <div className="create-notice">
              <b>ⓘ Lưu ý nghiệp vụ</b>
              <br />
              Shipment là điểm tập hợp chi phí và có thể là nguồn phân bổ xuống Bill. Tạo Shipment không tự động sinh chi phí.
            </div>
            <button className="btn" type="submit" disabled={submitting}>✓  Lưu Shipment</button>
            <button className="btn btn-ghost" type="button" disabled={submitting} onClick={() => void submit("draft")}>Lưu nháp</button>
          </>
        }
      >
        <CreateSection title="1. Thông tin chung" hint="Thông tin nhận diện Shipment và nguồn dữ liệu" badge={<span className="create-tag">Nguồn: Nhập thủ công</span>}>
          <div className="create-grid">
            <div className="cw-field">
              <label htmlFor="shipmentNo">Mã Shipment <span className="req">*</span></label>
              <input id="shipmentNo" name="shipmentNo" maxLength={64} value={shipmentNo} onChange={(e) => setShipmentNo(e.target.value)} disabled={submitting} placeholder="Nhập mã Shipment" />
              <p className="cw-hint">Có thể nhập theo mã nghiệp vụ hiện có hoặc chọn tự động tạo.</p>
              <div className="create-checks" style={{ marginTop: 7 }}>
                <label>
                  <input type="checkbox" checked={autoNo} onChange={(e) => {
                    const on = e.target.checked;
                    setAutoNo(on);
                    if (on && !shipmentNo) setShipmentNo(generateBusinessNo("SHP"));
                  }} />
                  Tự động tạo mã
                </label>
              </div>
            </div>
            <div className="cw-field">
              <label>Ngày tạo <span className="req">*</span></label>
              <input type="date" defaultValue={today} readOnly />
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
            </div>
            <div className="cw-field s6">
              <label htmlFor="externalId">ID tham chiếu hệ thống nguồn</label>
              <input id="externalId" name="externalId" maxLength={128} disabled={submitting} placeholder="Không bắt buộc khi nhập thủ công" />
            </div>
            <div className="cw-field s6">
              <label htmlFor="transportMode">Phương thức vận chuyển <span className="req">*</span></label>
              <select id="transportMode" name="transportMode" value={mode} onChange={(e) => setMode(e.target.value)} disabled={submitting}>
                {modeOpts.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="serviceType">Loại dịch vụ</label>
              <select id="serviceType" name="serviceType" disabled={submitting} defaultValue="">
                <option value="">Chọn dịch vụ</option>
                {services.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="assignedUserId">Nhân viên phụ trách</label>
              <select id="assignedUserId" name="assignedUserId" disabled={submitting} defaultValue="">
                <option value="">Chưa chọn</option>
                {users.map((u) => <option key={u.value} value={u.value}>{u.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="customerReference">Reference ngoài hệ thống</label>
              <input id="customerReference" name="customerReference" disabled={submitting} placeholder="Booking / consol / reference..." />
            </div>
            <div className="cw-field s12">
              <label htmlFor="description">Mô tả / Ghi chú Shipment</label>
              <textarea id="description" name="description" disabled={submitting} placeholder="Nhập ghi chú nghiệp vụ..." />
            </div>
          </div>
        </CreateSection>

        <CreateSection title="2. Hành trình vận chuyển" hint="Shipment có thể gồm một hoặc nhiều Chặng; mỗi Chặng có thể được thực hiện bởi một hoặc nhiều Chuyến">
          <div className="create-grid">
            <LocationField id="originCode" name="originCode" label="Điểm đi" required value={origin} onChange={setOrigin} options={locations} disabled={submitting} />
            <LocationField id="destinationCode" name="destinationCode" label="Điểm đến" required value={dest} onChange={setDest} options={locations} disabled={submitting} />
            <div className="cw-field">
              <label>Tuyến vận chuyển</label>
              <input readOnly value={route || "Tự xác định từ điểm đi → điểm đến"} />
            </div>
            <div className="cw-field">
              <label htmlFor="etdAt">ETD dự kiến</label>
              <input id="etdAt" name="etdAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label htmlFor="etaAt">ETA dự kiến</label>
              <input id="etaAt" name="etaAt" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label htmlFor="carrierName">Hãng vận chuyển</label>
              <input id="carrierName" name="carrierName" disabled={submitting} placeholder="Chọn hãng vận chuyển" />
            </div>
            <div className="cw-field s12">
              <label>Chặng (Transport Leg)</label>
              <input readOnly value="Thêm Chặng sau khi lưu — trên hồ sơ Shipment" />
              <p className="cw-hint">Có thể bổ sung/chỉnh sửa cấu trúc Chặng sau khi tạo Shipment. Không điều phối TMS tại đây.</p>
            </div>
            <div className="cw-field s12">
              <label>Chuyến (Transport Movement)</label>
              <input readOnly value="Tìm hoặc liên kết Chuyến sau khi lưu" />
              <p className="cw-hint">Ví dụ Air: số chuyến + ngày. Liên kết trên hồ sơ Chặng/Chuyến.</p>
            </div>
          </div>
        </CreateSection>

        <CreateSection title="3. Bill liên kết" hint="Chọn các Bill thuộc Shipment. Một Bill có thể thuộc nhiều Shipment trong hành trình." badge={<span className="create-tag">Bill ↔ Shipment: N:N</span>}>
          <div className="create-grid">
            <RefTagPicker
              entityType="bill"
              label="Tìm Bill"
              placeholder="Tìm theo số Bill, khách hàng, tuyến, reference..."
              selected={bills}
              onChange={setBills}
              initialHits={billHits}
            />
          </div>
          {bills.length > 0 ? (
            <div className="table-wrap" style={{ marginTop: 12 }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Số Bill</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {bills.map((b) => (
                    <tr key={b.id}>
                      <td>{b.code}</td>
                      <td>
                        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setBills(bills.filter((x) => x.id !== b.id))}>
                          ×
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
          <p className="cw-hint" style={{ marginTop: 8 }}>
            Việc liên kết Bill không làm thay đổi quyền sở hữu dữ liệu vận hành từ hệ thống nguồn và không tự tạo chi phí/doanh thu.
          </p>
        </CreateSection>

        <CreateSection title="4. Hàng hóa & đo lường" hint="Tổng hợp từ Bill liên kết hoặc nhập theo nguồn dữ liệu được phép; phục vụ Rating Context và phân bổ">
          <CreateCargoFields disabled={submitting} />
        </CreateSection>

        <CreateSection title="5. Ngữ cảnh chi phí & tính giá" hint="Thông tin tham chiếu cho Rating Engine và Cost Allocation; không tạo giao dịch tài chính tại màn hình này">
          <div className="create-grid">
            <div className="cw-field s6">
              <label htmlFor="buyRateCardId">Bảng giá mua tham chiếu</label>
              <select id="buyRateCardId" name="buyRateCardId" disabled={submitting} defaultValue="">
                <option value="">Chọn Rate Card BUY nếu có</option>
                {rateCards.map((r) => <option key={r.value} value={r.value}>{r.label}</option>)}
              </select>
            </div>
            <div className="cw-field s6">
              <label htmlFor="vendorPartyId">Nhà cung cấp / Hãng</label>
              <select id="vendorPartyId" name="vendorPartyId" disabled={submitting} defaultValue="">
                <option value="">Chọn nhà cung cấp</option>
                {vendors.map((v) => <option key={v.value} value={v.value}>{v.label}</option>)}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="preferredCurrency">Tiền tệ</label>
              <select id="preferredCurrency" name="preferredCurrency" disabled={submitting} defaultValue="VND">
                {(currencies.length ? currencies : [{ value: "VND", label: "VND" }]).map((c) => (
                  <option key={c.value} value={c.value}>{c.label}</option>
                ))}
              </select>
            </div>
            <div className="cw-field">
              <label htmlFor="rateDate">Ngày áp dụng giá</label>
              <input id="rateDate" name="rateDate" type="date" disabled={submitting} />
            </div>
            <div className="cw-field">
              <label>Rating Context</label>
              <input readOnly defaultValue="Tạo snapshot khi thực hiện tính giá" />
            </div>
            <div className="cw-field s12">
              <label htmlFor="ratingNote">Ghi chú tính giá / phân bổ</label>
              <textarea id="ratingNote" name="ratingNote" disabled={submitting} placeholder="Thông tin bổ sung cho Rating/Cost Allocation..." />
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
