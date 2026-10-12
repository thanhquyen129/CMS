jest.mock('react-native', () => ({
  Platform: { OS: 'android' },
}));

jest.mock('expo-file-system/legacy', () => ({
  getInfoAsync: jest.fn(),
}));

import {
  checkDeviceIntegrity,
  assertDeviceIntegrityForFinancialAction,
  DeviceSecurityCompromisedException,
} from '../DeviceIntegrity';
import * as FileSystem from 'expo-file-system/legacy';
import { Platform } from 'react-native';

describe('DeviceIntegrity Security Suite', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    delete (global as any).frida;
    delete (global as any).__frida;
    Platform.OS = 'android';
  });

  it('passes integrity check on normal clean devices', async () => {
    (FileSystem.getInfoAsync as jest.Mock).mockResolvedValue({ exists: false });

    const result = await checkDeviceIntegrity();
    expect(result.isCompromised).toBe(false);
    expect(result.detectedThreats).toEqual([]);
    await expect(assertDeviceIntegrityForFinancialAction('Duyệt chi')).resolves.not.toThrow();
  });

  it('detects Frida global injection artifacts and throws on financial actions', async () => {
    (global as any).__frida = true;

    const result = await checkDeviceIntegrity();
    expect(result.isCompromised).toBe(true);
    expect(result.detectedThreats).toContain('FRIDA');

    await expect(assertDeviceIntegrityForFinancialAction('Phê duyệt phiếu chi')).rejects.toThrow(
      DeviceSecurityCompromisedException
    );
  });

  it('detects rooted Android binary if su exists', async () => {
    (FileSystem.getInfoAsync as jest.Mock).mockImplementation((path: string) => {
      if (path === '/system/bin/su' || path === '/data/adb/magisk') {
        return Promise.resolve({ exists: true });
      }
      return Promise.resolve({ exists: false });
    });

    const result = await checkDeviceIntegrity();
    expect(result.isCompromised).toBe(true);
    expect(result.detectedThreats).toContain('ROOT');

    await expect(assertDeviceIntegrityForFinancialAction('Chuyển trạng thái chi phí')).rejects.toThrow(
      'CẢNH BÁO BẢO MẬT DOANH NGHIỆP'
    );
  });

  it('bypasses blocking in __DEV__ mode so developers on debug cables are not falsely blocked', async () => {
    (global as any).__frida = true;
    (global as any).__DEV__ = true;

    // In __DEV__ mode, assertDeviceIntegrityForFinancialAction should resolve without throwing
    await expect(assertDeviceIntegrityForFinancialAction('Thao tác debug')).resolves.not.toThrow();

    delete (global as any).__DEV__;
  });
});

