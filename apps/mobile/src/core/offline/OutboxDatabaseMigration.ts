import * as SQLite from "expo-sqlite";

export const CURRENT_DATABASE_VERSION = 2;

interface ColumnInfo {
  cid: number;
  name: string;
  type: string;
  notnull: number;
  dflt_value: any;
  pk: number;
}

/**
 * Thực hiện migration cơ sở dữ liệu SQLite Outbox từ bản thử nghiệm (V1)
 * lên bản chuẩn thương mại (V2).
 * Đảm bảo:
 * - Khắc phục Blob Bloat: Bổ sung các cột local_file_uri, file_content_type, file_name
 * - Quản lý Replay Backoff: Bổ sung next_retry_at, last_attempt_at
 * - Tối ưu hiệu năng: Thêm Index cho truy vấn Replay và Idempotency
 */
export async function runOutboxDatabaseMigrations(db: SQLite.SQLiteDatabase): Promise<void> {
  // 1. Tạo bảng cơ sở nếu chưa tồn tại
  await db.execAsync(`
    CREATE TABLE IF NOT EXISTS offline_outbox (
      id TEXT PRIMARY KEY NOT NULL,
      title_vi TEXT NOT NULL,
      endpoint TEXT NOT NULL,
      method TEXT NOT NULL,
      payload_json TEXT NOT NULL,
      if_match TEXT,
      idempotency_key TEXT NOT NULL,
      status TEXT NOT NULL,
      error_message_vi TEXT,
      retry_count INTEGER NOT NULL DEFAULT 0,
      created_at TEXT NOT NULL,
      updated_at TEXT NOT NULL
    );
  `);

  // 2. Kiểm tra các cột hiện có để tiến hành migration linh hoạt (Dynamic Column Migration)
  const columns = await db.getAllAsync<ColumnInfo>("PRAGMA table_info(offline_outbox);");
  const columnNames = new Set(columns.map((c) => c.name.toLowerCase()));

  // Migration V2: Khắc phục Blob Bloat & hỗ trợ Backoff Replay
  if (!columnNames.has("local_file_uri")) {
    await db.execAsync("ALTER TABLE offline_outbox ADD COLUMN local_file_uri TEXT;");
  }
  if (!columnNames.has("file_content_type")) {
    await db.execAsync("ALTER TABLE offline_outbox ADD COLUMN file_content_type TEXT;");
  }
  if (!columnNames.has("file_name")) {
    await db.execAsync("ALTER TABLE offline_outbox ADD COLUMN file_name TEXT;");
  }
  if (!columnNames.has("next_retry_at")) {
    await db.execAsync("ALTER TABLE offline_outbox ADD COLUMN next_retry_at TEXT;");
  }
  if (!columnNames.has("last_attempt_at")) {
    await db.execAsync("ALTER TABLE offline_outbox ADD COLUMN last_attempt_at TEXT;");
  }

  // 3. Tạo các chỉ mục tăng tốc độ truy vấn
  await db.execAsync(`
    CREATE INDEX IF NOT EXISTS idx_outbox_status_retry 
      ON offline_outbox (status, next_retry_at);

    CREATE INDEX IF NOT EXISTS idx_outbox_idempotency 
      ON offline_outbox (idempotency_key);
  `);

  // 4. Lưu version vào PRAGMA user_version
  await db.execAsync(`PRAGMA user_version = ${CURRENT_DATABASE_VERSION};`);
}
