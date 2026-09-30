import React, { createContext, useCallback, useContext, useEffect, useState } from "react";
import { Platform } from "react-native";
import Constants, { ExecutionEnvironment } from "expo-constants";
import {
  DEFAULT_TERMINOLOGY_VI,
  MobileBadgesDto,
  MobileBootstrapDto,
  MobileModuleDto,
  MobilePersona,
  MobileTabDto,
  term,
  TerminologyMap,
} from "@lcms/shared";
import {
  apiRequest,
  clearSessionTokens,
  getStoredSessionTokens,
  LcmsApiException,
  saveSessionTokens,
} from "../api/client";

interface AuthContextValue {
  isLoading: boolean;
  isAuthenticated: boolean;
  bootstrap: MobileBootstrapDto | null;
  pushToken: string | null;
  terminology: TerminologyMap;
  t: (key: string, fallback?: string) => string;
  loginWithCredentials: (params: {
    tenantCodeOrId: string;
    email: string;
    password: string;
  }) => Promise<void>;
  refreshBootstrap: () => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

const isExpoGo =
  Constants.executionEnvironment === ExecutionEnvironment.StoreClient ||
  Constants.appOwnership === "expo";

function resolveFallbackRoleAndPersona(emailRaw: string): {
  roleCode: string;
  persona: MobilePersona;
  canViewCost: boolean;
  canViewRevenue: boolean;
  canViewMargin: boolean;
} {
  const email = emailRaw.trim().toLowerCase();
  if (email.startsWith("controller@")) {
    return {
      roleCode: "FinancialController",
      persona: "executive_control",
      canViewCost: true,
      canViewRevenue: true,
      canViewMargin: true,
    };
  }
  if (email.startsWith("cost@")) {
    return {
      roleCode: "CostAccountant",
      persona: "cost_accountant",
      canViewCost: true,
      canViewRevenue: false,
      canViewMargin: false,
    };
  }
  if (email.startsWith("revenue@")) {
    return {
      roleCode: "RevenueAccountant",
      persona: "revenue_accountant",
      canViewCost: false,
      canViewRevenue: true,
      canViewMargin: false,
    };
  }
  if (email.startsWith("ops")) {
    return {
      roleCode: "Ops",
      persona: "field_ops",
      canViewCost: true,
      canViewRevenue: false,
      canViewMargin: false,
    };
  }
  if (email.startsWith("master@")) {
    return {
      roleCode: "MasterData",
      persona: "master_data",
      canViewCost: false,
      canViewRevenue: false,
      canViewMargin: false,
    };
  }
  if (email.startsWith("viewer@")) {
    return {
      roleCode: "Viewer",
      persona: "viewer",
      canViewCost: false,
      canViewRevenue: false,
      canViewMargin: false,
    };
  }
  return {
    roleCode: "Admin",
    persona: "executive_control",
    canViewCost: true,
    canViewRevenue: true,
    canViewMargin: true,
  };
}

function buildFallbackBottomTabs(
  persona: MobilePersona,
  badges: MobileBadgesDto
): MobileTabDto[] {
  switch (persona) {
    case "executive_control":
      return [
        { key: "dashboard", labelVi: "Tổng quan", icon: "bar-chart-2", route: "/(tabs)/dashboard", badgeCount: 0 },
        { key: "approvals", labelVi: "Phê duyệt", icon: "check-circle", route: "/(tabs)/approvals", badgeCount: badges.pendingApprovals },
        { key: "control", labelVi: "Kiểm soát", icon: "shield-alert", route: "/(tabs)/control", badgeCount: badges.openExceptions + badges.openVariances },
        { key: "bills", labelVi: "Bill & Công nợ", icon: "file-text", route: "/(tabs)/bills", badgeCount: 0 },
        { key: "modules", labelVi: "Phân hệ", icon: "grid", route: "/(tabs)/modules", badgeCount: badges.unreadNotifications },
      ];
    case "cost_accountant":
      return [
        { key: "dashboard", labelVi: "Tổng quan CP", icon: "pie-chart", route: "/(tabs)/dashboard", badgeCount: 0 },
        { key: "costs", labelVi: "Chi phí", icon: "trending-down", route: "/(tabs)/costs", badgeCount: 0 },
        { key: "ap", labelVi: "Công nợ AP", icon: "credit-card", route: "/(tabs)/ap", badgeCount: 0 },
        { key: "documents", labelVi: "Chứng từ & Chi", icon: "file-check", route: "/(tabs)/documents", badgeCount: 0 },
        { key: "modules", labelVi: "Phân hệ", icon: "grid", route: "/(tabs)/modules", badgeCount: badges.unreadNotifications },
      ];
    case "revenue_accountant":
      return [
        { key: "dashboard", labelVi: "Tổng quan DT", icon: "trending-up", route: "/(tabs)/dashboard", badgeCount: 0 },
        { key: "revenues", labelVi: "Doanh thu", icon: "dollar-sign", route: "/(tabs)/revenues", badgeCount: 0 },
        { key: "ar", labelVi: "Công nợ AR", icon: "wallet", route: "/(tabs)/ar", badgeCount: 0 },
        { key: "documents", labelVi: "Chứng từ & Thu", icon: "file-check", route: "/(tabs)/documents", badgeCount: 0 },
        { key: "modules", labelVi: "Phân hệ", icon: "grid", route: "/(tabs)/modules", badgeCount: badges.unreadNotifications },
      ];
    case "field_ops":
      return [
        { key: "bills", labelVi: "Vận hành", icon: "truck", route: "/(tabs)/bills", badgeCount: 0 },
        { key: "scanner", labelVi: "Quét & Chụp", icon: "camera", route: "/(tabs)/scanner", badgeCount: 0 },
        { key: "costs", labelVi: "CP Dự kiến", icon: "plus-circle", route: "/(tabs)/costs", badgeCount: 0 },
        { key: "offline", labelVi: "Đồng bộ", icon: "cloud-upload", route: "/(tabs)/offline", badgeCount: 0 },
        { key: "modules", labelVi: "Phân hệ", icon: "grid", route: "/(tabs)/modules", badgeCount: badges.unreadNotifications },
      ];
    case "master_data":
      return [
        { key: "bills", labelVi: "Tra cứu Bill", icon: "search", route: "/(tabs)/bills", badgeCount: 0 },
        { key: "scanner", labelVi: "Quét mã", icon: "camera", route: "/(tabs)/scanner", badgeCount: 0 },
        { key: "offline", labelVi: "Đồng bộ", icon: "cloud-upload", route: "/(tabs)/offline", badgeCount: 0 },
        { key: "modules", labelVi: "Phân hệ", icon: "grid", route: "/(tabs)/modules", badgeCount: badges.unreadNotifications },
      ];
    default:
      return [
        { key: "bills", labelVi: "Danh sách Bill", icon: "file-text", route: "/(tabs)/bills", badgeCount: 0 },
        { key: "scanner", labelVi: "Quét mã", icon: "camera", route: "/(tabs)/scanner", badgeCount: 0 },
        { key: "modules", labelVi: "Phân hệ", icon: "grid", route: "/(tabs)/modules", badgeCount: 0 },
      ];
  }
}

function buildFallbackModules(params: {
  canViewCost: boolean;
  canViewRevenue: boolean;
  canViewMargin: boolean;
  badges: MobileBadgesDto;
}): MobileModuleDto[] {
  const { canViewCost, canViewRevenue, canViewMargin, badges } = params;
  const list: MobileModuleDto[] = [
    {
      code: "dashboard",
      titleVi: "Trang chủ & KPI",
      subtitleVi: "Tổng quan số liệu theo quyền hạn",
      icon: "bar-chart-2",
      route: "/(tabs)/dashboard",
      category: "control",
      badgeCount: 0,
    },
    {
      code: "bills",
      titleVi: "Đơn hàng, Bill & Chặng/Chuyến",
      subtitleVi: "Quản lý vận hành, lập phiếu gửi Waybill, xác nhận CW",
      icon: "truck",
      route: "/(tabs)/bills",
      category: "operations",
      badgeCount: 0,
    },
    {
      code: "scanner",
      titleVi: "Quét mã vận đơn & Chụp chứng từ",
      subtitleVi: "Quét Barcode/QR HAWB/MAWB và đính kèm ảnh hiện trường",
      icon: "camera",
      route: "/(tabs)/scanner",
      category: "operations",
      badgeCount: 0,
    },
    {
      code: "rates",
      titleVi: "Bảng giá, So sánh & Tính giá Tự động",
      subtitleVi: "Tra cứu Rate Cards, so sánh báo giá & sinh CP/DT dự kiến",
      icon: "calculator",
      route: "/modules/rates",
      category: "operations",
      badgeCount: badges.fxExceptions,
    },
  ];

  if (canViewCost) {
    list.push(
      {
        code: "costs",
        titleVi: "Quản lý Chi phí & Phân bổ",
        subtitleVi: "Tạo chi phí, điều chỉnh (+/-) & phân bổ chi phí chung",
        icon: "trending-down",
        route: "/(tabs)/costs",
        category: "cost_ap",
        badgeCount: 0,
      },
      {
        code: "ap",
        titleVi: "Công nợ phải trả (AP)",
        subtitleVi: "Exposure phải trả, Sổ công nợ AP, điều chỉnh & xóa nợ",
        icon: "credit-card",
        route: "/(tabs)/ap",
        category: "cost_ap",
        badgeCount: 0,
      }
    );
  }

  if (canViewRevenue) {
    list.push(
      {
        code: "revenues",
        titleVi: "Quản lý Doanh thu",
        subtitleVi: canViewMargin
          ? "Doanh thu theo Bill, điều chỉnh, chia doanh thu & báo cáo lãi gộp"
          : "Doanh thu theo Bill, điều chỉnh & chia doanh thu",
        icon: "trending-up",
        route: "/(tabs)/revenues",
        category: "revenue_ar",
        badgeCount: 0,
      },
      {
        code: "ar",
        titleVi: "Công nợ phải thu (AR)",
        subtitleVi: "Exposure phải thu, Sổ công nợ AR, điều chỉnh & xóa nợ",
        icon: "wallet",
        route: "/(tabs)/ar",
        category: "revenue_ar",
        badgeCount: 0,
      }
    );
  }

  if (canViewCost || canViewRevenue) {
    list.push(
      {
        code: "documents",
        titleVi: "Chứng từ tài chính & Đối khớp N:N",
        subtitleVi: "Tiếp nhận chứng từ, đính kèm ảnh/PDF, đối chiếu & sinh AP/AR",
        icon: "file-check",
        route: "/(tabs)/documents",
        category: canViewCost ? "cost_ap" : "revenue_ar",
        badgeCount: 0,
      },
      {
        code: "settlements",
        titleVi: "Thanh toán, Thu tiền & Sao kê Ngân hàng",
        subtitleVi: "Lập Phiếu Chi/Phiếu Thu, gạch nợ AP/AR & dòng sao kê Bank Feed",
        icon: "repeat",
        route: "/modules/settlements",
        category: canViewCost ? "cost_ap" : "revenue_ar",
        badgeCount: 0,
      },
      {
        code: "approvals",
        titleVi: "Hàng đợi Phê duyệt",
        subtitleVi: "Duyệt nhanh bằng FaceID/Vân tay kèm xem trước Before → After",
        icon: "check-circle",
        route: "/(tabs)/approvals",
        category: "control",
        badgeCount: badges.pendingApprovals,
      },
      {
        code: "control",
        titleVi: "Kiểm soát Ngoại lệ, Chênh lệch & Đối soát",
        subtitleVi: "Xử lý Exceptions, Variances & Reconciliations",
        icon: "shield-alert",
        route: "/(tabs)/control",
        category: "control",
        badgeCount: badges.openExceptions + badges.openVariances,
      },
      {
        code: "closes",
        titleVi: "Chốt kỳ Tài chính & Bản chụp Bất biến",
        subtitleVi: "Kiểm tra Eligibility, chụp Snapshot khóa sổ, xem P&L & Reopen",
        icon: "lock",
        route: "/modules/closes",
        category: "control",
        badgeCount: 0,
      },
      {
        code: "reports",
        titleVi: "Báo cáo Tuổi nợ AP/AR & Lợi nhuận Gộp",
        subtitleVi: "Phân tích Aging theo nguyên tệ, lãi gộp theo Bill/KH & dòng tiền",
        icon: "pie-chart",
        route: "/modules/reports",
        category: "control",
        badgeCount: 0,
      }
    );
  }

  list.push(
    {
      code: "master",
      titleVi: "Danh mục Đối tác, Tỷ giá VCB & Danh mục chuẩn",
      subtitleVi: "Tra cứu/Khóa đối tác, đồng bộ tỷ giá Vietcombank 1-chạm",
      icon: "database",
      route: "/modules/master",
      category: "master_admin",
      badgeCount: 0,
    },
    {
      code: "settings",
      titleVi: "Quản trị, Nhật ký Audit & Sức khỏe Tích hợp",
      subtitleVi: "Xem lịch sử kiểm toán, người dùng/vai trò & xử lý Outbox máy chủ",
      icon: "settings",
      route: "/modules/settings",
      category: "master_admin",
      badgeCount: 0,
    },
    {
      code: "offline",
      titleVi: "Hàng đợi Đồng bộ Offline",
      subtitleVi: "Quản lý các bản ghi lưu nháp khi mất sóng tại kho/cảng",
      icon: "cloud-upload",
      route: "/(tabs)/offline",
      category: "master_admin",
      badgeCount: 0,
    }
  );

  return list;
}

async function buildClientBootstrapFallback(): Promise<MobileBootstrapDto> {
  const stored = await getStoredSessionTokens();
  const email = stored?.userEmail ?? "admin@cms.local";
  const displayName = stored?.userDisplayName ?? email;
  const userId = stored?.userId ?? "00000000-0000-0000-0000-000000000001";
  const tenantId = stored?.tenantId ?? "ops";

  let terminology: TerminologyMap = DEFAULT_TERMINOLOGY_VI;
  try {
    const serverTerms = await apiRequest<TerminologyMap>("/api/terminology", {
      skipAuthRefresh: true,
    });
    if (serverTerms && typeof serverTerms === "object") {
      terminology = { ...DEFAULT_TERMINOLOGY_VI, ...serverTerms };
    }
  } catch {
    // Use local Vietnamese terminology fallback
  }

  const { roleCode, persona, canViewCost, canViewRevenue, canViewMargin } =
    resolveFallbackRoleAndPersona(email);

  const badges: MobileBadgesDto = {
    unreadNotifications: 0,
    pendingApprovals: 0,
    openExceptions: 0,
    openVariances: 0,
    fxExceptions: 0,
  };

  return {
    asOfUtc: new Date().toISOString(),
    user: {
      id: userId,
      email,
      displayName,
      organizationId: null,
    },
    tenant: {
      id: tenantId,
      code: "OPS",
      name: "LCMS Logistics (VPS Contabo)",
      defaultCurrencyCode: "VND",
      dateFormat: "dd/MM/yyyy",
      timeZoneId: "Asia/Ho_Chi_Minh",
    },
    roleCodes: [roleCode],
    primaryPersona: persona,
    permissions: [],
    financialVisibility: {
      canViewCost,
      canViewRevenue,
      canViewMargin,
    },
    enabledModules: [
      "dashboard",
      "bills",
      "rates",
      "costs",
      "revenues",
      "documents",
      "ap",
      "ar",
      "settlements",
      "control",
      "closes",
      "reports",
      "master",
      "settings",
    ],
    bottomTabs: buildFallbackBottomTabs(persona, badges),
    modules: buildFallbackModules({
      canViewCost,
      canViewRevenue,
      canViewMargin,
      badges,
    }),
    badges,
    terminology,
  };
}

async function registerPushTokenBestEffort(): Promise<string | null> {
  if (Platform.OS === "web") {
    return null;
  }

  try {
    // Expo SDK 53+ removed remote Android Push Notifications from Expo Go client.
    // When testing inside Expo Go on Android, register a virtual ExpoGo device token
    // with LCMS.Api without importing expo-notifications (which throws at module load).
    if (isExpoGo && Platform.OS === "android") {
      const virtualToken = `ExpoGoToken[${Constants.sessionId || "android-dev"}]`;
      await apiRequest("/api/notifications/devices", {
        method: "POST",
        body: {
          deviceToken: virtualToken,
          platform: "android",
          deviceName: `${Constants.deviceName ?? "Android"} (Expo Go)`,
          appVersion: "1.0.0",
        },
      });
      return virtualToken;
    }

    // Dynamic require only in native Development Builds / Standalone APK/IPA or iOS
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const Notifications = require("expo-notifications") as typeof import("expo-notifications");
    const { status: existingStatus } = await Notifications.getPermissionsAsync();
    let finalStatus = existingStatus;
    if (existingStatus !== "granted") {
      const { status } = await Notifications.requestPermissionsAsync();
      finalStatus = status;
    }
    if (finalStatus !== "granted") {
      return null;
    }
    const tokenData = await Notifications.getExpoPushTokenAsync();
    const deviceToken = tokenData.data;
    if (deviceToken) {
      await apiRequest("/api/notifications/devices", {
        method: "POST",
        body: {
          deviceToken,
          platform: Platform.OS === "ios" ? "ios" : "android",
          deviceName: `${Platform.OS.toUpperCase()} Mobile`,
          appVersion: "1.0.0",
        },
      });
    }
    return deviceToken;
  } catch {
    return null;
  }
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [bootstrap, setBootstrap] = useState<MobileBootstrapDto | null>(null);
  const [pushToken, setPushToken] = useState<string | null>(null);

  const refreshBootstrap = useCallback(async () => {
    try {
      const raw = await apiRequest<
        Omit<MobileBootstrapDto, "bottomTabs" | "modules"> & {
          bottomTabs?: Array<MobileTabDto & { label?: string }>;
          modules?: Array<
            Omit<MobileModuleDto, "category"> & {
              label?: string;
              description?: string;
              category?: string;
            }
          >;
        }
      >("/api/mobile/bootstrap");

      const validTabKeys = new Set([
        "dashboard",
        "approvals",
        "control",
        "bills",
        "scanner",
        "costs",
        "revenues",
        "ap",
        "ar",
        "documents",
        "offline",
        "modules",
      ]);

      const serverTabs = (raw.bottomTabs ?? [])
        .filter((t) => validTabKeys.has(t.key))
        .map((t) => ({
          ...t,
          labelVi: t.labelVi ?? t.label ?? t.key,
        }));

      const fallbackTabs = buildFallbackBottomTabs(
        raw.primaryPersona ?? "executive_control",
        raw.badges
      );

      const finalTabs = serverTabs.length >= 3 ? serverTabs : fallbackTabs;
      if (!finalTabs.some((t) => t.key === "modules")) {
        finalTabs.push({
          key: "modules",
          labelVi: "Phân hệ",
          icon: "grid",
          route: "/(tabs)/modules",
          badgeCount: raw.badges?.unreadNotifications ?? 0,
        });
      }

      const serverModules: MobileModuleDto[] = (raw.modules ?? []).map((m) => ({
        ...m,
        titleVi: m.titleVi ?? m.label ?? m.code,
        subtitleVi: m.subtitleVi ?? m.description ?? "",
        category:
          m.category === "finance" ||
          m.category === "apar" ||
          m.category === "cost_ap" ||
          m.category === "revenue_ar"
            ? m.code === "revenues" || m.code === "ar"
              ? "revenue_ar"
              : "cost_ap"
            : m.category === "operations" || m.category === "pricing"
            ? "operations"
            : m.category === "control" ||
              m.category === "core" ||
              m.category === "reports"
            ? "control"
            : "master_admin",
      }));

      const fallbackModules = buildFallbackModules({
        canViewCost: raw.financialVisibility?.canViewCost ?? true,
        canViewRevenue: raw.financialVisibility?.canViewRevenue ?? true,
        canViewMargin: raw.financialVisibility?.canViewMargin ?? true,
        badges: raw.badges ?? {
          unreadNotifications: 0,
          pendingApprovals: 0,
          openExceptions: 0,
          openVariances: 0,
          fxExceptions: 0,
        },
      });

      const mergedModulesMap = new Map<string, MobileModuleDto>();
      for (const sm of serverModules) {
        mergedModulesMap.set(sm.code, sm);
      }
      for (const fm of fallbackModules) {
        if (!mergedModulesMap.has(fm.code)) {
          mergedModulesMap.set(fm.code, fm);
        }
      }

      const normalized: MobileBootstrapDto = {
        ...raw,
        asOfUtc: raw.asOfUtc ?? new Date().toISOString(),
        bottomTabs: finalTabs,
        modules: Array.from(mergedModulesMap.values()),
      };
      setBootstrap(normalized);
    } catch (err) {
      if (err instanceof LcmsApiException && err.parsed.status === 404) {
        const fallback = await buildClientBootstrapFallback();
        setBootstrap(fallback);
        return;
      }
      throw err;
    }
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const stored = await getStoredSessionTokens();
        if (stored) {
          await refreshBootstrap();
          const token = await registerPushTokenBestEffort();
          setPushToken(token);
        }
      } catch {
        await clearSessionTokens();
        setBootstrap(null);
      } finally {
        setIsLoading(false);
      }
    })();
  }, [refreshBootstrap]);

  const loginWithCredentials = useCallback(
    async (params: { tenantCodeOrId: string; email: string; password: string }) => {
      const res = await apiRequest<{
        accessToken: string;
        refreshToken?: string;
        tenantId?: string;
        user?: {
          id: string;
          email: string;
          displayName: string;
          tenantId: string;
        };
      }>("/api/auth/login", {
        method: "POST",
        skipAuthRefresh: true,
        body: {
          email: params.email.trim(),
          password: params.password,
        },
      });

      const resolvedTenantId =
        res.user?.tenantId ?? res.tenantId ?? params.tenantCodeOrId.trim();

      await saveSessionTokens({
        accessToken: res.accessToken,
        refreshToken: res.refreshToken ?? null,
        tenantId: resolvedTenantId,
        userId: res.user?.id ?? null,
        userEmail: res.user?.email ?? params.email.trim(),
        userDisplayName: res.user?.displayName ?? params.email.trim(),
      });

      await refreshBootstrap();
      const token = await registerPushTokenBestEffort();
      setPushToken(token);
    },
    [refreshBootstrap]
  );

  const logout = useCallback(async () => {
    try {
      if (pushToken) {
        await apiRequest(
          `/api/notifications/devices?deviceToken=${encodeURIComponent(pushToken)}`,
          { method: "DELETE", skipAuthRefresh: true }
        );
      }
      await apiRequest("/api/auth/logout", { method: "POST", skipAuthRefresh: true });
    } catch {
      // Best-effort server logout
    } finally {
      await clearSessionTokens();
      setBootstrap(null);
      setPushToken(null);
    }
  }, [pushToken]);

  const terminology = bootstrap?.terminology ?? DEFAULT_TERMINOLOGY_VI;
  const t = useCallback(
    (key: string, fallback?: string) => term(terminology, key, fallback),
    [terminology]
  );

  return (
    <AuthContext.Provider
      value={{
        isLoading,
        isAuthenticated: Boolean(bootstrap),
        bootstrap,
        pushToken,
        terminology,
        t,
        loginWithCredentials,
        refreshBootstrap,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth phải được sử dụng bên trong AuthProvider.");
  }
  return ctx;
}
