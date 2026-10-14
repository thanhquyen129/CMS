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
  weightBasisOptions,
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
  serviceTypeCode: string;
  commodityCode: string;
  transportMode: string;
  partyTypeCode: string;
  routeId: string;
  appliesAnyRoute: boolean;
  carrierPartyId: string;
  carrierName: string;
  weightBasis: string;
  weightMin: string;
  weightMax: string;
  chargeCode: string;
  applicability: string;
  minAmount: string;
  maxAmount: string;
};

type MatrixGroup = {
  key: string;
  code: string;
  routeId: string;
  appliesAnyRoute: boolean;
  carrierPartyId: string;
  carrierName: string;
  transportMode: string;
  columns: Col[];
  rows: Band[];
  cells: Record<string, Record<string, string>>;
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
  rowVersion?: string | null;
};

const modeOptions = [
  { value: "air", label: "Hàng không" },
  { value: "sea", label: "Đường biển" },
  { value: "road", label: "Đường bộ" },
  { value: "rail", label: "Đường sắt" },
  { value: "water", label: "Đường thủy" },
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

function emptyRule(key?: string): RuleRow {
  return {
    key: key ?? uid(),
    code: "",
    name: "",
    calcMethod: "fixed",
    unitAmount: "",
    serviceTypeCode: "",
    commodityCode: "",
    transportMode: "",
    partyTypeCode: "",
    routeId: "",
    appliesAnyRoute: false,
    carrierPartyId: "",
    carrierName: "",
    weightBasis: "",
    weightMin: "",
    weightMax: "",
    chargeCode: "",
    applicability: "",
    minAmount: "",
    maxAmount: "",
  };
}

function rulesFromSaved(rules: PricingRule[]): RuleRow[] {
  const basic = rules.filter((r) => !isWeightBreakMethod(r.calcMethod));
  if (basic.length === 0) return [emptyRule("rule-1")];
  return basic.map((r) => ({
    key: r.id,
    code: r.code,
    name: r.name,
    calcMethod: r.calcMethod,
    unitAmount: String(r.unitAmount),
    serviceTypeCode: r.serviceTypeCode ?? "",
    commodityCode: r.commodityCode ?? "",
    transportMode: r.transportMode ?? "",
    partyTypeCode: r.partyTypeCode ?? "",
    routeId: r.routeId ?? "",
    appliesAnyRoute: r.appliesAnyRoute === true,
    carrierPartyId: r.carrierPartyId ?? "",
    carrierName: r.carrierName ?? "",
    weightBasis: r.weightBasis ?? "",
    weightMin: r.weightMin == null ? "" : String(r.weightMin),
    weightMax: r.weightMax == null ? "" : String(r.weightMax),
    chargeCode: r.chargeCode ?? "",
    applicability: r.applicability ?? "",
    minAmount: r.minAmount == null ? "" : String(r.minAmount),
    maxAmount: r.maxAmount == null ? "" : String(r.maxAmount),
  }));
}

function emptyGroup(preset: "air" | "sea", routeId = "", appliesAnyRoute = false, key?: string): MatrixGroup {
  const id = key ?? uid();
  return {
    key: id,
    code: "",
    routeId,
    appliesAnyRoute,
    carrierPartyId: "",
    carrierName: "",
    transportMode: preset,
    columns: preset === "sea" ? seaCols(id) : airCols(id),
    rows: preset === "sea" ? seaBands(id) : airBands(id),
    cells: {},
  };
}

function indexed<T extends { key: string }>(prefix: string, kind: string, rows: Omit<T, "key">[]): T[] {
  return rows.map((row, index) => ({ ...row, key: `${prefix}-${kind}${index + 1}` }) as T);
}

const airBands = (prefix: string): Band[] =>
  indexed<Band>(prefix, "b", [
    { min: "0", max: "6" },
    { min: "6", max: "11" },
    { min: "11", max: "21" },
    { min: "21", max: "101" },
    { min: "101", max: "301" },
    { min: "301", max: "501" },
    { min: "501", max: "" },
  ]);

const airCols = (prefix: string): Col[] =>
  indexed<Col>(prefix, "c", [
    { name: "Hàng thường", code: "GENERAL" },
    { name: "Thực phẩm khô", code: "DRY_FOOD" },
    { name: "Mỹ phẩm", code: "COSMETICS" },
    { name: "Chuyển nhanh", code: "EXPRESS" },
  ]);

const seaBands = (prefix: string): Band[] =>
  indexed<Band>(prefix, "b", [
    { min: "0", max: "4" },
    { min: "4", max: "7" },
    { min: "7", max: "10.001" },
    { min: "10.001", max: "" },
  ]);

const seaCols = (prefix: string): Col[] =>
  indexed<Col>(prefix, "c", [
    { name: "Hàng thường", code: "GENERAL" },
    { name: "Mỹ phẩm, thực phẩm", code: "COSMETICS_FOOD" },
  ]);

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

function groupsFromRules(rules: PricingRule[], preset: "air" | "sea", fallbackRouteId: string): MatrixGroup[] {
  const matrix = rules.filter((r) => isWeightBreakMethod(r.calcMethod) && (r.breaks?.length ?? 0) > 0);
  if (matrix.length === 0) return [emptyGroup(preset, fallbackRouteId, false, "group-1")];
  const order: string[] = [];
  const byCode = new Map<string, PricingRule[]>();
  for (const rule of matrix) {
    const code = rule.matrixGroupCode || "legacy";
    if (!byCode.has(code)) {
      order.push(code);
      byCode.set(code, []);
    }
    byCode.get(code)!.push(rule);
  }
  return order.map((code) => {
    const members = byCode.get(code)!;
    const head = members[0];
    const shaped = matrixFromRules(members);
    return {
      key: head.id,
      code: code === "legacy" ? "" : code,
      routeId: head.appliesAnyRoute ? "" : head.routeId ?? fallbackRouteId,
      appliesAnyRoute: head.appliesAnyRoute === true,
      carrierPartyId: head.carrierPartyId ?? "",
      carrierName: head.carrierName ?? "",
      transportMode: head.transportMode || preset,
      columns: shaped?.columns ?? airCols(head.id),
      rows: shaped?.rows ?? airBands(head.id),
      cells: shaped?.cells ?? {},
    };
  });
}

export function RateCardDeclarationForm(props: Props) {
  const router = useRouter();
  const editing = Boolean(props.cardId);
  const initialMode = props.declarationMode === "advanced" ? "advanced" : "basic";
  const startPreset = props.transportMode?.toLowerCase() === "sea" ? "sea" : "air";
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [partyType, setPartyType] = useState(props.partyType === "customer" ? "customer" : "vendor");
  const [partnerEpoch, setPartnerEpoch] = useState(0);
  const [partnerNotice, setPartnerNotice] = useState<string | null>(null);
  const [currency, setCurrency] = useState((props.currencyCode || props.defaultCurrency || "VND").toUpperCase());
  const [routeQuery, setRouteQuery] = useState("");
  const [mode, setMode] = useState<"basic" | "advanced">(initialMode);
  const [pendingMode, setPendingMode] = useState<"basic" | "advanced" | null>(null);
  const [rules, setRules] = useState<RuleRow[]>(() => {
    const rows = rulesFromSaved(props.rules ?? []);
    if (!rows[0].routeId && !rows[0].appliesAnyRoute && props.routeId) {
      rows[0] = { ...rows[0], routeId: props.routeId };
    }
    return rows;
  });
  const [preset, setPreset] = useState<"air" | "sea">(startPreset);
  const [groups, setGroups] = useState<MatrixGroup[]>(() => groupsFromRules(props.rules ?? [], startPreset, props.routeId ?? ""));
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
  const filteredRoutes = props.routes.filter((r) => {
    if (!r.isActive && !rules.some((rule) => rule.routeId === r.id) && !groups.some((group) => group.routeId === r.id)) return false;
    const q = routeQuery.trim().toLowerCase();
    if (!q) return true;
    return [r.code, r.name, r.originCode, r.destinationCode].some((part) => part?.toLowerCase().includes(q));
  });
  const priced = useMemo(
    () => groups.some((group) => Object.values(group.cells).some((row) => Object.values(row).some((v) => v.trim() !== ""))),
    [groups]
  );
  const basicDirty = rules.some((r) => r.code.trim() || r.name.trim() || r.unitAmount.trim());
  const advancedDirty = priced || deliveryOn || remoteOn || (initialMode === "advanced" && mode === "advanced");

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
    if (mode === "basic") {
      const seed = rules[0];
      setRules([emptyRule()]);
      setGroups([emptyGroup(preset, seed?.routeId ?? "", seed?.appliesAnyRoute ?? false)]);
    }
    if (mode === "advanced") {
      const seed = groups[0];
      setGroups([emptyGroup(preset)]);
      setRules([{ ...emptyRule(), routeId: seed?.routeId ?? "", appliesAnyRoute: seed?.appliesAnyRoute ?? false }]);
      setDeliveryOn(false);
      setRemoteOn(false);
    }
    setMode(pendingMode);
    setPendingMode(null);
  }

  function applyPreset(groupKey: string, next: "air" | "sea") {
    const group = groups.find((item) => item.key === groupKey);
    const hasPrice = group ? Object.values(group.cells).some((row) => Object.values(row).some((v) => v.trim() !== "")) : false;
    if (hasPrice && !window.confirm("Đổi khung sẽ xóa đơn giá đã nhập trên ma trận. Tiếp tục?")) return;
    setPreset(next);
    setGroups((prev) => prev.map((item) => item.key === groupKey
      ? { ...item, transportMode: next, columns: next === "sea" ? seaCols(groupKey) : airCols(groupKey), rows: next === "sea" ? seaBands(groupKey) : airBands(groupKey), cells: {} }
      : item));
    setMinQty(next === "sea" ? "1" : "");
  }

  function setCell(groupKey: string, rowKey: string, colKey: string, value: string) {
    setGroups((prev) => prev.map((item) => item.key === groupKey
      ? { ...item, cells: { ...item.cells, [rowKey]: { ...(item.cells[rowKey] ?? {}), [colKey]: value } } }
      : item));
  }

  function patchGroup(groupKey: string, patch: Partial<MatrixGroup>) {
    setGroups((prev) => prev.map((item) => item.key === groupKey ? { ...item, ...patch } : item));
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    const fd = new FormData(e.currentTarget);
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
      carrierName: null,
      routeId: null,
      declarationMode: mode,
      confirmModeChange: mode !== initialMode,
      supplierPartyId: sell ? null : String(fd.get("supplierPartyId") ?? "").trim() || null,
      customerPartyId: sell ? String(fd.get("customerPartyId") ?? "").trim() || null : null,
      customerGroupCode: sell ? String(fd.get("customerGroupCode") ?? "").trim() || null : null,
      note: String(fd.get("note") ?? "").trim() || null,
      rateVersionId: props.versionId ?? null,
      rowVersion: props.rowVersion ?? null,
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
        if (!rule.appliesAnyRoute && !rule.routeId) {
          setError(activeRoutes.length === 0
            ? "Danh mục tuyến trống hoặc không có tuyến đang hoạt động. Không tạo tuyến giả."
            : "Chọn tuyến từ danh mục hoặc Tất cả tuyến.");
          return;
        }
        const min = parseNum(rule.weightMin);
        const max = rule.weightMax.trim() ? parseNum(rule.weightMax) : null;
        if ((rule.weightMin.trim() || rule.weightMax.trim()) && (min === null || min < 0 || (rule.weightMax.trim() && (max === null || max <= min)))) {
          setError("Nấc trọng lượng cần mức từ không âm và mức đến lớn hơn mức từ.");
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
        currencyCode: currency,
        serviceTypeCode: rule.serviceTypeCode.trim() || null,
        commodityCode: rule.commodityCode.trim() || null,
        transportMode: rule.transportMode.trim() || null,
        partyTypeCode: rule.partyTypeCode.trim() || null,
        routeId: rule.appliesAnyRoute ? null : rule.routeId || null,
        appliesAnyRoute: rule.appliesAnyRoute,
        carrierPartyId: rule.carrierPartyId || null,
        carrierName: rule.carrierName.trim() || null,
        weightBasis: rule.weightBasis || null,
        weightMin: parseNum(rule.weightMin),
        weightMax: rule.weightMax.trim() ? parseNum(rule.weightMax) : null,
        chargeCode: rule.chargeCode.trim() || null,
        applicability: rule.applicability.trim() || null,
        minAmount: parseNum(rule.minAmount),
        maxAmount: parseNum(rule.maxAmount),
      }));
      body.transportMode = props.transportMode ?? null;
    } else {
      for (const group of groups) {
        if (!group.appliesAnyRoute && !group.routeId) {
          setError(activeRoutes.length === 0
            ? "Danh mục tuyến trống hoặc không có tuyến đang hoạt động. Không tạo tuyến giả."
            : "Chọn tuyến hoặc Tất cả tuyến cho từng nhóm ma trận.");
          return;
        }
      }
      const first = groups[0];
      const rows = first.rows;
      const columns = first.columns;
      const cells = first.cells;
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
      body.transportMode = first.transportMode || preset;
      body.minimumQuantity = parseNum(minQty);
      body.groups = groups.map((group, index) => ({
        code: group.code.trim() || `G${index + 1}`,
        routeId: group.appliesAnyRoute ? null : group.routeId || null,
        appliesAnyRoute: group.appliesAnyRoute,
        carrierPartyId: group.carrierPartyId || null,
        carrierName: group.carrierName.trim() || null,
        transportMode: group.transportMode || preset,
        columns: group.columns.map((c) => ({ code: c.code.trim(), name: c.name.trim() })),
        bands: group.rows.map((row) => ({
          minQuantity: parseNum(row.min),
          maxQuantity: row.max.trim() ? parseNum(row.max) : null,
          prices: group.columns.map((col) => {
            const raw = group.cells[row.key]?.[col.key] ?? "";
            return raw.trim() ? parseNum(raw) : null;
          }),
        })),
      }));
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
    <form className="receive-form rate-declaration" onSubmit={onSubmit} noValidate>
      <p className="note">
        Nhập thông tin chung và chọn một hình thức khai báo. Mỗi bảng giá chỉ dùng bảng giá cơ bản hoặc bảng giá nâng cao.
      </p>
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      {props.routeError ? <div className="alert alert-error" role="alert">{props.routeError}</div> : null}
      {props.currencyError ? <div className="alert alert-error" role="alert">{props.currencyError}</div> : null}

      <section className="rate-block rate-block-general">
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
          <p className="muted small">Một tiền tệ cho cả bảng giá. Quy tắc bên dưới không ghi đè.</p>
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
      </section>

      <section className="rate-block rate-block-mode">
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
      </section>
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
        <section className="rate-block rate-block-rules">
          <h2 className="section-title">3. Bảng giá cơ bản</h2>
          <p className="note">Quy tắc dùng tiền tệ {currency}. Tuyến và hãng vận chuyển khai báo trên từng quy tắc. Nấc trọng lượng là điều kiện chọn, không phải bậc lũy tiến.</p>
          <div className="table-wrap">
            <table className="data-table rate-rules">
              <thead>
                <tr>
                  <th className="col-idx" scope="col">#</th>
                  <th className="col-code" scope="col">Mã quy tắc</th>
                  <th className="col-name" scope="col">Tên quy tắc</th>
                  <th className="col-route" scope="col">Tuyến</th>
                  <th className="col-carrier" scope="col">Hãng vận chuyển</th>
                  <th className="col-weight" scope="col">Nấc trọng lượng</th>
                  <th className="col-method" scope="col">Cách tính</th>
                  <th className="col-amount" scope="col">Số tiền / Đơn giá</th>
                  <th className="col-service" scope="col">Loại dịch vụ</th>
                  <th className="col-commodity" scope="col">Loại hàng</th>
                  <th className="col-mode" scope="col">Phương thức</th>
                  <th className="col-actions" scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((rule, index) => (
                  <tr key={rule.key}>
                    <td>{index + 1}</td>
                    <td><input aria-label={`Mã quy tắc ${index + 1}`} value={rule.code} maxLength={64} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, code: ev.target.value } : r))} /></td>
                    <td><input aria-label={`Tên quy tắc ${index + 1}`} value={rule.name} maxLength={256} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, name: ev.target.value } : r))} /></td>
                    <td className="col-route">
                      <label htmlFor={`route-search-${rule.key}`}>Tuyến {index + 1}</label>
                      <input id={`route-search-${rule.key}`} value={routeQuery} onChange={(ev) => setRouteQuery(ev.target.value)} placeholder="Tìm mã tuyến, điểm đi, điểm đến" autoComplete="off" />
                      <select
                        aria-label={`Chọn tuyến ${index + 1}`}
                        value={rule.appliesAnyRoute ? "ANY" : rule.routeId}
                        onChange={(ev) => {
                          const value = ev.target.value;
                          setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, appliesAnyRoute: value === "ANY", routeId: value === "ANY" ? "" : value } : r));
                        }}
                      >
                        <option value="">{activeRoutes.length === 0 ? "Không có tuyến đang hoạt động" : "Chọn tuyến"}</option>
                        <option value="ANY">Tất cả tuyến</option>
                        {filteredRoutes.map((route) => (
                          <option key={route.id} value={route.id}>{routeOptionLabel(route)}{route.isActive ? "" : " — ngừng sử dụng"}</option>
                        ))}
                      </select>
                      {props.routes.some((route) => route.id === rule.routeId && !route.isActive) ? (
                        <p className="alert alert-warning" role="status">Tuyến đã lưu ngừng hiệu lực. Giữ nguyên hoặc chọn tuyến đang hoạt động.</p>
                      ) : null}
                      {activeRoutes.length === 0 ? (
                        <p className="alert alert-warning" role="status">Danh mục tuyến trống hoặc không có tuyến đang hoạt động. Không tạo tuyến giả.</p>
                      ) : null}
                    </td>
                    <td className="col-carrier">
                      <PartyTypeahead
                        name={`carrier-${rule.key}`}
                        label={`Hãng vận chuyển ${index + 1}`}
                        roleCode="carrier"
                        defaultId={rule.carrierPartyId || null}
                        defaultLabel={rule.carrierName || null}
                        hint="Hãng khai thác, không phải nhà cung cấp trên bảng giá."
                        onSelect={(item) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, carrierPartyId: item?.id ?? "", carrierName: item?.name ?? "" } : r))}
                      />
                    </td>
                    <td className="col-weight">
                      <div className="muted small">Loại trọng lượng</div>
                      <select aria-label={`Loại trọng lượng ${index + 1}`} value={rule.weightBasis} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, weightBasis: ev.target.value } : r))}>
                        {weightBasisOptions.map((item) => <option key={item.value || "none"} value={item.value}>{item.label}</option>)}
                      </select>
                      <div className="muted small">Trọng lượng từ (kg)</div>
                      <input aria-label={`Trọng lượng từ ${index + 1}`} placeholder="min" inputMode="decimal" value={rule.weightMin} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, weightMin: ev.target.value } : r))} />
                      <div className="muted small">Trọng lượng đến (kg)</div>
                      <input aria-label={`Trọng lượng đến ${index + 1}`} placeholder="max, bỏ trống nếu mở" inputMode="decimal" value={rule.weightMax} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, weightMax: ev.target.value } : r))} />
                    </td>
                    <td className="col-method">
                      <select aria-label={`Cách tính ${index + 1}`} value={rule.calcMethod} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, calcMethod: ev.target.value } : r))}>
                        {basicCalcMethods.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                      </select>
                      {rule.calcMethod === "unit_rate" ? (
                        <>
                          <div className="muted small">Đơn vị tính</div>
                          <select aria-label={`Đơn vị áp dụng ${index + 1}`} value={rule.applicability} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, applicability: ev.target.value } : r))}>
                            <option value="">Theo số lượng cước</option>
                            <option value="per_gross_kg">Theo kg thực</option>
                          </select>
                        </>
                      ) : null}
                    </td>
                    <td className="col-amount">
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
                    <td><input aria-label={`Loại dịch vụ ${index + 1}`} value={rule.serviceTypeCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, serviceTypeCode: ev.target.value } : r))} /></td>
                    <td><input aria-label={`Mã loại hàng ${index + 1}`} value={rule.commodityCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, commodityCode: ev.target.value } : r))} /></td>
                    <td className="col-mode">
                      <select aria-label={`Phương thức ${index + 1}`} value={rule.transportMode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, transportMode: ev.target.value } : r))}>
                        <option value="">Mọi phương thức</option>
                        {modeOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                      </select>
                      <select aria-label={`Lọc chiều giá ${index + 1}`} value={rule.partyTypeCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, partyTypeCode: ev.target.value } : r))}>
                        <option value="">Mọi chiều giá</option>
                        <option value="vendor">Giá mua</option>
                        <option value="customer">Giá bán</option>
                      </select>
                      <input aria-label={`Khoản mục ${index + 1}`} placeholder="Khoản mục, ví dụ cước chính" value={rule.chargeCode} onChange={(ev) => setRules((prev) => prev.map((r) => r.key === rule.key ? { ...r, chargeCode: ev.target.value } : r))} />
                    </td>
                    <td>
                      <button className="btn btn-ghost" type="button" onClick={() => setRules((prev) => prev.length === 1 ? [emptyRule()] : prev.filter((r) => r.key !== rule.key))}>Xóa</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="cta-row">
            <button className="btn btn-ghost" type="button" onClick={() => setRules((prev) => [...prev, emptyRule()])}>+ Thêm quy tắc giá</button>
          </div>
        </section>
      ) : (
        <>
          <h2 className="section-title">3. Bảng giá nâng cao</h2>
          <p className="note">
            Đơn giá theo khung {unit} và loại hàng. Ô trống nghĩa là khung đó không có giá.
            Mức Đến trùng mức Từ của khung sau thì số lẻ thuộc khung dưới.
          </p>
          {groups.map((group, groupIndex) => {
            const groupUnit = group.transportMode === "sea" ? "CBM" : "kg";
            return (
              <section key={group.key} className="rate-block rate-block-rules">
                <h3 className="section-title sm">Nhóm ma trận {groupIndex + 1}</h3>
                <div className="form-grid">
                  <div className="field">
                    <label htmlFor={`group-route-${group.key}`}>Tuyến nhóm {groupIndex + 1}</label>
                    <select
                      id={`group-route-${group.key}`}
                      value={group.appliesAnyRoute ? "ANY" : group.routeId}
                      onChange={(ev) => {
                        const value = ev.target.value;
                        patchGroup(group.key, { appliesAnyRoute: value === "ANY", routeId: value === "ANY" ? "" : value });
                      }}
                    >
                      <option value="">Chọn tuyến</option>
                      <option value="ANY">Tất cả tuyến</option>
                      {props.routes.filter((route) => route.isActive || route.id === group.routeId).map((route) => (
                        <option key={route.id} value={route.id}>{routeOptionLabel(route)}</option>
                      ))}
                    </select>
                  </div>
                  <div className="field">
                    <PartyTypeahead
                      name={`group-carrier-${group.key}`}
                      label={`Hãng nhóm ${groupIndex + 1}`}
                      roleCode="carrier"
                      defaultId={group.carrierPartyId || null}
                      defaultLabel={group.carrierName || null}
                      hint="Hãng khai thác của nhóm, không phải nhà cung cấp."
                      onSelect={(item) => patchGroup(group.key, { carrierPartyId: item?.id ?? "", carrierName: item?.name ?? "" })}
                    />
                  </div>
                  <div className="field">
                    <label htmlFor={`group-mode-${group.key}`}>Phương thức nhóm {groupIndex + 1}</label>
                    <select id={`group-mode-${group.key}`} value={group.transportMode} onChange={(ev) => patchGroup(group.key, { transportMode: ev.target.value })}>
                      {modeOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                    </select>
                  </div>
                </div>
                <div className="cta-row">
                  <button className="btn btn-ghost" type="button" onClick={() => applyPreset(group.key, "air")}>Khung hàng không</button>
                  <button className="btn btn-ghost" type="button" onClick={() => applyPreset(group.key, "sea")}>Khung đường biển</button>
                  <button className="btn btn-ghost" type="button" onClick={() => setGroups((prev) => [...prev, { ...group, key: uid(), code: "", cells: { ...group.cells } }])}>Sao chép nhóm</button>
                  <button className="btn btn-ghost" type="button" disabled={groups.length <= 1} onClick={() => setGroups((prev) => prev.filter((item) => item.key !== group.key))}>Xóa nhóm</button>
                </div>
                <div className="table-wrap">
                  <table className="data-table">
                    <caption className="muted small">Đơn giá / {groupUnit}. Nấc [từ, đến).</caption>
                    <thead>
                      <tr>
                        <th scope="col">Khung ({groupUnit})</th>
                        {group.columns.map((col) => (
                          <th key={col.key} scope="col">
                            <input aria-label={`Tên ${col.code}`} value={col.name} onChange={(ev) => patchGroup(group.key, { columns: group.columns.map((c) => c.key === col.key ? { ...c, name: ev.target.value } : c) })} />
                            <input aria-label={`Mã ${col.name}`} value={col.code} onChange={(ev) => patchGroup(group.key, { columns: group.columns.map((c) => c.key === col.key ? { ...c, code: ev.target.value.toUpperCase() } : c) })} />
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {group.rows.map((row, index) => (
                        <tr key={row.key}>
                          <th>
                            <span className="muted small">{preview(row, group.rows[index + 1])}</span>
                            <div className="cta-row">
                              <input aria-label="Từ" value={row.min} inputMode="decimal" style={{ width: "4.5rem" }} onChange={(ev) => patchGroup(group.key, { rows: group.rows.map((r) => r.key === row.key ? { ...r, min: ev.target.value } : r) })} />
                              <input aria-label="Đến" value={row.max} inputMode="decimal" placeholder="mở" style={{ width: "4.5rem" }} onChange={(ev) => patchGroup(group.key, { rows: group.rows.map((r) => r.key === row.key ? { ...r, max: ev.target.value } : r) })} />
                              <button className="btn btn-ghost" type="button" disabled={group.rows.length <= 1} onClick={() => patchGroup(group.key, { rows: group.rows.filter((r) => r.key !== row.key) })}>Xóa</button>
                            </div>
                          </th>
                          {group.columns.map((col) => (
                            <td key={col.key}>
                              <input aria-label={`${col.name} ${preview(row, group.rows[index + 1])}`} inputMode="decimal" value={group.cells[row.key]?.[col.key] ?? ""} onChange={(ev) => setCell(group.key, row.key, col.key, ev.target.value)} style={{ width: "7rem" }} />
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="cta-row">
                  <button className="btn btn-ghost" type="button" onClick={() => patchGroup(group.key, { rows: [...group.rows, { key: uid(), min: "", max: "" }] })}>Thêm khung</button>
                  <button className="btn btn-ghost" type="button" onClick={() => patchGroup(group.key, { columns: [...group.columns, { key: uid(), name: "Loại hàng", code: `COL${group.columns.length + 1}` }] })}>Thêm loại hàng</button>
                </div>
              </section>
            );
          })}
          <div className="cta-row">
            <button className="btn btn-ghost" type="button" onClick={() => setGroups((prev) => [...prev, emptyGroup(preset)])}>Thêm nhóm</button>
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
