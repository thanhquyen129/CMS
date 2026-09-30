import React from "react";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { AuthProvider } from "../src/auth/AuthContext";

export default function RootLayout() {
  return (
    <AuthProvider>
      <StatusBar style="light" />
      <Stack
        screenOptions={{
          headerShown: false,
          headerStyle: { backgroundColor: "#0F172A" },
          headerTintColor: "#FFFFFF",
          headerTitleStyle: { fontWeight: "700", fontSize: 16 },
        }}
      >
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen name="login" options={{ headerShown: false }} />
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen
          name="bills/[id]"
          options={{
            headerShown: true,
            title: "Chi tiết Vận đơn 360° (Bill)",
            headerBackTitle: "Quay lại",
          }}
        />
        <Stack.Screen
          name="modules/settlements"
          options={{
            headerShown: true,
            title: "Thanh toán, Thu tiền & Sao kê",
            headerBackTitle: "Phân hệ",
          }}
        />
        <Stack.Screen
          name="modules/rates"
          options={{
            headerShown: true,
            title: "Bảng giá, So sánh & Tính giá",
            headerBackTitle: "Phân hệ",
          }}
        />
        <Stack.Screen
          name="modules/closes"
          options={{
            headerShown: true,
            title: "Chốt kỳ Tài chính & Snapshot",
            headerBackTitle: "Phân hệ",
          }}
        />
        <Stack.Screen
          name="modules/reports"
          options={{
            headerShown: true,
            title: "Báo cáo Tuổi nợ & Lợi nhuận",
            headerBackTitle: "Phân hệ",
          }}
        />
        <Stack.Screen
          name="modules/master"
          options={{
            headerShown: true,
            title: "Danh mục Đối tác & Tỷ giá VCB",
            headerBackTitle: "Phân hệ",
          }}
        />
        <Stack.Screen
          name="modules/settings"
          options={{
            headerShown: true,
            title: "Quản trị, Audit & Tích hợp",
            headerBackTitle: "Phân hệ",
          }}
        />
      </Stack>
    </AuthProvider>
  );
}
