#!/usr/bin/env python3
"""Verify LSSTEST02112026002 Effective Cost/Revenue/Profit via selector logic."""
from __future__ import annotations

import json
import sqlite3
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
DB = ROOT / "src" / "LCMS.Api" / "lcms_dev.db"
OUT = Path(__file__).resolve().parent / "LSSTEST_effective_verify.json"


def best_available(rows):
    """Actual > Confirmed > Expected; partial Actual + remaining Expected."""
    by_key = defaultdict(list)
    for r in rows:
        by_key[r["eik"]].append(r)
    total = 0.0
    for eik, group in by_key.items():
        active = [x for x in group if x["status"] == "active"]
        actuals = [x for x in active if x["maturity"] == "actual" and x["actual"] is not None]
        confirmed = [x for x in active if x["maturity"] == "confirmed" and x["confirmed"] is not None]
        expected = [x for x in active if x["maturity"] == "expected" and x["expected"] is not None]
        if actuals:
            a = max(float(x["actual"]) for x in actuals)
            rem = 0.0
            if expected:
                e = max(float(x["expected"]) for x in expected)
                if e > a:
                    rem = e - a
            total += a + rem
        elif confirmed:
            total += max(float(x["confirmed"]) for x in confirmed)
        elif expected:
            total += max(float(x["expected"]) for x in expected)
    return total


def main() -> None:
    conn = sqlite3.connect(DB)
    cur = conn.cursor()
    bill = cur.execute(
        "SELECT id FROM bills WHERE bill_no=?", ("LSSTEST02112026002",)
    ).fetchone()
    if not bill:
        raise SystemExit("bill missing")
    bill_id = bill[0]
    costs = [
        {
            "code": r[0],
            "maturity": r[1],
            "status": r[2],
            "expected": r[3],
            "confirmed": r[4],
            "actual": r[5],
            "eik": r[6],
        }
        for r in cur.execute(
            """
            SELECT cost_type_code, financial_maturity, record_status,
                   expected_amount, confirmed_amount, actual_amount, economic_identity_key
            FROM costs WHERE bill_id=?
            """,
            (bill_id,),
        )
    ]
    revs = [
        {
            "code": r[0],
            "maturity": r[1],
            "status": r[2],
            "expected": r[3],
            "confirmed": r[4],
            "actual": r[5],
            "eik": r[6],
        }
        for r in cur.execute(
            """
            SELECT revenue_type_code, financial_maturity, record_status,
                   expected_amount, confirmed_amount, actual_amount, economic_identity_key
            FROM revenues WHERE bill_id=?
            """,
            (bill_id,),
        )
    ]
    cost_eff = best_available(costs)
    rev_eff = best_available(revs)
    profit = rev_eff - cost_eff
    result = {
        "bill_no": "LSSTEST02112026002",
        "bill_id": bill_id,
        "cost_rows": costs,
        "revenue_rows": revs,
        "effective_cost": cost_eff,
        "effective_revenue": rev_eff,
        "best_available_profit": profit,
        "assert_cost_1740000": cost_eff == 1740000,
        "assert_rev_2440000": rev_eff == 2440000,
        "assert_profit_700000": profit == 700000,
    }
    OUT.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({k: result[k] for k in (
        "effective_cost", "effective_revenue", "best_available_profit",
        "assert_cost_1740000", "assert_rev_2440000", "assert_profit_700000",
        "cost_rows", "revenue_rows",
    )}, indent=2, default=str))
    conn.close()
    if not (result["assert_cost_1740000"] and result["assert_rev_2440000"] and result["assert_profit_700000"]):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
