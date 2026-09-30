import * as LocalAuthentication from "expo-local-authentication";
import { Platform } from "react-native";

export interface BiometricPromptOptions {
  actionLabelVi: string;
  amountSummaryVi?: string;
}

export interface BiometricVerificationResult {
  verified: boolean;
  method: "biometric" | "device_passcode" | "fallback_confirmed";
  errorVi?: string;
}

/**
 * Step-up biometric verification (FaceID / TouchID / Fingerprint / Device Credential)
 * required before executing any money-impacting mutation on mobile:
 * - Approve / Reject request
 * - Confirm / Actualize / Adjust Cost or Revenue
 * - Allocate Payment / Collection
 * - Write-off / Reverse Write-off AP or AR
 * - Lock / Reopen Financial Close
 */
export async function verifyBiometricForMoneyAction(
  options: BiometricPromptOptions
): Promise<BiometricVerificationResult> {
  if (Platform.OS === "web") {
    return { verified: true, method: "fallback_confirmed" };
  }

  try {
    const hasHardware = await LocalAuthentication.hasHardwareAsync();
    const isEnrolled = await LocalAuthentication.isEnrolledAsync();

    if (!hasHardware || !isEnrolled) {
      // Device has no biometric enrolled — fall back to OS device credential if supported
      const result = await LocalAuthentication.authenticateAsync({
        promptMessage: `Xác nhận bảo mật: ${options.actionLabelVi}`,
        cancelLabel: "Hủy",
        disableDeviceFallback: false,
      });

      if (result.success) {
        return { verified: true, method: "device_passcode" };
      }
      return {
        verified: false,
        method: "device_passcode",
        errorVi: "Chưa xác thực khóa màn hình thiết bị.",
      };
    }

    const promptMessage = options.amountSummaryVi
      ? `${options.actionLabelVi} (${options.amountSummaryVi})`
      : `Xác thực sinh trắc học: ${options.actionLabelVi}`;

    const authResult = await LocalAuthentication.authenticateAsync({
      promptMessage,
      cancelLabel: "Hủy thao tác",
      fallbackLabel: "Dùng mật khẩu thiết bị",
      disableDeviceFallback: false,
    });

    if (authResult.success) {
      return { verified: true, method: "biometric" };
    }

    return {
      verified: false,
      method: "biometric",
      errorVi: "Xác thực sinh trắc học đã bị hủy hoặc không khớp.",
    };
  } catch {
    return {
      verified: false,
      method: "biometric",
      errorVi: "Không thể khởi chạy cảm biến sinh trắc học trên thiết bị.",
    };
  }
}

