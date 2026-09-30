import React, { useEffect } from "react";
import { Tabs, useRouter } from "expo-router";
import { Text } from "react-native";
import { useAuth } from "../../src/auth/AuthContext";

const ALL_TAB_KEYS = [
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
] as const;

export default function RoleAdaptiveTabsLayout() {
  const router = useRouter();
  const { isLoading, isAuthenticated, bootstrap } = useAuth();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace("/login");
    }
  }, [isLoading, isAuthenticated, router]);

  const activeTabs = bootstrap?.bottomTabs ?? [
    {
      key: "dashboard",
      labelVi: "Tổng quan",
      icon: "bar-chart-2",
      route: "/(tabs)/dashboard",
      badgeCount: 0,
    },
    {
      key: "bills",
      labelVi: "Vận đơn",
      icon: "file-text",
      route: "/(tabs)/bills",
      badgeCount: 0,
    },
    {
      key: "scanner",
      labelVi: "Quét & Chụp",
      icon: "camera",
      route: "/(tabs)/scanner",
      badgeCount: 0,
    },
    {
      key: "offline",
      labelVi: "Ngoại tuyến",
      icon: "cloud-off",
      route: "/(tabs)/offline",
      badgeCount: 0,
    },
    {
      key: "modules",
      labelVi: "Phân hệ",
      icon: "grid",
      route: "/(tabs)/modules",
      badgeCount: 0,
    },
  ];

  const activeMap = new Map(activeTabs.map((t) => [t.key, t]));

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: "#0F172A" },
        headerTintColor: "#FFFFFF",
        headerTitleStyle: { fontWeight: "700", fontSize: 16 },
        tabBarActiveTintColor: "#0F172A",
        tabBarInactiveTintColor: "#64748B",
        tabBarStyle: {
          backgroundColor: "#FFFFFF",
          borderTopColor: "#E2E8F0",
          height: 60,
          paddingBottom: 8,
          paddingTop: 6,
        },
      }}
    >
      {ALL_TAB_KEYS.map((key) => {
        const tabConfig = activeMap.get(key);
        const isVisibleInBottomBar = Boolean(tabConfig);

        return (
          <Tabs.Screen
            key={key}
            name={key}
            options={{
              title: tabConfig?.labelVi ?? key,
              href: isVisibleInBottomBar ? undefined : null,
              tabBarBadge:
                tabConfig && tabConfig.badgeCount > 0
                  ? tabConfig.badgeCount
                  : undefined,
              tabBarIcon: ({ color }) => (
                <Text style={{ color, fontSize: 16, fontWeight: "700" }}>
                  {getTabEmoji(key)}
                </Text>
              ),
            }}
          />
        );
      })}
    </Tabs>
  );
}

function getTabEmoji(key: string): string {
  switch (key) {
    case "dashboard":
      return "📊";
    case "approvals":
      return "✅";
    case "control":
      return "🛡️";
    case "bills":
      return "📦";
    case "scanner":
      return "📷";
    case "costs":
      return "💸";
    case "revenues":
      return "💰";
    case "ap":
      return "🧾";
    case "ar":
      return "🏦";
    case "documents":
      return "📑";
    case "offline":
      return "📡";
    case "modules":
      return "🧭";
    default:
      return "•";
  }
}

