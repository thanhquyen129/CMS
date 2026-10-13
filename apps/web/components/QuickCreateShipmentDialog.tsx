"use client";

import { useRef, useState } from "react";
import { LocationField } from "@/components/LocationField";
import { generateBusinessNo, manualSource, type CatalogOption } from "@/lib/create-workspace";

type Props = {
  open: boolean;
  modes: CatalogOption[];
  locations: CatalogOption[];
  onClose: () => void;
  onCreated: (shipment: { id: string; shipmentNo: string }) => void;
};

/** Same upsert, source and validation as Tạo Shipment — identity plus required route fields. */
export function QuickCreateShipmentDialog({ open, modes, locations, onClose, onCreated }: Props) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [shipmentNo, setShipmentNo] = useState("");
  const [mode, setMode] = useState("air");
  const [origin, setOrigin] = useState("");
  const [dest, setDest] = useState("");
  const identity = useRef<string | null>(null);
  const inFlight = useRef(false);

  if (!open) return null;

  function close() {
    if (inFlight.current) return;
    setError(null);
    onClose();
  }

  async function save() {
    if (inFlight.current) return;
    setError(null);
    const number = shipmentNo.trim();
    if (!number) {
      setError("Nhập mã Shipment.");
      return;
    }
    if (!origin.trim() || !dest.trim()) {
      setError("Chọn điểm đi và điểm đến.");
      return;
    }
    identity.current ??= number;
    inFlight.current = true;
    setBusy(true);
    try {
      const res = await fetch("/bff/shipments", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          shipmentNo: number,
          sourceSystem: manualSource(),
          externalId: identity.current,
          operationalStatus: "active",
          isActive: true,
          applyContext: true,
          transportMode: mode || "air",
          originCode: origin.trim(),
          destinationCode: dest.trim(),
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (res.status === 403) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Bạn không có quyền tạo Shipment.");
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được Shipment. Chặng chưa được tạo.");
        return;
      }
      const created = (await res.json()) as { id?: string };
      if (!created.id) {
        setError("Không nhận được mã Shipment. Thử lại — hệ thống không tạo thêm bản ghi mới.");
        return;
      }
      identity.current = null;
      onCreated({ id: created.id, shipmentNo: number });
      setShipmentNo("");
      setOrigin("");
      setDest("");
      setError(null);
    } catch {
      setError("Không kết nối được máy chủ. Shipment chưa được tạo. Thử lại sẽ dùng cùng mã.");
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={close}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="quick-shipment-title"
        style={{ maxWidth: 560 }}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <h2 id="quick-shipment-title">Tạo nhanh Shipment</h2>
        <p>Mã lô hàng, phương thức và hành trình — cùng điều kiện với màn tạo Shipment đầy đủ.</p>
        {error ? (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        ) : null}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void save();
          }}
        >
          <div className="form-grid">
            <div className="field">
              <label htmlFor="quickShipmentNo">
                Mã Shipment <span className="req">*</span>
              </label>
              <input
                id="quickShipmentNo"
                value={shipmentNo}
                maxLength={64}
                disabled={busy}
                onChange={(e) => setShipmentNo(e.target.value)}
              />
              <button
                className="btn btn-ghost btn-sm"
                type="button"
                disabled={busy}
                onClick={() => setShipmentNo(generateBusinessNo("SHP"))}
              >
                Tự động tạo mã
              </button>
            </div>
            <div className="field">
              <label htmlFor="quickTransportMode">
                Phương thức <span className="req">*</span>
              </label>
              <select
                id="quickTransportMode"
                value={mode}
                disabled={busy}
                onChange={(e) => setMode(e.target.value)}
              >
                {modes.map((m) => (
                  <option key={m.value} value={m.value}>
                    {m.label}
                  </option>
                ))}
              </select>
            </div>
            <LocationField
              id="quickOrigin"
              name="originCode"
              label="Điểm đi"
              required
              value={origin}
              options={locations}
              disabled={busy}
              onChange={setOrigin}
            />
            <LocationField
              id="quickDest"
              name="destinationCode"
              label="Điểm đến"
              required
              value={dest}
              options={locations}
              disabled={busy}
              onChange={setDest}
            />
          </div>
          <div className="dialog-actions">
            <button className="btn btn-ghost" type="button" disabled={busy} onClick={close}>
              Hủy
            </button>
            <button className="btn" type="submit" disabled={busy}>
              {busy ? "Đang lưu…" : "Lưu Shipment"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
