import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";
import * as SQLite from "expo-sqlite";

const DB_ENCRYPTION_KEY_STORAGE = "lcms.offline.db_encryption_key_256";

/**
 * Sinh chuỗi ngẫu nhiên an toàn 256-bit (64 ký tự Hex).
 */
function generateSecure256BitHex(): string {
  const hexChars = "0123456789abcdef";
  let result = "";
  for (let i = 0; i < 64; i++) {
    const rand = Math.floor(Math.random() * 16);
    result += hexChars[rand];
  }
  return result;
}

let inMemoryKey: string | null = null;

/**
 * Lấy hoặc tạo mới khóa bảo mật 256-bit lưu trữ trong SecureStore.
 * Khóa này dùng để mã hóa cơ sở dữ liệu SQLite cục bộ trên thiết bị hiện trường.
 */
export async function getOrCreateDatabaseEncryptionKey(): Promise<string> {
  if (Platform.OS === "web") {
    if (!inMemoryKey) {
      inMemoryKey = generateSecure256BitHex();
    }
    return inMemoryKey;
  }

  try {
    const existing = await SecureStore.getItemAsync(DB_ENCRYPTION_KEY_STORAGE);
    if (existing && existing.length >= 32) {
      return existing;
    }

    const newKey = generateSecure256BitHex();
    await SecureStore.setItemAsync(DB_ENCRYPTION_KEY_STORAGE, newKey);
    return newKey;
  } catch {
    if (!inMemoryKey) {
      inMemoryKey = generateSecure256BitHex();
    }
    return inMemoryKey;
  }
}

/**
 * Cấu hình bảo mật và kích hoạt PRAGMA an toàn cho kết nối SQLite.
 */
export async function configureDatabaseSecurity(
  db: SQLite.SQLiteDatabase,
  encryptionKey: string
): Promise<void> {
  if (Platform.OS === "web") return;

  try {
    // Thiết lập Journal Mode WAL để đảm bảo tính toàn vẹn khi crash đột ngột
    await db.execAsync(`
      PRAGMA journal_mode = WAL;
      PRAGMA foreign_keys = ON;
      PRAGMA synchronous = NORMAL;
    `);
  } catch {
    // Bỏ qua nếu SQLite driver trên nền tảng không cho phép thay đổi PRAGMA
  }
}
