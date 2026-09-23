"use client";

import type { FormEvent } from "react";
import { useMemo, useState, useTransition } from "react";
import { useRouter } from "next/navigation";

type Col = { key: string; name: string; code: string };
type Row = { key: string; min: string; max: string };

type Props = {
  versionId?: string;
  currencyCode?: string;
  transportMode?: string | null;
};

const airRows = (): Row[] =>
  [
    ["0", "6"],
    ["6", "11"],
    ["11", "21"],
    ["21", "101"],
    ["101", "301"],
    ["301", "501"],
    ["501", ""],
  ].map(([min, max]) => ({ key: uid(), min, max }));

const airCols = (): Col[] =>
  [
    ["Hàng thường", "GENERAL"],
    ["Thực phẩm khô", "DRY_FOOD"],
    ["Mỹ phẩm", "COSMETICS"],
    ["Chuyển nhanh", "EXPRESS"],
  ].map(([name, code]) => ({ key: uid(), name, code }));

const seaRows = (): Row[] =>
  [
    ["0", "4"],
    ["4", "7"],
    ["7", "10.001"],
    ["10.001", ""],
  ].map(([min, max]) => ({ key: uid(), min, max }));

const seaCols = (): Col[] =>
  [
    ["Hàng thường", "GENERAL"],
    ["Mỹ phẩm, thực phẩm", "COSMETICS_FOOD"],
  ].map(([name, code]) => ({ key: uid(), name, code }));

function uid(): string {
  return Math.random().toString(36).slice(2, 10);
}

function parseNum(raw: string): number | null {
  const text = raw.trim().replace(/\s/g, "").replace(",", ".");
  if (!text) return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : null;
}

function preview(row: Row, next: Row | undefined): string {
  const min = parseNum(row.min);
  const max = parseNum(row.max);
  if (min === null) return "—";
  if (next) {
    const nextMin = parseNum(next.min);
    if (nextMin !== null && (max === null || nextMin - max <= 1)) {
      const upper = Math.floor(nextMin - 0.001 + 0.0001);
      return min === 0 ? `< ${Math.round(nextMin)}` : `${trim(min)} – ${upper}`;
    }
  }
  if (max === null) return `> ${Math.floor(min + 0.0001)}`;
  if (min === 0) return `< ${Math.round(max)}`;
  return `${trim(min)} – ${Math.floor(max + 0.0001)}`;
}

function trim(value: number): string {
  return Number.isInteger(value) ? String(value) : String(value);
}

