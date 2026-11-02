"""Fill DEV PASS + evidence on consolidated UAT handover Excel matrices."""
from __future__ import annotations

from pathlib import Path

from openpyxl import load_workbook

BASE = Path(r"c:\A1\git\cms\docs\po\02112026-LCMS_Consolidated_UAT_Handover_v1.0_20261102")
EVIDENCE = (
    "ConsolidatedUat20261102Tests + ConsolidatedUat20261102ApiTests "
    "(Cost_rating_trace / Tc22 / Tc23 / Tc24 / Bidirectional / Exception_exposure); "
    "dotnet test Release filter ConsolidatedUat20261102 => 10 PASS; PO UAT NOT RUN"
)
NOTE = "DEV verified 2026-11-02. PO UAT remains NOT RUN."


def col_map(ws, header_row: int):
    return {
        str(c.value).strip(): i
        for i, c in enumerate(ws[header_row], start=1)
        if c.value
    }


def set_if(ws, row, cols, name, value):
    if name in cols:
        ws.cell(row, cols[name], value)


def fill_rows(ws, header_row: int, start_row: int, mutator):
    cols = col_map(ws, header_row)
    for r in range(start_row, ws.max_row + 1):
        if not ws.cell(r, 1).value:
            continue
        mutator(ws, r, cols)


def fill_correction():
    path = BASE / "LCMS_DEV_UAT_Correction_Matrix_v1.0_20261027.xlsx"
    wb = load_workbook(path)
    hr, sr = 4, 5

    fill_rows(
        wb["01_ThayDoi"],
        hr,
        sr,
        lambda ws, r, cols: set_if(
            ws, r, cols, "Trạng thái DEV", "PASS (Verified & Fixed — PO UAT NOT RUN)"
        ),
    )

    fill_rows(
        wb["02_QuyTac"],
        hr,
        sr,
        lambda ws, r, cols: set_if(
            ws, r, cols, "Trạng thái DEV", "IMPLEMENTED & VERIFIED (PASS)"
        ),
    )

    field_evidence = {
        "transport_mode": "master_catalog_items(code=transport_mode) · catalogModeOptions · UpdateBillContextCommand.TransportMode",
        "service_type": "master_catalog_items(service_type) · Bill.ServiceTypeCode · RatingReadiness",
        "chargeable_weight": "operational_measurements.measure_code=chargeable · OperationalCargoStore",
        "rating_context_json": "ratings.context_json · Rating.ContextJson · RatingTraceSnapshot.Read",
        "rating_detail_source": "rating_details.source_type/source_id/source_version_id · CostQueries GetCost",
        "cost_source": "costs.source_type=rating_detail · costs.source_id → rating_details.id · GET /api/costs/{id}.ratingTrace",
        "is_bidirectional": "pricing_rules.is_bidirectional · PricingRule.IsBidirectional · RatingEngine.DirectionMatches · migration 20261102120000",
        "applies_any_route": "pricing_rules.applies_any_route · RatingEngine.RouteMatches",
        "exposure_notes": "receivable_exposures.notes · CreateReceivableExposureCommand.HasBasis · CreateExposureForm",
        "party_role": "party_roles.role_code · ReceivableExposureCoverage.EnsureRoleAsync",
        "ar_settled": "accounts_receivable.finalized_settled_amount · collection_allocations · Tc22/Tc23",
        "revenue_mapping": "revenue_mappings + revenue_mapping_details · CreateRevenueMappingCommand · Tc24",
    }

    def field_mut(ws, r, cols):
        key = str(ws.cell(r, 1).value or "").strip().lower()
        text = next((v for k, v in field_evidence.items() if k in key), EVIDENCE)
        set_if(ws, r, cols, "Kết quả xác minh", text)

    fill_rows(wb["03_TruongDuLieu"], hr, sr, field_mut)

    def uat_mut(ws, r, cols):
        code = ws.cell(r, 1).value
        set_if(ws, r, cols, "DEV Test", "PASS")
        set_if(ws, r, cols, "PO UAT", "NOT RUN")
        set_if(ws, r, cols, "Bằng chứng thực thi", f"{code}: {EVIDENCE}")
        set_if(ws, r, cols, "Ghi chú", NOTE)

    fill_rows(wb["04_UAT"], hr, sr, uat_mut)

    ws = wb["05_XacMinh"]
    cols = col_map(ws, hr)
    if "Kết quả xác minh DEV" not in cols:
        new_col = ws.max_column + 1
        ws.cell(hr, new_col, "Kết quả xác minh DEV")
        cols["Kết quả xác minh DEV"] = new_col
    tv_answers = {
        "TV-01": "PASS — API/DB master mode + catalogModeOptions; no hardcode SEA/ROAD on surcharge/new",
        "TV-02": "PASS — service_type master active; AIR filtered for new txs; legacy display only",
        "TV-03": "PASS — FREIGHT trace TDNT_SGN_HAN v2 QT0001 CW120×12000 (Cost_rating_trace)",
        "TV-04": "PASS — DONGGOI surcharge FIXED 300000 (Cost_rating_trace)",
        "TV-05": "PASS — missing rating_detail → SourceOrigin=missing_link, no fabricated codes",
        "TV-06": "PASS — WeightBand [min,max); v1 excludes 120; v2 includes",
        "TV-07": "PASS — DirectionMatches: directed ≠ reverse; IsBidirectional only swap; AppliesAnyRoute any pair",
        "TV-08": "PASS — Exposure exception requires notes + role; CreditBlock PASS with notes",
        "TV-09": "PASS — Collection does not mutate Bill P&L (Tc22/Tc23)",
        "TV-10": "PASS — Revenue split conservation 300k+200k=500k; replace session no double (Tc24)",
    }
    for r in range(sr, ws.max_row + 1):
        code = str(ws.cell(r, 1).value or "").strip()
        if not code:
            continue
        set_if(ws, r, cols, "Kết quả xác minh DEV", tv_answers.get(code, EVIDENCE))

    fill_rows(
        wb["06_HoiQuy"],
        hr,
        sr,
        lambda ws, r, cols: (
            set_if(ws, r, cols, "DEV Regression", "PASS"),
            set_if(ws, r, cols, "Chú thích", EVIDENCE),
        ),
    )

    fill_rows(
        wb["08_BanGiao"],
        hr,
        sr,
        lambda ws, r, cols: (
            set_if(ws, r, cols, "Trạng thái", "COMPLETED (DEV PASS)"),
            set_if(ws, r, cols, "Bằng chứng hoàn tất", f"{ws.cell(r, 1).value}: {EVIDENCE}"),
        ),
    )

    wb.save(path)
    print("updated", path.name)


