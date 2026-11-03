#!/usr/bin/env python3
"""MG-01..MG-12 inventory/cleanup against local SQLite test DB."""
from __future__ import annotations

import hashlib
import json
import shutil
import sqlite3
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
DB = ROOT / "src" / "LCMS.Api" / "lcms_dev.db"
OUT = Path(__file__).resolve().parent


def main() -> None:
    if not DB.exists() or DB.stat().st_size == 0:
        raise SystemExit(f"Missing DB: {DB}")

    raw = DB.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    bak = OUT / f"lcms_dev_backup_{stamp}.db"
    shutil.copy2(DB, bak)
    manifest = {
        "file": str(DB.resolve()),
        "bytes": len(raw),
        "sha256": digest,
        "backup": str(bak.resolve()),
        "at": datetime.now(timezone.utc).isoformat(),
    }
    (OUT / "MG01_backup_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    conn = sqlite3.connect(DB)
    conn.row_factory = sqlite3.Row
    cur = conn.cursor()

    def scalar(sql: str):
        try:
            row = cur.execute(sql).fetchone()
            return row[0] if row else None
        except Exception as ex:  # noqa: BLE001
            return f"ERR:{ex}"

    before = {
        "ratings": scalar("SELECT COUNT(*) FROM ratings WHERE deleted_at IS NULL"),
        "rating_details": scalar("SELECT COUNT(*) FROM rating_details WHERE deleted_at IS NULL"),
        "costs_active": scalar(
            "SELECT COUNT(*) FROM costs WHERE deleted_at IS NULL AND record_status='active'"
        ),
        "revenues_active": scalar(
            "SELECT COUNT(*) FROM revenues WHERE deleted_at IS NULL AND record_status='active'"
        ),
        "costs_superseded": scalar("SELECT COUNT(*) FROM costs WHERE record_status='superseded'"),
        "bills": scalar("SELECT COUNT(*) FROM bills WHERE deleted_at IS NULL"),
        "financial_documents": scalar(
            "SELECT COUNT(*) FROM financial_documents WHERE deleted_at IS NULL"
        ),
        "accounts_payable": scalar("SELECT COUNT(*) FROM accounts_payable WHERE deleted_at IS NULL"),
        "accounts_receivable": scalar(
            "SELECT COUNT(*) FROM accounts_receivable WHERE deleted_at IS NULL"
        ),
    }

    cols = [r[1] for r in cur.execute("PRAGMA table_info(costs)")]
    if "economic_identity_key" not in cols:
        cur.executescript(
            """
            ALTER TABLE costs ADD COLUMN economic_identity_key TEXT NULL;
            ALTER TABLE costs ADD COLUMN service_occurrence_key TEXT NULL;
            ALTER TABLE costs ADD COLUMN applied_from_rating_id TEXT NULL;
            """
        )
        conn.commit()
        cols = [r[1] for r in cur.execute("PRAGMA table_info(costs)")]
    rev_cols = [r[1] for r in cur.execute("PRAGMA table_info(revenues)")]
    if "economic_identity_key" not in rev_cols:
        cur.executescript(
            """
            ALTER TABLE revenues ADD COLUMN economic_identity_key TEXT NULL;
            ALTER TABLE revenues ADD COLUMN service_occurrence_key TEXT NULL;
            ALTER TABLE revenues ADD COLUMN applied_from_rating_id TEXT NULL;
            """
        )
        conn.commit()

    # Best-effort backfill (MG-08)
    cur.execute(
        """
        UPDATE costs
        SET economic_identity_key =
              lower(replace(tenant_id,'-','')) || '|' ||
              lower(replace(coalesce(bill_id,'billnone'),'-','')) || '|BUY|' ||
              upper(coalesce(cost_type_code,'UNKNOWN')) || '|' ||
              CASE WHEN transport_leg_id IS NOT NULL THEN 'leg:' || lower(replace(transport_leg_id,'-',''))
                   WHEN bill_id IS NOT NULL THEN 'bill:' || lower(replace(bill_id,'-',''))
                   ELSE 'scope:none' END || '|' ||
              CASE WHEN vendor_party_id IS NOT NULL THEN lower(replace(vendor_party_id,'-','')) ELSE 'cp:none' END,
            service_occurrence_key =
              CASE WHEN transport_leg_id IS NOT NULL THEN 'leg:' || lower(replace(transport_leg_id,'-',''))
                   WHEN bill_id IS NOT NULL THEN 'bill:' || lower(replace(bill_id,'-',''))
                   ELSE 'scope:none' END
        WHERE economic_identity_key IS NULL OR economic_identity_key = ''
        """
    )
    cur.execute(
        """
        UPDATE revenues
        SET economic_identity_key =
              lower(replace(tenant_id,'-','')) || '|' ||
              lower(replace(bill_id,'-','')) || '|SELL|' ||
              upper(coalesce(revenue_type_code,'UNKNOWN')) || '|' ||
              'bill:' || lower(replace(bill_id,'-','')) || ':' || upper(coalesce(revenue_type_code,'UNKNOWN')) || '|' ||
              CASE WHEN customer_party_id IS NOT NULL THEN lower(replace(customer_party_id,'-','')) ELSE 'cp:none' END,
            service_occurrence_key =
              'bill:' || lower(replace(bill_id,'-','')) || ':' || upper(coalesce(revenue_type_code,'UNKNOWN'))
        WHERE economic_identity_key IS NULL OR economic_identity_key = ''
        """
    )
    conn.commit()

    has_eik = "economic_identity_key" in cols
    # Duplicate Expected-only (MG-05/09). Actual+Expected same key = maturity mix (keep).
    dup_sql = """
                SELECT economic_identity_key, COUNT(*)
                FROM costs
                WHERE record_status='active' AND deleted_at IS NULL
                  AND financial_maturity='expected'
                  AND economic_identity_key IS NOT NULL AND economic_identity_key <> ''
                GROUP BY economic_identity_key
                HAVING COUNT(*) > 1
                """
    maturity_mix_sql = """
                SELECT economic_identity_key, COUNT(*)
                FROM costs
                WHERE record_status='active' AND deleted_at IS NULL
                  AND economic_identity_key IS NOT NULL AND economic_identity_key <> ''
                GROUP BY economic_identity_key
                HAVING SUM(CASE WHEN financial_maturity='actual' THEN 1 ELSE 0 END) > 0
                   AND SUM(CASE WHEN financial_maturity='expected' THEN 1 ELSE 0 END) > 0
                """
    dup_before = []
    maturity_mix_before = []
    affected = 0
    if has_eik:
        dup_before = [{"key": r[0], "count": r[1]} for r in cur.execute(dup_sql)]
        maturity_mix_before = [
            {"key": r[0], "count": r[1]} for r in cur.execute(maturity_mix_sql)
        ]
        for row in dup_before:
            ids = [
                r[0]
                for r in cur.execute(
                    """
                    SELECT id FROM costs
                    WHERE economic_identity_key=? AND record_status='active' AND deleted_at IS NULL
                      AND financial_maturity='expected'
                    ORDER BY created_at DESC
                    """,
                    (row["key"],),
                )
            ]
            for old in ids[1:]:
                cur.execute(
                    "UPDATE costs SET record_status='superseded', updated_at=? WHERE id=?",
                    (datetime.now(timezone.utc).isoformat(), old),
                )
                affected += 1
        conn.commit()

    dup_after = []
    maturity_mix_after = []
    if has_eik:
        dup_after = [{"key": r[0], "count": r[1]} for r in cur.execute(dup_sql)]
        maturity_mix_after = [
            {"key": r[0], "count": r[1]} for r in cur.execute(maturity_mix_sql)
        ]

    after = {
        "costs_active": scalar(
            "SELECT COUNT(*) FROM costs WHERE deleted_at IS NULL AND record_status='active'"
        ),
        "costs_superseded": scalar("SELECT COUNT(*) FROM costs WHERE record_status='superseded'"),
        "revenues_active": scalar(
            "SELECT COUNT(*) FROM revenues WHERE deleted_at IS NULL AND record_status='active'"
        ),
    }

    sample_bills = [
        dict(r)
        for r in cur.execute(
            """
            SELECT id, bill_no FROM bills
            WHERE deleted_at IS NULL
              AND (bill_no LIKE '%LSSTEST%' OR bill_no LIKE '%TESTLSS%' OR bill_no LIKE '%TEST%')
            LIMIT 30
            """
        )
    ]
    sample_costs = [
        dict(r)
        for r in cur.execute(
            """
            SELECT b.bill_no, c.cost_type_code, c.financial_maturity, c.record_status,
                   c.expected_amount, c.actual_amount,
                   COALESCE(c.economic_identity_key,'') AS economic_identity_key
            FROM costs c
            JOIN bills b ON b.id = c.bill_id
            WHERE c.deleted_at IS NULL
            ORDER BY b.bill_no, c.created_at
            LIMIT 50
            """
        )
    ]

    report = {
        "MG01": manifest,
        "MG02_03_04_before": before,
        "MG05_dup_groups_before": dup_before,
        "MG05_maturity_mix_before": maturity_mix_before,
        "MG09_affected_supersede_rows": affected,
        "MG12_after": after,
        "MG12_dup_groups_after": dup_after,
        "MG12_maturity_mix_after": maturity_mix_after,
        "has_economic_identity_key": has_eik,
        "sample_bills": sample_bills,
        "sample_costs": sample_costs,
    }
    (OUT / "MG_inventory_result.json").write_text(
        json.dumps(report, indent=2, default=str), encoding="utf-8"
    )
    print(json.dumps({
        "sha256": digest,
        "affected": affected,
        "has_eik": has_eik,
        "before_costs_active": before["costs_active"],
        "after_costs_active": after["costs_active"],
        "dup_before": len(dup_before),
        "dup_after": len(dup_after),
        "sample_bills": len(sample_bills),
    }, indent=2))
    conn.close()


if __name__ == "__main__":
    main()
