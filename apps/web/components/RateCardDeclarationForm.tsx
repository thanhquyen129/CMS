"use client";

import type { FormEvent } from "react";
import { useMemo, useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PartyTypeahead } from "@/components/PartyTypeahead";
import type { RouteItem } from "@/lib/reference-masters-shared";
import {
  amountFieldLabel,
  basicCalcMethods,
  isWeightBreakMethod,
  routeOptionLabel,
  type PricingRule,
} from "@/lib/rate-cards";

type CurrencyOption = { code: string; name: string; isActive: boolean };
type Col = { key: string; name: string; code: string };
type Band = { key: string; min: string; max: string };
type RuleRow = {
  key: string;
  code: string;
  name: string;
  calcMethod: string;
  unitAmount: string;
  currencyCode: string;
  serviceTypeCode: string;
  commodityCode: string;
  transportMode: string;
  partyTypeCode: string;
  routeCode: string;
  applicability: string;
  minAmount: string;
  maxAmount: string;
};

type Props = {
  defaultCurrency: string;
  currencies: CurrencyOption[];
  routes: RouteItem[];
  routeError?: string | null;
  currencyError?: string | null;
  cardId?: string;
  code?: string;
  name?: string;
  partyType?: string;
  currencyCode?: string;
  description?: string | null;
  carrierName?: string | null;
  routeId?: string | null;
  routeCode?: string | null;
  supplierPartyId?: string | null;
  customerPartyId?: string | null;
  customerGroupCode?: string | null;
  declarationMode?: string | null;
  modeLocked?: boolean;
  versionId?: string;
  vatRate?: number | null;
  effectiveFrom?: string | null;
  note?: string | null;
  transportMode?: string | null;
  rules?: PricingRule[];
};

const modeOptions = [
  { value: "air", label: "Air" },
  { value: "sea", label: "Sea" },
  { value: "road", label: "Road" },
  { value: "rail", label: "Rail" },
  { value: "water", label: "Water" },
];

function uid(): string {
  return Math.random().toString(36).slice(2, 10);
}

function parseNum(raw: string): number | null {
  const text = raw.trim().replace(/\s/g, "").replace(",", ".");
  if (!text) return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : null;
}

