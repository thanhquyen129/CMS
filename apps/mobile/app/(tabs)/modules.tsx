import React from "react";
import {
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/AuthContext";

const CATEGORY_TITLES: Record<string, string> = {
  control: "1. Điều hành, Phê duyệt & Kiểm soát Tài chính",
  operations: "2. Vận hành, Vận đơn (Bill) & Biểu giá (Rating)",
  cost_ap: "3. Chi phí, Công nợ Phải trả (AP) & Thanh toán",
  revenue_ar: "4. Doanh thu, Công nợ Phải thu (AR) & Thu tiền",
  master_admin: "5. Danh mục Master Data & Quản trị Thuê bao",
};

export default function ModulesHubScreen() {
  const router = useRouter();
  const { bootstrap, logout } = useAuth();
  const modules = bootstrap?.modules ?? [];

  const categories = [
    "control",
    "operations",
    "cost_ap",
    "revenue_ar",
    "master_admin",
  ] as const;

  const handleNavigateModule = (code: string, route: string) => {
    const codeRouteMap: Record<string, string> = {
      dashboard: "/(tabs)/dashboard",
      bills: "/(tabs)/bills",
      scanner: "/(tabs)/scanner",
      costs: "/(tabs)/costs",
      revenues: "/(tabs)/revenues",
      ap: "/(tabs)/ap",
      ar: "/(tabs)/ar",
      documents: "/(tabs)/documents",
      approvals: "/(tabs)/approvals",
      control: "/(tabs)/control",
      offline: "/(tabs)/offline",
      settlements: "/modules/settlements",
      rates: "/modules/rates",
      closes: "/modules/closes",
      reports: "/modules/reports",
      master: "/modules/master",
      settings: "/modules/settings",
    };

    const target = codeRouteMap[code] ?? route ?? "/(tabs)/dashboard";
    router.push(target as never);
  };

  return (
    <ScrollView style={styles.container}>
      <View style={styles.profileCard}>
        <Text style={styles.userName}>{bootstrap?.user.displayName}</Text>
        <Text style={styles.userEmail}>{bootstrap?.user.email}</Text>
        <Text style={styles.roleList}>
          Vai trò: {(bootstrap?.roleCodes ?? []).join(", ") || "Operator"} • Persona:{" "}
          {bootstrap?.primaryPersona}
        </Text>
        <Text style={styles.storageNote}>
          📁 Chứng từ ảnh/PDF lưu trực tiếp trên ổ đĩa VPS Contabo (/opt/cms/attachments)
        </Text>
      </View>

      {categories.map((cat) => {
        const items = modules.filter((m) => m.category === cat);
        if (items.length === 0) return null;

        return (
          <View key={cat} style={styles.categoryBlock}>
            <Text style={styles.categoryHeader}>{CATEGORY_TITLES[cat]}</Text>
            {items.map((mod) => (
              <TouchableOpacity
                key={mod.code}
                style={styles.moduleRow}
                onPress={() => handleNavigateModule(mod.code, mod.route)}
              >
                <View style={{ flex: 1 }}>
                  <Text style={styles.moduleTitle}>{mod.titleVi}</Text>
                  <Text style={styles.moduleSub}>{mod.subtitleVi}</Text>
                </View>
                <Text style={{ fontSize: 13, color: "#2563EB", fontWeight: "700", marginLeft: 8 }}>
                  Mở →
                </Text>
                {mod.badgeCount > 0 ? (
                  <View style={styles.badge}>
                    <Text style={styles.badgeText}>{mod.badgeCount}</Text>
                  </View>
                ) : null}
              </TouchableOpacity>
            ))}
          </View>
        );
      })}

      <TouchableOpacity
        style={styles.logoutBtn}
        onPress={async () => {
          await logout();
          router.replace("/login");
        }}
      >
        <Text style={styles.logoutBtnText}>
          Đăng xuất & Hủy đăng ký Push Token thiết bị
        </Text>
      </TouchableOpacity>

      <View style={{ height: 36 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#F8FAFC",
    padding: 16,
  },
  profileCard: {
    backgroundColor: "#0F172A",
    borderRadius: 14,
    padding: 16,
    marginBottom: 16,
  },
  userName: {
    color: "#FFFFFF",
    fontSize: 17,
    fontWeight: "800",
  },
  userEmail: {
    color: "#94A3B8",
    fontSize: 13,
    marginTop: 2,
  },
  roleList: {
    color: "#E2E8F0",
    fontSize: 12,
    marginTop: 6,
    fontWeight: "600",
  },
  storageNote: {
    color: "#38BDF8",
    fontSize: 11,
    marginTop: 8,
  },
  categoryBlock: {
    marginBottom: 16,
  },
  categoryHeader: {
    fontSize: 13,
    fontWeight: "800",
    color: "#334155",
    marginBottom: 8,
  },
  moduleRow: {
    backgroundColor: "#FFFFFF",
    borderRadius: 10,
    padding: 14,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    flexDirection: "row",
    alignItems: "center",
  },
  moduleTitle: {
    fontSize: 14,
    fontWeight: "700",
    color: "#0F172A",
  },
  moduleSub: {
    fontSize: 12,
    color: "#64748B",
    marginTop: 2,
  },
  badge: {
    backgroundColor: "#DC2626",
    borderRadius: 12,
    paddingHorizontal: 8,
    paddingVertical: 3,
    marginLeft: 8,
  },
  badgeText: {
    color: "#FFFFFF",
    fontSize: 11,
    fontWeight: "700",
  },
  logoutBtn: {
    backgroundColor: "#FEF2F2",
    borderWidth: 1,
    borderColor: "#FECACA",
    borderRadius: 10,
    paddingVertical: 13,
    alignItems: "center",
    marginTop: 8,
  },
  logoutBtnText: {
    color: "#DC2626",
    fontSize: 14,
    fontWeight: "700",
  },
});

