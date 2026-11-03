#!/usr/bin/env python3
"""Seed Bill LSSTEST02112026002 duplicate Cost/Revenue pattern for MG-10/11 reconciliation."""
from __future__ import annotations

import sqlite3
import uuid
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
DB = ROOT / "src" / "LCMS.Api" / "lcms_dev.db"


def uid() -> str:
    return str(uuid.uuid4()).upper()


def main() -> None:
    conn = sqlite3.connect(DB)
    cur = conn.cursor()
    tenant = cur.execute("SELECT id FROM tenants LIMIT 1").fetchone()
    if not tenant:
        raise SystemExit("no tenant")
    tenant_id = tenant[0]
    now = datetime.now(timezone.utc).isoformat()
    bill_no = "LSSTEST02112026002"
    existing = cur.execute("SELECT id FROM bills WHERE bill_no=?", (bill_no,)).fetchone()
    if existing:
        bill_id = existing[0]
        cur.execute("DELETE FROM costs WHERE bill_id=?", (bill_id,))
        cur.execute("DELETE FROM revenues WHERE bill_id=?", (bill_id,))
    else:
        bill_id = uid()
        sample = cur.execute(
            "SELECT organization_id, customer_party_id, bill_type, operational_status, is_active FROM bills LIMIT 1"
        ).fetchone()
        org, cust, btype, op, active = sample
        cur.execute(
            """
            INSERT INTO bills (
              id, tenant_id, bill_no, organization_id, customer_party_id,
              bill_type, operational_status, is_active, created_at, row_version
            ) VALUES (?,?,?,?,?,?,?,?,?,?)
            """,
            (
                bill_id,
                tenant_id,
                bill_no,
                org,
                cust,
                btype or "standard",
                op or "open",
                1 if active is None else active,
                now,
                uuid.uuid4().bytes,
            ),
        )

    def add_cost(code, maturity, expected, actual, status, occ):
        cid = uid()
        eik = (
            f"{tenant_id.replace('-', '').lower()}|{bill_id.replace('-', '').lower()}"
            f"|BUY|{code}|{occ}|cp:none"
        )
        cur.execute(
            """
            INSERT INTO costs (
              id, tenant_id, bill_id, cost_type_code, financial_maturity, attribution_type,
              expected_amount, confirmed_amount, actual_amount, amount, currency_code,
              source_type, record_status, approval_status, effective_date, created_at, row_version,
              economic_identity_key, service_occurrence_key, fx_status, partner_override_requires_rerate
            ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
            """,
            (
                cid,
                tenant_id,
                bill_id,
                code,
                maturity,
                "direct",
                expected,
                None,
                actual,
                actual if actual is not None else expected,
                "VND",
                "rating_detail",
                status,
                "not_required",
                now[:10],
                now,
                uuid.uuid4().bytes,
                eik,
                occ,
                "missing",
                0,
            ),
        )

    def add_rev(code, maturity, expected, actual, occ):
        rid = uid()
        eik = (
            f"{tenant_id.replace('-', '').lower()}|{bill_id.replace('-', '').lower()}"
            f"|SELL|{code}|{occ}|cp:none"
        )
        cur.execute(
            """
            INSERT INTO revenues (
              id, tenant_id, bill_id, revenue_type_code, financial_maturity,
              expected_amount, confirmed_amount, actual_amount, amount, currency_code,
              source_type, record_status, approval_status, effective_date, created_at, row_version,
              economic_identity_key, service_occurrence_key, fx_status, partner_override_requires_rerate
            ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
            """,
            (
                rid,
                tenant_id,
                bill_id,
                code,
                maturity,
                expected,
                None,
                actual,
                actual if actual is not None else expected,
                "VND",
                "rating_detail",
                "active",
                "not_required",
                now[:10],
                now,
                uuid.uuid4().bytes,
                eik,
                occ,
                "missing",
                0,
            ),
        )

    freight_occ = f"bill:{bill_id.replace('-', '').lower()}"
    dg_occ = f"{freight_occ}:donggoi"
    add_cost("FREIGHT", "actual", 1440000, 1440000, "active", freight_occ)
    add_cost("FREIGHT", "expected", 1440000, None, "active", freight_occ)
    add_cost("DONGGOI_SGN_HAN_MUA", "expected", 300000, None, "active", dg_occ)
    add_cost("DONGGOI_SGN_HAN_MUA", "expected", 300000, None, "active", dg_occ)
    add_rev("FREIGHT", "actual", 2040000, 2040000, f"{freight_occ}:FREIGHT")
    add_rev("DONGGOI_SGN_HAN", "expected", 400000, None, f"{freight_occ}:DONGGOI")
    conn.commit()

    rows = cur.execute(
        """
        SELECT id FROM costs
        WHERE bill_id=? AND cost_type_code='DONGGOI_SGN_HAN_MUA' AND record_status='active'
          AND actual_amount IS NULL
        ORDER BY created_at DESC
        """,
        (bill_id,),
    ).fetchall()
    affected = 0
    for old in rows[1:]:
        cur.execute("UPDATE costs SET record_status='superseded' WHERE id=?", (old[0],))
        affected += 1
    conn.commit()

    # Effective math check via same keys
    costs = cur.execute(
        """
        SELECT cost_type_code, financial_maturity, record_status, expected_amount, actual_amount, economic_identity_key
        FROM costs WHERE bill_id=? ORDER BY cost_type_code, created_at
        """,
        (bill_id,),
    ).fetchall()
    print({"bill_id": bill_id, "donggoi_superseded": affected, "cost_rows": len(costs)})
    for r in costs:
        print(r)
    conn.close()


if __name__ == "__main__":
    main()