export function ComposeTariffForm({ versionId, currencyCode, transportMode }: Props) {
  const router = useRouter();
  const filling = Boolean(versionId);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [mode, setMode] = useState(transportMode?.toLowerCase() || "air");
  const [columns, setColumns] = useState<Col[]>(() => (mode === "sea" ? seaCols() : airCols()));
  const [rows, setRows] = useState<Row[]>(() => (mode === "sea" ? seaRows() : airRows()));
  const [cells, setCells] = useState<Record<string, Record<string, string>>>({});
  const [minQty, setMinQty] = useState(mode === "sea" ? "1" : "");
  const [deliveryOn, setDeliveryOn] = useState(false);
  const [deliveryUnder, setDeliveryUnder] = useState("2");
  const [deliveryAmount, setDeliveryAmount] = useState("50000");
  const [remoteOn, setRemoteOn] = useState(false);
  const [remoteAmount, setRemoteAmount] = useState("85000");
  const [remoteCurrency, setRemoteCurrency] = useState("VND");
  const [remoteDestinations, setRemoteDestinations] = useState("SBH, SWK");

  const unit = mode === "sea" ? "CBM" : "kg";
  const priced = useMemo(
    () => Object.values(cells).some((row) => Object.values(row).some((v) => v.trim() !== "")),
    [cells]
  );

  function applyPreset(nextMode: "air" | "sea") {
    if (priced && !window.confirm("Đổi khung sẽ xóa đơn giá đã nhập. Tiếp tục?")) {
      return;
    }
    setMode(nextMode);
    setColumns(nextMode === "sea" ? seaCols() : airCols());
    setRows(nextMode === "sea" ? seaRows() : airRows());
    setCells({});
    setMinQty(nextMode === "sea" ? "1" : "");
  }

  function setCell(rowKey: string, colKey: string, value: string) {
    setCells((prev) => ({
      ...prev,
      [rowKey]: { ...(prev[rowKey] ?? {}), [colKey]: value },
    }));
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    const fd = new FormData(e.currentTarget);

    const parsedRows = rows.map((row) => {
      const min = parseNum(row.min);
      const max = row.max.trim() ? parseNum(row.max) : null;
      return { row, min, max };
    });
    if (parsedRows.some((r) => r.min === null || (r.row.max.trim() && r.max === null))) {
      setError("Mức từ / đến của khung phải là số.");
      return;
    }

    const badPrice = parsedRows.some((r) =>
      columns.some((col) => {
        const raw = cells[r.row.key]?.[col.key] ?? "";
        return raw.trim() !== "" && parseNum(raw) === null;
      })
    );
    if (badPrice) {
      setError("Đơn giá không hợp lệ.");
      return;
    }
    if (columns.some((c) => !c.code.trim() || !c.name.trim())) {
      setError("Mỗi loại hàng cần tên và mã.");
      return;
    }
    if (deliveryOn && (parseNum(deliveryUnder) === null || parseNum(deliveryAmount) === null)) {
      setError("Phí giao hàng cần mức áp dụng và số tiền.");
      return;
    }
    if (remoteOn && parseNum(remoteAmount) === null) {
      setError("Phụ phí vùng cần đơn giá.");
      return;
    }

    const body: Record<string, unknown> = {
      rateVersionId: versionId ?? null,
      transportMode: mode,
      minimumQuantity: parseNum(minQty),
      columns: columns.map((c) => ({ code: c.code.trim(), name: c.name.trim() })),
      bands: parsedRows.map(({ row, min, max }) => ({
        minQuantity: min,
        maxQuantity: max,
        prices: columns.map((col) => {
          const raw = cells[row.key]?.[col.key] ?? "";
          return raw.trim() ? parseNum(raw) : null;
        }),
      })),
      delivery: deliveryOn
        ? { underQuantity: parseNum(deliveryUnder), amount: parseNum(deliveryAmount) }
        : null,
      remote: remoteOn
        ? {
            amountPerKg: parseNum(remoteAmount),
            currencyCode: remoteCurrency.trim().toUpperCase(),
            destinations: remoteDestinations.split(/[,;\s]+/).filter(Boolean),
          }
        : null,
    };

    if (!filling) {
      body.code = String(fd.get("code") ?? "").trim();
      body.name = String(fd.get("name") ?? "").trim();
      body.partyType = String(fd.get("partyType") ?? "vendor");
      body.currencyCode = String(fd.get("currencyCode") ?? currencyCode ?? "VND").trim().toUpperCase();
      body.description = String(fd.get("description") ?? "").trim() || null;
      body.routeCode = String(fd.get("routeCode") ?? "").trim() || null;
      body.carrierName = String(fd.get("carrierName") ?? "").trim() || null;
      const from = String(fd.get("effectiveFrom") ?? "").trim();
      body.effectiveFrom = from ? new Date(from).toISOString() : null;
      body.note = String(fd.get("note") ?? "").trim() || null;
      if (!body.code || !body.name) {
        setError("Nhập mã và tên bảng giá.");
        return;
      }
    }

    setSubmitting(true);
    try {
      const res = await fetch("/bff/rate-cards/compose", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được bảng giá.");
        return;
      }
      const created = (await res.json()) as { rateCardId?: string };
      const next = created.rateCardId ? `/rate-cards/${created.rateCardId}` : "/rate-cards";
      startTransition(() => router.push(next));
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Nhập đơn giá theo khung {unit} và loại hàng. Ô trống nghĩa là khung đó không có giá.
        Mức Đến trùng mức Từ của khung sau thì số lẻ thuộc khung dưới (10,5 kg dùng giá 6–10).
        Lưu nháp, rồi phát hành để tính trên Bill.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {!filling ? (
        <div className="form-grid">
          <div className="field">
            <label htmlFor="code">Mã</label>
            <input id="code" name="code" required maxLength={64} autoComplete="off" />
          </div>
          <div className="field">
            <label htmlFor="name">Tên</label>
            <input id="name" name="name" required maxLength={256} autoComplete="off" />
          </div>
          <div className="field">
            <label htmlFor="partyType">Loại giá</label>
            <select id="partyType" name="partyType" defaultValue="vendor">
              <option value="vendor">Giá mua</option>
              <option value="customer">Giá bán</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="currencyCode">Tiền tệ</label>
            <input id="currencyCode" name="currencyCode" defaultValue={currencyCode || "VND"} maxLength={3} required />
          </div>
          <div className="field">
            <label htmlFor="carrierName">Hãng/NCC</label>
            <input id="carrierName" name="carrierName" maxLength={128} />
          </div>
          <div className="field">
            <label htmlFor="routeCode">Tuyến</label>
            <input id="routeCode" name="routeCode" maxLength={64} placeholder="VN-MY" />
          </div>
          <div className="field">
            <label htmlFor="effectiveFrom">Hiệu lực từ</label>
            <input id="effectiveFrom" name="effectiveFrom" type="date" />
          </div>
          <div className="field field-span">
            <label htmlFor="description">Mô tả</label>
            <input id="description" name="description" maxLength={1024} />
          </div>
          <div className="field field-span">
            <label htmlFor="note">Ghi chú phiên bản</label>
            <input id="note" name="note" maxLength={1024} />
          </div>
        </div>
      ) : null}

      <div className="cta-row">
        <button className="btn btn-ghost" type="button" onClick={() => applyPreset("air")}>
          Khung Air
        </button>
        <button className="btn btn-ghost" type="button" onClick={() => applyPreset("sea")}>
          Khung Sea
        </button>
      </div>

      <div className="table-wrap">
        <table className="data-table">
          <caption className="muted small" style={{ captionSide: "top", textAlign: "left", padding: "0.5rem 0" }}>
            Đơn giá / {unit}
          </caption>
          <thead>
            <tr>
              <th scope="col">Khung ({unit})</th>
              {columns.map((col) => (
                <th key={col.key} scope="col">
                  <input
                    aria-label={`Tên ${col.code}`}
                    value={col.name}
                    onChange={(ev) =>
                      setColumns((prev) => prev.map((c) => (c.key === col.key ? { ...c, name: ev.target.value } : c)))
                    }
                  />
                  <input
                    aria-label={`Mã ${col.name}`}
                    value={col.code}
                    onChange={(ev) =>
                      setColumns((prev) =>
                        prev.map((c) => (c.key === col.key ? { ...c, code: ev.target.value.toUpperCase() } : c))
                      )
                    }
                  />
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={row.key}>
                <th scope="row">
                  <span className="muted small">{preview(row, rows[index + 1])}</span>
                  <div className="cta-row" style={{ marginTop: "0.25rem" }}>
                    <input
                      aria-label="Từ"
                      value={row.min}
                      inputMode="decimal"
                      style={{ width: "4.5rem" }}
                      onChange={(ev) =>
                        setRows((prev) => prev.map((r) => (r.key === row.key ? { ...r, min: ev.target.value } : r)))
                      }
                    />
                    <input
                      aria-label="Đến"
                      value={row.max}
                      inputMode="decimal"
                      placeholder="mở"
                      style={{ width: "4.5rem" }}
                      onChange={(ev) =>
                        setRows((prev) => prev.map((r) => (r.key === row.key ? { ...r, max: ev.target.value } : r)))
                      }
                    />
                    <button
                      className="btn btn-ghost btn-sm"
                      type="button"
                      onClick={() => setRows((prev) => prev.filter((r) => r.key !== row.key))}
                      disabled={rows.length <= 1}
                    >
                      Xóa
                    </button>
                  </div>
                </th>
                {columns.map((col) => (
                  <td key={col.key}>
                    <input
                      aria-label={`${col.name} ${preview(row, rows[index + 1])}`}
                      inputMode="decimal"
                      value={cells[row.key]?.[col.key] ?? ""}
                      onChange={(ev) => setCell(row.key, col.key, ev.target.value)}
                      style={{ width: "7rem" }}
                    />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="cta-row">
        <button
          className="btn btn-ghost"
          type="button"
          onClick={() => setRows((prev) => [...prev, { key: uid(), min: "", max: "" }])}
        >
          Thêm khung
        </button>
        <button
          className="btn btn-ghost"
          type="button"
          onClick={() =>
            setColumns((prev) => [...prev, { key: uid(), name: "Loại hàng", code: `COL${prev.length + 1}` }])
          }
        >
          Thêm loại hàng
        </button>
        {columns.length > 1 ? (
          <button className="btn btn-ghost" type="button" onClick={() => setColumns((prev) => prev.slice(0, -1))}>
            Bớt cột cuối
          </button>
        ) : null}
      </div>

      <div className="form-grid">
        <div className="field">
          <label htmlFor="minQty">Số lượng tối thiểu</label>
          <input id="minQty" value={minQty} inputMode="decimal" onChange={(ev) => setMinQty(ev.target.value)} placeholder="Sea: 1 CBM" />
        </div>
        <div className="field checkbox-field">
          <label>
            <input type="checkbox" checked={deliveryOn} onChange={(ev) => setDeliveryOn(ev.target.checked)} /> Phí giao theo khối
          </label>
        </div>
        {deliveryOn ? (
          <>
            <div className="field">
              <label htmlFor="deliveryUnder">Áp khi dưới</label>
              <input id="deliveryUnder" value={deliveryUnder} inputMode="decimal" onChange={(ev) => setDeliveryUnder(ev.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="deliveryAmount">Số tiền / đơn</label>
              <input id="deliveryAmount" value={deliveryAmount} inputMode="decimal" onChange={(ev) => setDeliveryAmount(ev.target.value)} />
            </div>
          </>
        ) : null}
        <div className="field checkbox-field">
          <label>
            <input type="checkbox" checked={remoteOn} onChange={(ev) => setRemoteOn(ev.target.checked)} /> Phụ phí vùng (đ/kg)
          </label>
        </div>
        {remoteOn ? (
          <>
            <div className="field">
              <label htmlFor="remoteDest">Mã điểm đến</label>
              <input id="remoteDest" value={remoteDestinations} onChange={(ev) => setRemoteDestinations(ev.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="remoteAmount">Đơn giá / kg</label>
              <input id="remoteAmount" value={remoteAmount} inputMode="decimal" onChange={(ev) => setRemoteAmount(ev.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="remoteCurrency">Tiền tệ phụ phí</label>
              <input id="remoteCurrency" value={remoteCurrency} maxLength={3} onChange={(ev) => setRemoteCurrency(ev.target.value.toUpperCase())} />
            </div>
          </>
        ) : null}
      </div>

      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang lưu…" : "Lưu nháp"}
        </button>
      </div>
    </form>
  );
}
