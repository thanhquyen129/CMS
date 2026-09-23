import { formatMoney } from "@/lib/money";
import {
  isWeightBreakMethod,
  type PricingRule,
  type RateBreak,
} from "@/lib/rate-cards";

type Props = {
  rules: PricingRule[];
  currencyCode: string;
  transportMode: string | null | undefined;
};

function bandKey(band: RateBreak): string {
  return `${band.minQuantity}|${band.maxQuantity ?? ""}`;
}

function bandLabel(min: number, max: number | null): string {
  if (min === 0 && max != null) {
    return `< ${Math.round(max + 0.001)}`;
  }

  if (max == null) {
    return `> ${Math.floor(min + 0.0001)}`;
  }

  return `${trimNum(min)} – ${Math.floor(max + 0.0001)}`;
}

function trimNum(value: number): string {
  return Number.isInteger(value) ? String(value) : String(value);
}

export function TariffMatrix({ rules, currencyCode, transportMode }: Props) {
  const freight = rules
    .filter(
      (r) =>
        isWeightBreakMethod(r.calcMethod) && (r.breaks?.length ?? 0) > 0
    )
    .slice()
    .sort((a, b) => a.sortOrder - b.sortOrder);
  if (freight.length === 0) {
    return null;
  }

  const surcharges = rules.filter((r) => !freight.some((f) => f.id === r.id));
  const rowKeys: string[] = [];
  const rowLabel = new Map<string, string>();
  for (const rule of freight) {
    for (const band of rule.breaks ?? []) {
      const key = bandKey(band);
      if (!rowLabel.has(key)) {
        rowKeys.push(key);
        rowLabel.set(key, bandLabel(band.minQuantity, band.maxQuantity));
      }
    }
  }

  const unit =
    transportMode?.toLowerCase() === "sea" ? "CBM" : "kg";
  const moneyCode = freight[0]?.currencyCode || currencyCode;

  return (
    <div className="stack">
      <div className="table-wrap">
        <table className="data-table">
          <caption className="muted small" style={{ captionSide: "top", textAlign: "left", padding: "0 0 0.5rem" }}>
            Đơn giá ({moneyCode}/{unit})
          </caption>
          <thead>
            <tr>
              <th scope="col">{unit === "CBM" ? "Khối (CBM)" : "Trọng lượng (kg)"}</th>
              {freight.map((rule) => (
                <th key={rule.id} scope="col" className="num">
                  {rule.name}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rowKeys.map((key) => (
              <tr key={key}>
                <th scope="row">{rowLabel.get(key)}</th>
                {freight.map((rule) => {
                  const band = (rule.breaks ?? []).find((b) => bandKey(b) === key);
                  return (
                    <td key={rule.id} className="num">
                      {band
                        ? formatMoney(band.unitAmount, rule.currencyCode || moneyCode)
                        : "—"}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {surcharges.length > 0 ? (
        <div>
          <h4 className="section-title sm">Phụ phí</h4>
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Khoản</th>
                  <th scope="col">Điều kiện</th>
                  <th scope="col" className="num">
                    Đơn giá
                  </th>
                </tr>
              </thead>
              <tbody>
                {surcharges.map((rule) => (
                  <tr key={rule.id}>
                    <td>{rule.name}</td>
                    <td className="muted small">{surchargeCondition(rule)}</td>
                    <td className="num">{surchargeAmount(rule)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function surchargeCondition(rule: PricingRule): string {
  const parts: string[] = [];
  if (rule.destinationCode) {
    parts.push(`Điểm đến ${rule.destinationCode}`);
  }

  if (rule.applicability === "per_gross_kg") {
    parts.push("nhân kg thực, không nhân CBM");
  }

  const bands = rule.breaks ?? [];
  if (bands.length > 0) {
    const paid = bands.find((b) => b.unitAmount > 0);
    if (paid) {
      parts.push(
        `khối tính cước ${bandLabel(paid.minQuantity, paid.maxQuantity)}`
      );
    }
  }

  return parts.join(" · ") || "Luôn áp khi chọn bảng này";
}

function surchargeAmount(rule: PricingRule): string {
  const paid = (rule.breaks ?? []).find((b) => b.unitAmount > 0);
  if (paid) {
    return formatMoney(paid.unitAmount, rule.currencyCode);
  }

  if (rule.unitAmount > 0) {
    const suffix = rule.applicability === "per_gross_kg" || rule.calcMethod === "unit_rate"
      ? "/kg"
      : "";
    return `${formatMoney(rule.unitAmount, rule.currencyCode)}${suffix}`;
  }

  return "—";
}
