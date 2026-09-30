import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";
import {
  AttachmentDto,
  AttachmentObjectType,
  createIdempotencyKey,
  parseApiError,
  ParsedApiError,
} from "@lcms/shared";

const STORAGE_KEYS = {
  ACCESS_TOKEN: "lcms.auth.access_token",
  REFRESH_TOKEN: "lcms.auth.refresh_token",
  TENANT_ID: "lcms.auth.tenant_id",
  USER_ID: "lcms.auth.user_id",
  USER_EMAIL: "lcms.auth.user_email",
  USER_DISPLAY_NAME: "lcms.auth.user_display_name",
  API_BASE_URL: "lcms.config.api_base_url",
};

const memoryStore: Record<string, string> = {};

async function getSecureItem(key: string): Promise<string | null> {
  try {
    if (Platform.OS === "web") {
      return memoryStore[key] ?? null;
    }
    return await SecureStore.getItemAsync(key);
  } catch {
    return memoryStore[key] ?? null;
  }
}

async function setSecureItem(key: string, value: string | null): Promise<void> {
  try {
    if (value === null) {
      delete memoryStore[key];
      if (Platform.OS !== "web") {
        await SecureStore.deleteItemAsync(key);
      }
    } else {
      memoryStore[key] = value;
      if (Platform.OS !== "web") {
        await SecureStore.setItemAsync(key, value);
      }
    }
  } catch {
    if (value === null) {
      delete memoryStore[key];
    } else {
      memoryStore[key] = value;
    }
  }
}

export interface AuthSessionTokens {
  accessToken: string;
  refreshToken?: string | null;
  tenantId: string;
  userId?: string | null;
  userEmail?: string | null;
  userDisplayName?: string | null;
}

export async function saveSessionTokens(tokens: AuthSessionTokens): Promise<void> {
  await setSecureItem(STORAGE_KEYS.ACCESS_TOKEN, tokens.accessToken);
  await setSecureItem(STORAGE_KEYS.REFRESH_TOKEN, tokens.refreshToken ?? null);
  await setSecureItem(STORAGE_KEYS.TENANT_ID, tokens.tenantId);
  if (tokens.userId !== undefined) {
    await setSecureItem(STORAGE_KEYS.USER_ID, tokens.userId ?? null);
  }
  if (tokens.userEmail !== undefined) {
    await setSecureItem(STORAGE_KEYS.USER_EMAIL, tokens.userEmail ?? null);
  }
  if (tokens.userDisplayName !== undefined) {
    await setSecureItem(STORAGE_KEYS.USER_DISPLAY_NAME, tokens.userDisplayName ?? null);
  }
}

export async function clearSessionTokens(): Promise<void> {
  await setSecureItem(STORAGE_KEYS.ACCESS_TOKEN, null);
  await setSecureItem(STORAGE_KEYS.REFRESH_TOKEN, null);
  await setSecureItem(STORAGE_KEYS.TENANT_ID, null);
  await setSecureItem(STORAGE_KEYS.USER_ID, null);
  await setSecureItem(STORAGE_KEYS.USER_EMAIL, null);
  await setSecureItem(STORAGE_KEYS.USER_DISPLAY_NAME, null);
}

export async function getStoredSessionTokens(): Promise<AuthSessionTokens | null> {
  const [accessToken, refreshToken, tenantId, userId, userEmail, userDisplayName] =
    await Promise.all([
      getSecureItem(STORAGE_KEYS.ACCESS_TOKEN),
      getSecureItem(STORAGE_KEYS.REFRESH_TOKEN),
      getSecureItem(STORAGE_KEYS.TENANT_ID),
      getSecureItem(STORAGE_KEYS.USER_ID),
      getSecureItem(STORAGE_KEYS.USER_EMAIL),
      getSecureItem(STORAGE_KEYS.USER_DISPLAY_NAME),
    ]);
  if (!accessToken || !tenantId) {
    return null;
  }
  return { accessToken, refreshToken, tenantId, userId, userEmail, userDisplayName };
}

