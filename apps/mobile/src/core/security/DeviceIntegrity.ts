/**
 * Device Integrity & Anti-Tampering Engine for LCMS Mobile Enterprise
 * 
 * Detects rooted Android environments (Magisk, SuperSU), jailbroken iOS devices (Cydia, Sileo),
 * and dynamic instrumentation frameworks (Frida, Xposed, Substrate).
 * 
 * Enforces hardware and OS trust policy: strictly blocks financial mutations (approvals,
 * disbursements, payment allocations, state transitions) on compromised devices.
 */

import { Platform } from 'react-native';
import * as FileSystem from 'expo-file-system/legacy';

export interface DeviceIntegrityResult {
  isCompromised: boolean;
  reasons: string[];
  detectedThreats: ('ROOT' | 'JAILBREAK' | 'FRIDA' | 'HOOKING')[];
  scannedAt: string;
}

export class DeviceSecurityCompromisedException extends Error {
  public readonly threats: string[];

  constructor(message: string, threats: string[] = []) {
    super(message);
    this.name = 'DeviceSecurityCompromisedException';
    this.threats = threats;
  }
}

// Suspicious paths indicating Android Root (Magisk, SuperSU, Busybox)
const SUSPICIOUS_ANDROID_ROOT_PATHS = [
  '/system/app/Superuser.apk',
  '/sbin/su',
  '/system/bin/su',
  '/system/xbin/su',
  '/data/local/xbin/su',
  '/data/local/bin/su',
  '/system/sd/xbin/su',
  '/system/bin/failsafe/su',
  '/data/local/su',
  '/su/bin/su',
  '/system/bin/.ext/.su',
  '/data/adb/magisk',
  '/sbin/.magisk',
  '/system/xbin/busybox',
  '/system/bin/busybox',
];

// Suspicious paths indicating iOS Jailbreak (Cydia, Sileo, Zebra, MobileSubstrate)
const SUSPICIOUS_IOS_JAILBREAK_PATHS = [
  '/Applications/Cydia.app',
  '/Applications/Sileo.app',
  '/Applications/Zebra.app',
  '/Library/MobileSubstrate/MobileSubstrate.dylib',
  '/bin/bash',
  '/usr/sbin/sshd',
  '/etc/apt',
  '/private/var/lib/apt/',
  '/private/var/stash',
  '/Library/MobileSubstrate/DynamicLibraries/LiveClock.dylib',
];

// Suspicious paths indicating Frida dynamic reverse-engineering server
const SUSPICIOUS_FRIDA_PATHS = [
  '/data/local/tmp/frida-server',
  '/data/local/tmp/re.frida.server',
  '/usr/lib/frida/frida-agent.dylib',
  '/data/local/tmp/frida',
];

/**
 * Checks runtime environment for Frida hooks, memory injection, or debugging flags
 */
function checkRuntimeFridaArtifacts(): { detected: boolean; reason?: string } {
  // Check global namespace for Frida instrumentation artifacts
  const globalAny = global as any;
  if (globalAny.frida || globalAny.__frida || globalAny._frida) {
    return { detected: true, reason: 'Phát hiện biến can thiệp toàn cục Frida runtime trong bộ nhớ JS' };
  }

  // Check for common Frida agent function tampering
  if (typeof (process as any)?.binding === 'function') {
    const bindingStr = (process as any).binding.toString();
    if (bindingStr.includes('frida') || bindingStr.includes('gum-js-loop')) {
      return { detected: true, reason: 'Phát hiện hook can thiệp process binding của Frida' };
    }
  }

  return { detected: false };
}

/**
 * Probes file system for suspicious binaries and packages
 */
async function probeSuspiciousFiles(paths: string[]): Promise<string[]> {
  const found: string[] = [];

  for (const p of paths) {
    try {
      const info = await FileSystem.getInfoAsync(p);
      if (info.exists) {
        found.push(p);
      }
    } catch {
      // In non-rooted sandbox, accessing external system paths throws a permission denied error
      // which is expected normal behavior.
    }
  }

  return found;
}

/**
 * Scans the device for OS tampering (Root, Jailbreak, Frida)
 */