function dateInput(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso.slice(0, 10);
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function emptyRule(currency: string): RuleRow {
  return {
    key: uid(),
    code: "",
    name: "",
    calcMethod: "fixed",
    unitAmount: "",
    currencyCode: currency,
    serviceTypeCode: "",
    commodityCode: "",
    transportMode: "",
    partyTypeCode: "",
    routeCode: "",
    applicability: "",
    minAmount: "",
    maxAmount: "",
  };
}

function rulesFromSaved(rules: PricingRule[], currency: string): RuleRow[] {
  const basic = rules.filter((r) => !isWeightBreakMethod(r.calcMethod));
  if (basic.length === 0) return [emptyRule(currency)];
  return basic.map((r) => ({
    key: r.id,
    code: r.code,
    name: r.name,
    calcMethod: r.calcMethod,
    unitAmount: String(r.unitAmount),
    currencyCode: r.currencyCode || currency,
    serviceTypeCode: r.serviceTypeCode ?? "",
    commodityCode: r.commodityCode ?? "",
    transportMode: r.transportMode ?? "",
    partyTypeCode: r.partyTypeCode ?? "",
    routeCode: r.routeCode ?? "",
    applicability: r.applicability ?? "",
    minAmount: r.minAmount == null ? "" : String(r.minAmount),
    maxAmount: r.maxAmount == null ? "" : String(r.maxAmount),
  }));
}

const airBands = (): Band[] =>
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

const seaBands = (): Band[] =>
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

function preview(row: Band, next: Band | undefined): string {
  const min = parseNum(row.min);
  const max = parseNum(row.max);
  if (min === null) return "—";
  if (next) {
    const nextMin = parseNum(next.min);
    if (nextMin !== null && (max === null || nextMin - max <= 1)) {
      const upper = Math.floor(nextMin - 0.001 + 0.0001);
      return min === 0 ? `< ${Math.round(nextMin)}` : `${trimNum(min)} – ${upper}`;
    }
  }
  if (max === null) return `> ${Math.floor(min + 0.0001)}`;
  if (min === 0) return `< ${Math.round(max)}`;
  return `${trimNum(min)} – ${Math.floor(max + 0.0001)}`;
}

function trimNum(value: number): string {
  return Number.isInteger(value) ? String(value) : String(value);
}

function matrixFromRules(rules: PricingRule[]): { columns: Col[]; rows: Band[]; cells: Record<string, Record<string, string>> } | null {
  const matrix = rules.filter((r) => isWeightBreakMethod(r.calcMethod) && (r.breaks?.length ?? 0) > 0);
  if (matrix.length === 0) return null;
  const columns = matrix.map((r) => ({ key: r.id, name: r.name, code: r.commodityCode || r.code }));
  const bandKeys: string[] = [];
  const bandByKey = new Map<string, Band>();
  for (const rule of matrix) {
    for (const band of rule.breaks ?? []) {
      const key = `${band.minQuantity}|${band.maxQuantity ?? ""}`;
      if (!bandByKey.has(key)) {
        bandKeys.push(key);
        bandByKey.set(key, {
          key,
          min: String(band.minQuantity),
          max: band.maxQuantity == null ? "" : String(band.maxQuantity),
        });
      }
    }
  }
  const rows = bandKeys.map((key) => bandByKey.get(key)!);
  const cells: Record<string, Record<string, string>> = {};
  for (const rule of matrix) {
    for (const band of rule.breaks ?? []) {
      const key = `${band.minQuantity}|${band.maxQuantity ?? ""}`;
      cells[key] = { ...(cells[key] ?? {}), [rule.id]: String(band.unitAmount) };
    }
  }
  return { columns, rows, cells };
}

export function RateCardDeclarationForm(props: Props) {
  const router = useRouter();
  const editing = Boolean(props.cardId);
  const initialMode = props.declarationMode === "advanced" ? "advanced" : "basic";
  const seeded = matrixFromRules(props.rules ?? []);
  const startPreset = props.transportMode?.toLowerCase() === "sea" ? "sea" : "air";
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [partyType, setPartyType] = useState(props.partyType === "customer" ? "customer" : "vendor");
  const [partnerEpoch, setPartnerEpoch] = useState(0);
  const [partnerNotice, setPartnerNotice] = useState<string | null>(null);
  const [currency, setCurrency] = useState((props.currencyCode || props.defaultCurrency || "VND").toUpperCase());
  const [routeQuery, setRouteQuery] = useState("");
  const [routeId, setRouteId] = useState(props.routeId ?? "");
  const [mode, setMode] = useState<"basic" | "advanced">(initialMode);
  const [pendingMode, setPendingMode] = useState<"basic" | "advanced" | null>(null);
  const [rules, setRules] = useState<RuleRow[]>(() => rulesFromSaved(props.rules ?? [], currency));
  const [preset, setPreset] = useState<"air" | "sea">(startPreset);
  const [columns, setColumns] = useState<Col[]>(() => seeded?.columns ?? (startPreset === "sea" ? seaCols() : airCols()));
  const [rows, setRows] = useState<Band[]>(() => seeded?.rows ?? (startPreset === "sea" ? seaBands() : airBands()));
  const [cells, setCells] = useState<Record<string, Record<string, string>>>(() => seeded?.cells ?? {});
  const [minQty, setMinQty] = useState(startPreset === "sea" ? "1" : "");
  const [deliveryOn, setDeliveryOn] = useState(false);
  const [deliveryUnder, setDeliveryUnder] = useState("2");
  const [deliveryAmount, setDeliveryAmount] = useState("50000");
  const [remoteOn, setRemoteOn] = useState(false);
  const [remoteAmount, setRemoteAmount] = useState("85000");
  const [remoteCurrency, setRemoteCurrency] = useState(currency);
  const [remoteDestinations, setRemoteDestinations] = useState("");

  const sell = partyType === "customer";
  const activeCurrencies = props.currencies.filter((c) => c.isActive);
  const currencyChoices = useMemo(() => {
    const codes = activeCurrencies.map((c) => c.code.toUpperCase());
    if (currency && !codes.includes(currency)) codes.unshift(currency);
    return codes;
  }, [activeCurrencies, currency]);
  const activeRoutes = props.routes.filter((r) => r.isActive);
  const visibleRoutes = props.routes.filter((r) => r.isActive || r.id === routeId);
  const filteredRoutes = visibleRoutes.filter((r) => {
    const q = routeQuery.trim().toLowerCase();
    if (!q) return true;
    return [r.code, r.name, r.originCode, r.destinationCode].some((part) => part?.toLowerCase().includes(q));
  });
  const selectedRoute = props.routes.find((r) => r.id === routeId) ?? null;
  const priced = useMemo(
    () => Object.values(cells).some((row) => Object.values(row).some((v) => v.trim() !== "")),
    [cells]
  );
  const basicDirty = rules.some((r) => r.code.trim() || r.name.trim() || r.unitAmount.trim());
  const advancedDirty = priced || deliveryOn || remoteOn || Boolean(seeded && mode === "advanced");

  function askMode(next: "basic" | "advanced") {
    if (props.modeLocked || next === mode) return;
    const dirty = mode === "basic" ? basicDirty : advancedDirty;
    if (!dirty) {
      setMode(next);
      return;
    }
    setPendingMode(next);
  }

  function acceptMode() {
    if (!pendingMode) return;
    if (mode === "basic") setRules([emptyRule(currency)]);
    if (mode === "advanced") {
      setCells({});
      setDeliveryOn(false);
      setRemoteOn(false);
    }
    setMode(pendingMode);
    setPendingMode(null);
  }

  function applyPreset(next: "air" | "sea") {
    if (priced && !window.confirm("Đổi khung sẽ xóa đơn giá đã nhập trên ma trận. Tiếp tục?")) return;
    setPreset(next);
    setColumns(next === "sea" ? seaCols() : airCols());
    setRows(next === "sea" ? seaBands() : airBands());
    setCells({});
    setMinQty(next === "sea" ? "1" : "");
  }

  function setCell(rowKey: string, colKey: string, value: string) {
    setCells((prev) => ({ ...prev, [rowKey]: { ...(prev[rowKey] ?? {}), [colKey]: value } }));
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    const fd = new FormData(e.currentTarget);
    if (activeRoutes.length === 0 && !routeId) {
      setError("Danh mục tuyến trống hoặc không có tuyến đang hoạt động. Không tạo tuyến giả.");
      return;
    }
    if (activeRoutes.length > 0 && !routeId && !props.routeCode) {
      setError("Chọn tuyến từ danh mục.");
      return;
    }
    if (currencyChoices.length === 0) {
      setError("Danh mục tiền tệ trống. Khai báo tiền tệ trước khi lưu.");
      return;
    }

    const body: Record<string, unknown> = {
      code: props.code ?? String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      partyType,
      currencyCode: currency,
      description: String(fd.get("description") ?? "").trim() || null,
      carrierName: String(fd.get("carrierName") ?? "").trim() || null,
      routeId: routeId || null,
      declarationMode: mode,
      confirmModeChange: mode !== initialMode,
      supplierPartyId: sell ? null : String(fd.get("supplierPartyId") ?? "").trim() || null,
      customerPartyId: sell ? String(fd.get("customerPartyId") ?? "").trim() || null : null,
      customerGroupCode: sell ? String(fd.get("customerGroupCode") ?? "").trim() || null : null,
      note: String(fd.get("note") ?? "").trim() || null,
      rateVersionId: props.versionId ?? null,
      rules: [],
      columns: [],
      bands: [],
    };
    const from = String(fd.get("effectiveFrom") ?? "").trim();
    body.effectiveFrom = from ? new Date(from).toISOString() : null;
    const vatRaw = String(fd.get("vatRate") ?? "").trim().replace(",", ".");
    body.vatRate = vatRaw ? Number(vatRaw) : null;
    if (!body.name || !body.code) {
      setError("Nhập mã và tên bảng giá.");
      return;
    }

    if (mode === "basic") {
      const filled = rules.filter((r) => r.code.trim() || r.name.trim() || r.unitAmount.trim());
      for (const rule of filled) {
        const amount = parseNum(rule.unitAmount);
        if (!rule.code.trim() || !rule.name.trim() || amount === null || amount < 0) {
          setError("Mỗi quy tắc cần mã, tên và số tiền / đơn giá không âm.");
          return;
        }
        if (rule.calcMethod === "min_max_clamp" && !rule.minAmount.trim() && !rule.maxAmount.trim()) {
          setError("Cách tính kẹp min/max cần số tiền tối thiểu hoặc tối đa.");
          return;
        }
      }
      body.rules = filled.map((rule) => ({
        code: rule.code.trim(),
        name: rule.name.trim(),
        calcMethod: rule.calcMethod,
        unitAmount: parseNum(rule.unitAmount),
        currencyCode: rule.currencyCode,
        serviceTypeCode: rule.serviceTypeCode.trim() || null,
        commodityCode: rule.commodityCode.trim() || null,
        transportMode: rule.transportMode.trim() || null,
        partyTypeCode: rule.partyTypeCode.trim() || null,
        routeCode: rule.routeCode.trim() || null,
        applicability: rule.applicability.trim() || null,
        minAmount: parseNum(rule.minAmount),
        maxAmount: parseNum(rule.maxAmount),
      }));
      body.transportMode = props.transportMode ?? null;
    } else {
      const parsedRows = rows.map((row) => ({ row, min: parseNum(row.min), max: row.max.trim() ? parseNum(row.max) : null }));
      if (parsedRows.some((r) => r.min === null || (r.row.max.trim() && r.max === null))) {
        setError("Mức từ / đến của khung phải là số.");
        return;
      }
      if (columns.some((c) => !c.code.trim() || !c.name.trim())) {
        setError("Mỗi loại hàng cần tên và mã.");
        return;
      }
      const badPrice = parsedRows.some((r) =>
        columns.some((col) => {
          const raw = cells[r.row.key]?.[col.key] ?? "";
          const n = raw.trim() ? parseNum(raw) : null;
          return raw.trim() !== "" && (n === null || n < 0);
        })
      );
      if (badPrice) {
        setError("Đơn giá trên ma trận không hợp lệ.");
        return;
      }
      if (!editing && deliveryOn && (parseNum(deliveryUnder) === null || parseNum(deliveryAmount) === null || (parseNum(deliveryAmount) ?? 0) < 0)) {
        setError("Phí giao hàng cần mức áp dụng và số tiền không âm.");
        return;
      }
      if (!editing && remoteOn && (parseNum(remoteAmount) === null || (parseNum(remoteAmount) ?? 0) < 0)) {
        setError("Phụ phí vùng cần đơn giá không âm.");
        return;
      }
      body.transportMode = preset;
      body.minimumQuantity = parseNum(minQty);
      body.columns = columns.map((c) => ({ code: c.code.trim(), name: c.name.trim() }));
      body.bands = parsedRows.map(({ row, min, max }) => ({
        minQuantity: min,
        maxQuantity: max,
        prices: columns.map((col) => {
          const raw = cells[row.key]?.[col.key] ?? "";
          return raw.trim() ? parseNum(raw) : null;
        }),
      }));
      body.delivery = !editing && deliveryOn ? { underQuantity: parseNum(deliveryUnder), amount: parseNum(deliveryAmount) } : null;
      body.remote = !editing && remoteOn
        ? {
            amountPerKg: parseNum(remoteAmount),
            currencyCode: remoteCurrency.trim().toUpperCase(),
            destinations: remoteDestinations.split(/[,;\s]+/).filter(Boolean),
          }
        : null;
    }

    setSubmitting(true);
    try {
      const res = await fetch(editing ? `/bff/rate-cards/${props.cardId}` : "/bff/rate-cards", {
        method: editing ? "PUT" : "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được bảng giá.");
        return;
      }
      const created = (await res.json().catch(() => ({}))) as { id?: string; rateCardId?: string };
      const next = `/rate-cards/${created.rateCardId || created.id || props.cardId}`;
      startTransition(() => router.push(next));
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const unit = preset === "sea" ? "CBM" : "kg";

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Nhập thông tin chung và chọn một hình thức khai báo. Mỗi bảng giá chỉ dùng bảng giá cơ bản hoặc bảng giá nâng cao.
      </p>
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      {props.routeError ? <div className="alert alert-error" role="alert">{props.routeError}</div> : null}
      {props.currencyError ? <div className="alert alert-error" role="alert">{props.currencyError}</div> : null}

      <h2 className="section-title">1. Thông tin chung</h2>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="code">Mã</label>
          {editing ? (
            <input id="code" name="code" value={props.code ?? ""} readOnly />
          ) : (
            <input id="code" name="code" required maxLength={64} autoComplete="off" />
          )}
        </div>
        <div className="field">
          <label htmlFor="name">Tên</label>
          <input id="name" name="name" required maxLength={256} defaultValue={props.name ?? ""} />
        </div>
        <div className="field">
          <label htmlFor="partyType">Loại giá</label>
          <select
            id="partyType"
            name="partyType"
            value={partyType}
            onChange={(ev) => {
              const next = ev.target.value;
              if (next === partyType) return;
              const form = ev.currentTarget.form;
              const fd = new FormData(form ?? undefined);
              const chosen = String(fd.get("supplierPartyId") ?? fd.get("customerPartyId") ?? "").trim();
              const group = String(fd.get("customerGroupCode") ?? "").trim();
              if ((chosen || group) && !window.confirm("Đổi chiều giá sẽ bỏ đối tác đã chọn vì không đúng vai trò. Tiếp tục?")) return;
              setPartyType(next);
              setPartnerEpoch((n) => n + 1);
              if (chosen || group) setPartnerNotice("Đã bỏ đối tác không đúng vai trò cho chiều giá mới.");
            }}
          >
            <option value="vendor">Giá mua</option>
            <option value="customer">Giá bán</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="currencyCode">Tiền tệ</label>
          <select id="currencyCode" name="currencyCode" required value={currency} onChange={(ev) => setCurrency(ev.target.value)}>
            {currencyChoices.length === 0 ? <option value="">Danh mục tiền tệ trống</option> : null}
            {currencyChoices.map((code) => (
              <option key={code} value={code}>{code}</option>
            ))}
          </select>
          <p className="muted small">Đổi tiền tệ không quy đổi số tiền đã nhập.</p>
        </div>
        <div className="field">
          <label htmlFor="carrierName">Hãng vận chuyển</label>
          <input id="carrierName" name="carrierName" maxLength={128} defaultValue={props.carrierName ?? ""} placeholder="Chọn hãng vận chuyển nếu có" />
        </div>
        <div className="field">
          <label htmlFor="routeSearch">Tuyến</label>
          <input
            id="routeSearch"
            value={routeQuery}
            onChange={(ev) => setRouteQuery(ev.target.value)}
            placeholder="Tìm mã tuyến, điểm đi, điểm đến"
            autoComplete="off"
          />
          <select id="routeId" name="routeId" value={routeId} onChange={(ev) => setRouteId(ev.target.value)} required={activeRoutes.length > 0 && !props.routeCode}>
            <option value="">{activeRoutes.length === 0 ? "Không có tuyến đang hoạt động" : "Chọn tuyến"}</option>
            {filteredRoutes.map((route) => (
              <option key={route.id} value={route.id}>
                {routeOptionLabel(route)}{route.isActive ? "" : " — ngừng sử dụng"}
              </option>
            ))}
          </select>
          {selectedRoute && !selectedRoute.isActive ? (
            <p className="alert alert-warning" role="status">Tuyến đã lưu ngừng hiệu lực. Giữ nguyên hoặc chọn tuyến đang hoạt động.</p>
          ) : null}
          {!selectedRoute && props.routeCode ? (
            <p className="alert alert-warning" role="status">Tuyến đã lưu dạng chữ {props.routeCode}. Chọn lại từ danh mục khi sửa.</p>
          ) : null}
          {activeRoutes.length === 0 ? (
            <p className="alert alert-warning" role="status">Danh mục tuyến trống hoặc không có tuyến đang hoạt động. Không tạo tuyến giả.</p>
          ) : null}
        </div>
        <div className="field">
          {sell ? (
            <PartyTypeahead
              key={`sell-${partnerEpoch}`}
              name="customerPartyId"
              label="Khách hàng"
              roleCode="customer"
              defaultId={props.customerPartyId}
              hint="Chỉ bảng giá bán. Để trống nếu bảng giá chung."
              emptyHint={<><Link href="/admin/parties">Mở Đối tác</Link> để bổ sung vai trò Khách hàng.</>}
            />
          ) : (
            <PartyTypeahead
              key={`buy-${partnerEpoch}`}
              name="supplierPartyId"
              label="Nhà cung cấp"
              roleCode="vendor"
              defaultId={props.supplierPartyId}
              hint="Chỉ bảng giá mua. Để trống nếu bảng giá chung."
              emptyHint={<><Link href="/admin/parties">Mở Đối tác</Link> để bổ sung vai trò Nhà cung cấp.</>}
            />
          )}
          {partnerNotice ? <p className="alert alert-warning" role="status">{partnerNotice}</p> : null}
        </div>
        {sell ? (
          <div className="field">
            <label htmlFor="customerGroupCode">Nhóm khách hàng</label>
            <input key={`group-${partnerEpoch}`} id="customerGroupCode" name="customerGroupCode" maxLength={64} defaultValue={props.customerGroupCode ?? ""} placeholder="Không chọn cùng khách hàng" />
          </div>
        ) : null}
        <div className="field">
          <label htmlFor="vatRate">Thuế suất VAT (%)</label>
          <input id="vatRate" name="vatRate" inputMode="decimal" defaultValue={props.vatRate ?? ""} placeholder="Để trống nếu chưa khai báo" />
        </div>
        <div className="field">
          <label htmlFor="effectiveFrom">Hiệu lực từ</label>
          <input id="effectiveFrom" name="effectiveFrom" type="date" defaultValue={dateInput(props.effectiveFrom)} />
        </div>
        <div className="field field-span">
          <label htmlFor="description">Mô tả</label>
          <input id="description" name="description" maxLength={1024} defaultValue={props.description ?? ""} />
        </div>
        <div className="field field-span">
          <label htmlFor="note">Ghi chú phiên bản</label>
          <input id="note" name="note" maxLength={1024} defaultValue={props.note ?? ""} />
        </div>
      </div>

      <h2 className="section-title">2. Hình thức khai báo giá</h2>
      {props.modeLocked ? <p className="note">Hình thức đã khóa vì bảng giá đã có phiên bản phát hành. Bản nháp giữ cùng hình thức.</p> : null}
      <div className="form-grid">
        <label className="panel" style={{ margin: 0, borderColor: mode === "basic" ? "#1d4e89" : undefined }}>
          <input type="radio" name="declarationMode" checked={mode === "basic"} disabled={props.modeLocked} onChange={() => askMode("basic")} />
          <strong> Bảng giá cơ bản</strong>
          <div className="muted small">Khai báo giá theo các quy tắc tính giá</div>
        </label>
        <label className="panel" style={{ margin: 0, borderColor: mode === "advanced" ? "#1d4e89" : undefined }}>
          <input type="radio" name="declarationMode" checked={mode === "advanced"} disabled={props.modeLocked} onChange={() => askMode("advanced")} />
          <strong> Bảng giá nâng cao</strong>
          <div className="muted small">Khai báo giá theo khung trọng lượng và nhóm hàng</div>
        </label>
      </div>
      {pendingMode ? (
        <div className="alert alert-warning" role="dialog" aria-modal="true" aria-labelledby="mode-switch-title">
          <p id="mode-switch-title">
            Chuyển sang {pendingMode === "basic" ? "bảng giá cơ bản" : "bảng giá nâng cao"} sẽ gỡ dữ liệu đang nhập của hình thức hiện tại trên biểu mẫu. Bản đã lưu chỉ đổi sau khi bấm Lưu nháp.
          </p>
          <div className="cta-row">
            <button className="btn btn-ghost" type="button" onClick={() => setPendingMode(null)}>Hủy</button>
            <button className="btn" type="button" onClick={acceptMode}>Đồng ý</button>
          </div>
        </div>
      ) : null}

      {mode === "basic" ? (
        <>
          <h2 className="section-title">3. Bảng giá cơ bản</h2>
          <p className="note">Quy tắc áp cho bản nháp này. Cách tính lấy từ cấu hình tính giá hiện có.</p>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Mã quy tắc</th>
                  <th>Tên quy tắc</th>
                  <th>Cách tính</th>
                  <th>Số tiền / Đơn giá</th>
                  <th>Tiền tệ</th>
                  <th>Loại dịch vụ</th>
                  <th>Mã loại hàng</th>
                  <th>Mã phương thức</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((rule, index) => (
                  <tr key={rule.key}>
                    <td>{index + 1}</td>
                    <td><input aria-label={`Mã quy tắc ${index + 1}`} value={rule.code} maxLength={64} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, code: ev.target.value } : r))} /></td>
                    <td><input aria-label={`Tên quy tắc ${index + 1}`} value={rule.name} maxLength={256} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, name: ev.target.value } : r))} /></td>
                    <td>
                      <select aria-label={`Cách tính ${index + 1}`} value={rule.calcMethod} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, calcMethod: ev.target.value } : r))}>
                        {basicCalcMethods.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                      </select>
                      {rule.calcMethod === "unit_rate" ? (
                        <select aria-label={`Đơn vị áp dụng ${index + 1}`} value={rule.applicability} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, applicability: ev.target.value } : r))}>
                          <option value="">Theo số lượng cước</option>
                          <option value="per_gross_kg">Theo kg thực</option>
                        </select>
                      ) : null}
                    </td>
                    <td>
                      <input
                        aria-label={`${amountFieldLabel(rule.calcMethod, rule.applicability)} ${index + 1}`}
                        placeholder={amountFieldLabel(rule.calcMethod, rule.applicability)}
                        inputMode="decimal"
                        value={rule.unitAmount}
                        onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, unitAmount: ev.target.value } : r))}
                      />
                      <div className="muted small">{amountFieldLabel(rule.calcMethod, rule.applicability)}</div>
                      {rule.calcMethod === "min_max_clamp" ? (
                        <div>
                          <input aria-label="Tối thiểu" placeholder="Tối thiểu" inputMode="decimal" value={rule.minAmount} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, minAmount: ev.target.value } : r))} />
                          <input aria-label="Tối đa" placeholder="Tối đa" inputMode="decimal" value={rule.maxAmount} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, maxAmount: ev.target.value } : r))} />
                        </div>
                      ) : null}
                    </td>
                    <td>
                      <select aria-label={`Tiền tệ quy tắc ${index + 1}`} value={rule.currencyCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, currencyCode: ev.target.value } : r))}>
                        {currencyChoices.map((code) => <option key={code} value={code}>{code}</option>)}
                      </select>
                    </td>
                    <td><input aria-label={`Loại dịch vụ ${index + 1}`} value={rule.serviceTypeCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, serviceTypeCode: ev.target.value } : r))} /></td>
                    <td><input aria-label={`Mã loại hàng ${index + 1}`} value={rule.commodityCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, commodityCode: ev.target.value } : r))} /></td>
                    <td>
                      <select aria-label={`Phương thức ${index + 1}`} value={rule.transportMode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, transportMode: ev.target.value } : r))}>
                        <option value="">Mọi phương thức</option>
                        {modeOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                      </select>
                      <select aria-label={`Lọc chiều giá ${index + 1}`} value={rule.partyTypeCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, partyTypeCode: ev.target.value } : r))}>
                        <option value="">Mọi chiều giá</option>
                        <option value="vendor">Giá mua</option>
                        <option value="customer">Giá bán</option>
                      </select>
                      <input aria-label={`Lọc tuyến ${index + 1}`} placeholder="Lọc tuyến" value={rule.routeCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, routeCode: ev.target.value } : r))} />
                    </td>
                    <td>
                      <button className="btn btn-ghost" type="button" onClick={() => setRules((prev) => prev.length === 1 ? [emptyRule(currency)] : prev.filter((r) => r.key !== rule.key))}>Xóa</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="cta-row">
            <button className="btn btn-ghost" type="button" onClick={() => setRules((prev) => [...prev, emptyRule(currency)])}>+ Thêm quy tắc giá</button>
          </div>
        </>
      ) : (
        <>
          <h2 className="section-title">3. Bảng giá nâng cao</h2>
          <p className="note">
            Đơn giá theo khung {unit} và loại hàng. Ô trống nghĩa là khung đó không có giá.
            Mức Đến trùng mức Từ của khung sau thì số lẻ thuộc khung dưới.
          </p>
          <div className="cta-row">
            <button className="btn btn-ghost" type="button" onClick={() => applyPreset("air")}>Khung Air</button>
            <button className="btn btn-ghost" type="button" onClick={() => applyPreset("sea")}>Khung Sea</button>
          </div>
          <div className="table-wrap">
            <table className="data-table">
              <caption className="muted small">Đơn giá / {unit}</caption>
              <thead>
                <tr>
                  <th>Khung ({unit})</th>
                  {columns.map((col) => (
                    <th key={col.key}>
                      <input aria-label={`Tên ${col.code}`} value={col.name} onChange={(ev) => setColumns((prev) => prev.map((c) => c.key === col.key ? { ...c, name: ev.target.value } : c))} />
                      <input aria-label={`Mã ${col.name}`} value={col.code} onChange={(ev) => setColumns((prev) => prev.map((c) => c.key === col.key ? { ...c, code: ev.target.value.toUpperCase() } : c))} />
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row, index) => (
                  <tr key={row.key}>
                    <th>
                      <span className="muted small">{preview(row, rows[index + 1])}</span>
                      <div className="cta-row">
                        <input aria-label="Từ" value={row.min} inputMode="decimal" style={{ width: "4.5rem" }} onChange={(ev) => setRows((prev) => prev.map((r) => r.key === row.key ? { ...r, min: ev.target.value } : r))} />
                        <input aria-label="Đến" value={row.max} inputMode="decimal" placeholder="mở" style={{ width: "4.5rem" }} onChange={(ev) => setRows((prev) => prev.map((r) => r.key === row.key ? { ...r, max: ev.target.value } : r))} />
                        <button className="btn btn-ghost" type="button" disabled={rows.length <= 1} onClick={() => setRows((prev) => prev.filter((r) => r.key !== row.key))}>Xóa</button>
                      </div>
                    </th>
                    {columns.map((col) => (
                      <td key={col.key}>
                        <input aria-label={`${col.name} ${preview(row, rows[index + 1])}`} inputMode="decimal" value={cells[row.key]?.[col.key] ?? ""} onChange={(ev) => setCell(row.key, col.key, ev.target.value)} style={{ width: "7rem" }} />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="cta-row">
            <button className="btn btn-ghost" type="button" onClick={() => setRows((prev) => [...prev, { key: uid(), min: "", max: "" }])}>Thêm khung</button>
            <button className="btn btn-ghost" type="button" onClick={() => setColumns((prev) => [...prev, { key: uid(), name: "Loại hàng", code: `COL${prev.length + 1}` }])}>Thêm loại hàng</button>
          </div>
          {!editing ? (
            <div className="form-grid">
              <div className="field">
                <label htmlFor="minQty">Số lượng tối thiểu</label>
                <input id="minQty" value={minQty} inputMode="decimal" onChange={(ev) => setMinQty(ev.target.value)} />
              </div>
              <div className="field checkbox-field">
                <label><input type="checkbox" checked={deliveryOn} onChange={(ev) => setDeliveryOn(ev.target.checked)} /> Phí giao theo khối</label>
              </div>
              {deliveryOn ? (
                <>
                  <div className="field">
                    <label htmlFor="deliveryUnder">Áp khi dưới</label>
                    <input id="deliveryUnder" value={deliveryUnder} inputMode="decimal" onChange={(ev) => setDeliveryUnder(ev.target.value)} />
                  </div>
                  <div className="field">
                    <label htmlFor="deliveryAmount">Số tiền</label>
                    <input id="deliveryAmount" value={deliveryAmount} inputMode="decimal" onChange={(ev) => setDeliveryAmount(ev.target.value)} />
                  </div>
                </>
              ) : null}
              <div className="field checkbox-field">
                <label><input type="checkbox" checked={remoteOn} onChange={(ev) => setRemoteOn(ev.target.checked)} /> Phụ phí vùng — tạo ở Quản lý phụ phí, gắn tùy chọn vào bảng giá này</label>
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
                    <select id="remoteCurrency" value={remoteCurrency} onChange={(ev) => setRemoteCurrency(ev.target.value)}>
                      {currencyChoices.map((code) => <option key={code} value={code}>{code}</option>)}
                    </select>
                  </div>
                </>
              ) : null}
            </div>
          ) : (
            <p className="note">Phụ phí đã tách khỏi ma trận. Sửa phụ phí tại Quản lý phụ phí. Lưu nháp chỉ thay khung giá của bản nháp.</p>
          )}
        </>
      )}

      <div className="cta-row">
        <Link className="btn btn-ghost" href={editing ? `/rate-cards/${props.cardId}` : "/rate-cards"}>Hủy</Link>
        <button className="btn" type="submit" disabled={busy}>{busy ? "Đang lưu…" : "Lưu nháp"}</button>
      </div>
    </form>
  );
}