export async function getApiBaseUrl(): Promise<string> {
  const custom = await getSecureItem(STORAGE_KEYS.API_BASE_URL);
  if (custom) {
    return custom.replace(/\/+$/, "");
  }
  const envUrl = process.env.EXPO_PUBLIC_API_URL;
  if (envUrl) {
    return envUrl.replace(/\/+$/, "");
  }
  return "http://194.233.89.26";
}

export async function setApiBaseUrl(url: string): Promise<void> {
  await setSecureItem(STORAGE_KEYS.API_BASE_URL, url.trim().replace(/\/+$/, ""));
}

export interface ApiRequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  ifMatch?: string | null;
  idempotencyKey?: string | null;
  headers?: Record<string, string>;
  skipAuthRefresh?: boolean;
}

export class LcmsApiException extends Error {
  public readonly parsed: ParsedApiError;

  constructor(parsed: ParsedApiError) {
    super(parsed.messageVi);
    this.name = "LcmsApiException";
    this.parsed = parsed;
  }
}

let refreshPromise: Promise<boolean> | null = null;

async function tryRefreshAccessToken(baseUrl: string): Promise<boolean> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    try {
      const refreshToken = await getSecureItem(STORAGE_KEYS.REFRESH_TOKEN);
      const tenantId = await getSecureItem(STORAGE_KEYS.TENANT_ID);
      if (!refreshToken || !tenantId) {
        return false;
      }

      const res = await fetch(`${baseUrl}/api/auth/refresh`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Tenant-Id": tenantId,
        },
        body: JSON.stringify({ refreshToken }),
      });

      if (!res.ok) {
        await clearSessionTokens();
        return false;
      }

      const data = (await res.json()) as {
        accessToken: string;
        refreshToken?: string;
        tenantId?: string;
      };

      await saveSessionTokens({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken ?? refreshToken,
        tenantId: data.tenantId ?? tenantId,
      });
      return true;
    } catch {
      return false;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const baseUrl = await getApiBaseUrl();
  const session = await getStoredSessionTokens();
  const method = options.method ?? "GET";

  const buildHeaders = (accessToken?: string | null): Record<string, string> => {
    const headers: Record<string, string> = {
      Accept: "application/json",
      ...(options.headers ?? {}),
    };

    if (options.body !== undefined) {
      headers["Content-Type"] = "application/json";
    }
    if (accessToken) {
      headers["Authorization"] = `Bearer ${accessToken}`;
    }
    if (session?.tenantId) {
      headers["X-Tenant-Id"] = session.tenantId;
    }
    if (options.ifMatch) {
      headers["If-Match"] = options.ifMatch;
    }
    if (options.idempotencyKey) {
      headers["Idempotency-Key"] = options.idempotencyKey;
    }
    return headers;
  };

  let res = await fetch(`${baseUrl}${path}`, {
    method,
    headers: buildHeaders(session?.accessToken),
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  if (res.status === 401 && !options.skipAuthRefresh && session?.refreshToken) {
    const refreshed = await tryRefreshAccessToken(baseUrl);
    if (refreshed) {
      const nextSession = await getStoredSessionTokens();
      res = await fetch(`${baseUrl}${path}`, {
        method,
        headers: buildHeaders(nextSession?.accessToken),
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      });
    }
  }

  if (res.status === 204) {
    return undefined as T;
  }

  const text = await res.text();
  let json: unknown = null;
  if (text) {
    try {
      json = JSON.parse(text);
    } catch {
      json = { message: text };
    }
  }

  if (!res.ok) {
    throw new LcmsApiException(parseApiError(res.status, json));
  }

  return json as T;
}

export async function uploadAttachmentBase64(params: {
  objectType: AttachmentObjectType;
  objectId: string;
  fileName: string;
  contentType: string;
  base64: string;
  notes?: string;
}): Promise<AttachmentDto> {
  return apiRequest<AttachmentDto>("/api/attachments", {
    method: "POST",
    idempotencyKey: createIdempotencyKey("att"),
    body: params,
  });
}