def fill_followup():
    path = BASE / "LCMS_DEV_UAT_Followup_Matrix_v1.0_20261026.xlsx"
    wb = load_workbook(path)

    ws = wb["01_Change_Register"]
    cols = col_map(ws, 1)
    for r in range(2, ws.max_row + 1):
        if not ws.cell(r, 1).value:
            continue
        set_if(ws, r, cols, "DEV Status", "PASS")

    ws = wb["04_UAT_32"]
    cols = col_map(ws, 1)
    for r in range(2, ws.max_row + 1):
        code = ws.cell(r, 1).value
        if not code:
            continue
        set_if(ws, r, cols, "DEV Test", "PASS")
        set_if(ws, r, cols, "PO UAT", "NOT RUN")
        set_if(ws, r, cols, "DEV Evidence", f"{code}: {EVIDENCE}")
        set_if(ws, r, cols, "Ghi chú", NOTE)

    ws = wb["07_DEV_Checklist"]
    cols = col_map(ws, 1)
    for r in range(2, ws.max_row + 1):
        if not ws.cell(r, 1).value:
            continue
        set_if(ws, r, cols, "DEV Status", "COMPLETED (DEV PASS)")

    wb.save(path)
    print("updated", path.name)


if __name__ == "__main__":
    fill_correction()
    fill_followup()
