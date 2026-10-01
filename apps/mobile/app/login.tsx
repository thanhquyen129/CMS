import React, { useEffect, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../src/auth/AuthContext";
import { getApiBaseUrl, setApiBaseUrl } from "../src/api/client";

const DEMO_ROLES = [
  {
    role: "Quản trị (Admin)",
    email: "admin@cms.local",
    desc: "Toàn quyền điều hành, phê duyệt, xem biên lợi nhuận",
  },
  {
    role: "Kiểm soát tài chính (FinancialController)",
    email: "controller@cms.local",
    desc: "Duyệt ngoại lệ, kiểm soát chênh lệch, khóa sổ kỳ",
  },
  {
    role: "Kế toán chi phí (CostAccountant)",
    email: "cost@cms.local",
    desc: "Quản lý Chi phí vận tải & Công nợ nhà cung cấp (AP)",
  },
  {
    role: "Kế toán doanh thu (RevenueAccountant)",
    email: "revenue@cms.local",
    desc: "Quản lý Doanh thu bán cước & Công nợ khách hàng (AR)",
  },
  {
    role: "Điều vận hiện trường (Ops)",
    email: "ops.user@cms.local",
    desc: "Quét mã vận đơn, xác nhận CW & ghi nhận chi phí hiện trường",
  },
  {
    role: "Quản trị danh mục (MasterData)",
    email: "master@cms.local",
    desc: "Quản lý đối tác, tuyến đường, địa điểm, biểu giá",
  },
  {
    role: "Chỉ xem Bill (Viewer)",
    email: "viewer@cms.local",
    desc: "Chỉ xem thông tin vận đơn cơ bản",
  },
];

export default function LoginScreen() {
  const router = useRouter();
  const { isAuthenticated, isLoading, loginWithCredentials } = useAuth();
  const [tenantCode, setTenantCode] = useState(
    process.env.EXPO_PUBLIC_DEFAULT_TENANT ?? "ops"
  );
  const [email, setEmail] = useState("admin@cms.local");
  const [password, setPassword] = useState("abc123");
  const [showPassword, setShowPassword] = useState(false);
  const [apiUrl, setApiUrl] = useState(
    process.env.EXPO_PUBLIC_API_URL ?? "http://194.233.89.26"
  );
  const [showServerConfig, setShowServerConfig] = useState(
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS !== "false"
  );
  const [showDemoPersonas, setShowDemoPersonas] = useState(
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS !== "false"
  );
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    void getApiBaseUrl().then(setApiUrl);
  }, []);

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      router.replace("/(tabs)/dashboard");
    }
  }, [isLoading, isAuthenticated, router]);

  const handleLogin = async () => {
    if (!tenantCode.trim() || !email.trim() || !password) {
      Alert.alert("Thiếu thông tin", "Vui lòng nhập Mã thuê bao, Email và Mật khẩu.");
      return;
    }

    setSubmitting(true);
    try {
      if (apiUrl.trim()) {
        await setApiBaseUrl(apiUrl.trim());
      }
      await loginWithCredentials({
        tenantCodeOrId: tenantCode,
        email,
        password,
      });
      router.replace("/(tabs)/dashboard");
    } catch (err) {
      Alert.alert(
        "Đăng nhập không thành công",
        err instanceof Error ? err.message : "Không thể kết nối tới máy chủ LCMS."
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <View style={styles.brandBox}>
        <View style={styles.logoBadge}>
          <Text style={styles.logoBadgeText}>CMS</Text>
        </View>
        <Text style={styles.title}>Hệ thống Quản trị Tài chính Doanh nghiệp</Text>
        <Text style={styles.subtitle}>
          Nền tảng Quản trị &amp; Kiểm soát Tài chính Doanh nghiệp Toàn diện
        </Text>
      </View>

      <View style={styles.card}>
        <Text style={styles.label}>Mã Đơn vị / Doanh nghiệp</Text>
        <TextInput
          style={styles.input}
          value={tenantCode}
          onChangeText={setTenantCode}
          autoCapitalize="none"
          placeholder="VD: ops"
        />

        <Text style={styles.label}>Email đăng nhập</Text>
        <TextInput
          style={styles.input}
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          keyboardType="email-address"
          placeholder="email@company.vn"
        />

        <View style={styles.passwordLabelRow}>
          <Text style={styles.label}>Mật khẩu</Text>
          <TouchableOpacity onPress={() => setShowPassword((v) => !v)}>
            <Text style={styles.showPassToggle}>
              {showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
            </Text>
          </TouchableOpacity>
        </View>
        <TextInput
          style={styles.input}
          value={password}
          onChangeText={setPassword}
          secureTextEntry={!showPassword}
          autoCapitalize="none"
          placeholder="Nhập mật khẩu truy cập"
        />

        <TouchableOpacity
          style={styles.configToggle}
          onPress={() => setShowServerConfig((v) => !v)}
        >
          <Text style={styles.configToggleText}>
            {showServerConfig
              ? "▲ Ẩn cài đặt kết nối máy chủ"
              : "⚙️ Cài đặt kết nối máy chủ hệ thống"}
          </Text>
        </TouchableOpacity>

        {showServerConfig ? (
          <View style={styles.serverConfigBox}>
            <Text style={styles.label}>Địa chỉ máy chủ trung tâm (Server URL)</Text>
            <TextInput
              style={styles.input}
              value={apiUrl}
              onChangeText={setApiUrl}
              autoCapitalize="none"
              placeholder="http://194.233.89.26"
            />
          </View>
        ) : null}

        <TouchableOpacity
          style={[styles.loginBtn, submitting && styles.loginBtnDisabled]}
          onPress={handleLogin}
          disabled={submitting}
        >
          {submitting ? (
            <ActivityIndicator color="#FFFFFF" />
          ) : (
            <Text style={styles.loginBtnText}>Đăng nhập an toàn</Text>
          )}
        </TouchableOpacity>
      </View>

      <View style={styles.personaSection}>
        <TouchableOpacity
          onPress={() => setShowDemoPersonas((v) => !v)}
          style={{ marginBottom: 8 }}
        >
          <Text style={styles.personaHeading}>
            {showDemoPersonas ? "▼ " : "▶ "}
            Chọn nhanh tài khoản theo vai trò (Role-Adaptive Demo • Mật khẩu: abc123)
          </Text>
        </TouchableOpacity>
        {showDemoPersonas
          ? DEMO_ROLES.map((item) => (
              <TouchableOpacity
                key={item.email}
                style={[
                  styles.personaCard,
                  email === item.email && styles.personaCardActive,
                ]}
                onPress={() => {
                  setEmail(item.email);
                  if (!tenantCode.trim()) setTenantCode("ops");
                  if (!password) setPassword("abc123");
                }}
              >
                <Text style={styles.personaRole}>{item.role}</Text>
                <Text style={styles.personaEmail}>{item.email}</Text>
                <Text style={styles.personaDesc}>{item.desc}</Text>
              </TouchableOpacity>
            ))
          : null}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flexGrow: 1,
    backgroundColor: "#F8FAFC",
    padding: 20,
    paddingTop: 64,
  },
  brandBox: {
    marginBottom: 20,
  },
  passwordLabelRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  showPassToggle: {
    fontSize: 12,
    fontWeight: "600",
    color: "#2563EB",
    marginBottom: 6,
  },
  logoBadge: {
    backgroundColor: "#0F172A",
    alignSelf: "flex-start",
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    marginBottom: 10,
  },
  logoBadgeText: {
    color: "#FFFFFF",
    fontWeight: "800",
    fontSize: 14,
    letterSpacing: 1,
  },
  title: {
    fontSize: 22,
    fontWeight: "800",
    color: "#0F172A",
  },
  subtitle: {
    fontSize: 13,
    color: "#475569",
    marginTop: 4,
  },
  card: {
    backgroundColor: "#FFFFFF",
    borderRadius: 14,
    padding: 18,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 20,
  },
  label: {
    fontSize: 12,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 6,
  },
  input: {
    borderWidth: 1,
    borderColor: "#CBD5E1",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    color: "#0F172A",
    backgroundColor: "#F8FAFC",
    marginBottom: 14,
  },
  configToggle: {
    marginBottom: 12,
  },
  configToggleText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#2563EB",
  },
  serverConfigBox: {
    marginBottom: 8,
  },
  loginBtn: {
    backgroundColor: "#0F172A",
    borderRadius: 10,
    paddingVertical: 13,
    alignItems: "center",
  },
  loginBtnDisabled: {
    opacity: 0.6,
  },
  loginBtnText: {
    color: "#FFFFFF",
    fontSize: 15,
    fontWeight: "700",
  },
  personaSection: {
    marginBottom: 32,
  },
  personaHeading: {
    fontSize: 13,
    fontWeight: "700",
    color: "#334155",
    marginBottom: 10,
  },
  personaCard: {
    backgroundColor: "#FFFFFF",
    borderRadius: 10,
    padding: 12,
    borderWidth: 1,
    borderColor: "#E2E8F0",
    marginBottom: 8,
  },
  personaCardActive: {
    borderColor: "#0F172A",
    backgroundColor: "#F1F5F9",
  },
  personaRole: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0F172A",
  },
  personaEmail: {
    fontSize: 12,
    color: "#2563EB",
    marginTop: 1,
  },
  personaDesc: {
    fontSize: 11,
    color: "#64748B",
    marginTop: 3,
  },
});

