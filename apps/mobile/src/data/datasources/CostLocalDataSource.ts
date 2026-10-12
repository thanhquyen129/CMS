import * as SQLite from "expo-sqlite";
import { Platform } from "react-native";
import { CostItemDto } from "@lcms/shared";

let dbInstance: SQLite.SQLiteDatabase | null = null;
let memoryCache: CostItemDto[] = [];

export class CostLocalDataSource {
  private async getDb(): Promise<SQLite.SQLiteDatabase | null> {
    if (Platform.OS === "web") {
      return null;
    }
    if (dbInstance) {
      return dbInstance;
    }
    try {
      dbInstance = await SQLite.openDatabaseAsync("lcms_mobile_offline.db");
      await dbInstance.execAsync(`
        CREATE TABLE IF NOT EXISTS cached_costs (
          id TEXT PRIMARY KEY NOT NULL,
          bill_id TEXT,
          bill_no TEXT,
          attribution_type TEXT NOT NULL,
          financial_maturity TEXT NOT NULL,
          expected_amount REAL NOT NULL,
          confirmed_amount REAL,
          actual_amount REAL,
          amount REAL NOT NULL,
          currency_code TEXT NOT NULL,
          reporting_currency_code TEXT,
          base_amount REAL,
          fx_rate REAL,
          fx_status TEXT NOT NULL,
          cost_type_code TEXT,
          vendor_party_id TEXT,
          vendor_name TEXT,
          approval_status TEXT NOT NULL,
          effective_date TEXT NOT NULL,
          row_version TEXT NOT NULL,
          cached_at TEXT NOT NULL
        );
      `);
      return dbInstance;
    } catch {
      return null;
    }
  }

  /**
   * Lấy danh sách chi phí được đệm trong SQLite khi offline.
   */
  public async getCachedCosts(): Promise<CostItemDto[]> {
    const db = await this.getDb();
    if (!db) {
      return [...memoryCache];
    }

    try {
      const rows = await db.getAllAsync<{
        id: string;
        bill_id: string | null;
        bill_no: string | null;
        attribution_type: "direct" | "shared";
        financial_maturity: "expected" | "confirmed" | "actual";
        expected_amount: number;
        confirmed_amount: number | null;
        actual_amount: number | null;
        amount: number;
        currency_code: string;
        reporting_currency_code: string | null;
        base_amount: number | null;
        fx_rate: number | null;
        fx_status: string;
        cost_type_code: string | null;
        vendor_party_id: string | null;
        vendor_name: string | null;
        approval_status: string;
        effective_date: string;
        row_version: string;
      }>("SELECT * FROM cached_costs ORDER BY effective_date DESC;");

      return rows.map((r) => ({
        id: r.id,
        billId: r.bill_id,
        billNo: r.bill_no,
        attributionType: r.attribution_type,
        financialMaturity: r.financial_maturity,
        expectedAmount: r.expected_amount,
        confirmedAmount: r.confirmed_amount,
        actualAmount: r.actual_amount,
        amount: r.amount,
        currencyCode: r.currency_code,
        reportingCurrencyCode: r.reporting_currency_code,
        baseAmount: r.base_amount,
        fxRate: r.fx_rate,
        fxStatus: r.fx_status,
        costTypeCode: r.cost_type_code,
        vendorPartyId: r.vendor_party_id,
        vendorName: r.vendor_name,
        approvalStatus: r.approval_status,
        effectiveDate: r.effective_date,
        rowVersion: r.row_version,
      }));
    } catch {
      return [...memoryCache];
    }
  }

  /**
   * Đồng bộ lưu cache chi phí vào SQLite (nghiêm cấm lưu Base64 - No Blob Bloat).
   */
  public async saveCachedCosts(items: CostItemDto[]): Promise<void> {
    memoryCache = [...items];
    const db = await this.getDb();
    if (!db) return;

    try {
      await db.withTransactionAsync(async () => {
        await db.runAsync("DELETE FROM cached_costs;");
        const now = new Date().toISOString();
        for (const item of items) {
          await db.runAsync(
            `INSERT OR REPLACE INTO cached_costs (
              id, bill_id, bill_no, attribution_type, financial_maturity,
              expected_amount, confirmed_amount, actual_amount, amount,
              currency_code, reporting_currency_code, base_amount, fx_rate,
              fx_status, cost_type_code, vendor_party_id, vendor_name,
              approval_status, effective_date, row_version, cached_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
            item.id,
            item.billId ?? null,
            item.billNo ?? null,
            item.attributionType,
            item.financialMaturity,
            item.expectedAmount,
            item.confirmedAmount ?? null,
            item.actualAmount ?? null,
            item.amount,
            item.currencyCode,
            item.reportingCurrencyCode ?? null,
            item.baseAmount ?? null,
            item.fxRate ?? null,
            item.fxStatus,
            item.costTypeCode ?? null,
            item.vendorPartyId ?? null,
            item.vendorName ?? null,
            item.approvalStatus,
            item.effectiveDate,
            item.rowVersion,
            now
          );
        }
      });
    } catch {
      // Bỏ qua lỗi cache để không chặn luồng hiển thị chính
    }
  }
}