export async function checkDeviceIntegrity(): Promise<DeviceIntegrityResult> {
  const reasons: string[] = [];
  const detectedThreats: ('ROOT' | 'JAILBREAK' | 'FRIDA' | 'HOOKING')[] = [];

  // Web environment (mocked preview)
  if (Platform.OS === 'web') {
    return {
      isCompromised: false,
      reasons: [],
      detectedThreats: [],
      scannedAt: new Date().toISOString(),
    };
  }

  // 1. Runtime Frida Artifacts Check
  const fridaRuntime = checkRuntimeFridaArtifacts();
  if (fridaRuntime.detected && fridaRuntime.reason) {
    reasons.push(fridaRuntime.reason);
    detectedThreats.push('FRIDA');
  }

  // 2. Android Root Probing
  if (Platform.OS === 'android') {
    const rootMatches = await probeSuspiciousFiles(SUSPICIOUS_ANDROID_ROOT_PATHS);
    if (rootMatches.length > 0) {
      reasons.push(`Phát hiện tệp tin đặc quyền hệ thống Root/Magisk: ${rootMatches.join(', ')}`);
      detectedThreats.push('ROOT');
    }

    const fridaMatches = await probeSuspiciousFiles(SUSPICIOUS_FRIDA_PATHS);
    if (fridaMatches.length > 0) {
      reasons.push(`Phát hiện tiến trình gỡ lỗi động Frida Server: ${fridaMatches.join(', ')}`);
      detectedThreats.push('FRIDA');
    }
  }

  // 3. iOS Jailbreak Probing
  if (Platform.OS === 'ios') {
    const jbMatches = await probeSuspiciousFiles(SUSPICIOUS_IOS_JAILBREAK_PATHS);
    if (jbMatches.length > 0) {
      reasons.push(`Phát hiện tệp tin và ứng dụng can thiệp Jailbreak (Cydia/Sileo): ${jbMatches.join(', ')}`);
      detectedThreats.push('JAILBREAK');
    }

    const fridaIosMatches = await probeSuspiciousFiles(SUSPICIOUS_FRIDA_PATHS);
    if (fridaIosMatches.length > 0) {
      reasons.push(`Phát hiện agent gỡ lỗi Frida: ${fridaIosMatches.join(', ')}`);
      detectedThreats.push('FRIDA');
    }
  }

  const isCompromised = detectedThreats.length > 0;

  return {
    isCompromised,
    reasons,
    detectedThreats,
    scannedAt: new Date().toISOString(),
  };
}

declare const __DEV__: boolean | undefined;

/**
 * Strictly verifies device integrity before executing any financial mutation.
 * Blocks execution immediately if Root, Jailbreak, or Frida is detected.
 * 
 * In development mode (__DEV__), this check is bypassed to prevent blocking
 * developer machines connected via USB debugging cables or running Expo Go / Dev Clients.
 * 
 * @param actionNameVi Vietnamese descriptive name of the financial operation
 * @throws DeviceSecurityCompromisedException when device is compromised
 */
export async function assertDeviceIntegrityForFinancialAction(
  actionNameVi: string = 'Thao tác tài chính'
): Promise<void> {
  // Chỉ kích hoạt khóa chặn can thiệp Root/Frida trên môi trường Release/Production (!__DEV__)
  // Cho phép thiết bị máy dev cắm cáp debug, máy ảo simulator hoặc Expo Go / Dev Client không bị chặn nhầm
  if (typeof __DEV__ !== 'undefined' && __DEV__) {
    return;
  }

  const result = await checkDeviceIntegrity();

  if (result.isCompromised) {
    const threatDetails = result.reasons.join('; ');
    throw new DeviceSecurityCompromisedException(
      `CẢNH BÁO BẢO MẬT DOANH NGHIỆP: Thiết bị có dấu hiệu can thiệp hệ điều hành (${result.detectedThreats.join(', ')}). ` +
      `Thao tác [${actionNameVi}] đã bị KHÓA TOÀN BỘ để bảo vệ dữ liệu tài chính và số dư quỹ tiền. ` +
      `Chi tiết: ${threatDetails}`,
      result.detectedThreats
    );
  }
}
