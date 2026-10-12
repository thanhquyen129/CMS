jest.mock('react-native', () => ({
  Platform: { OS: 'android' },
  StyleSheet: { create: (styles: any) => styles },
  Vibration: { vibrate: jest.fn() },
  View: 'View',
  Text: 'Text',
  TouchableOpacity: 'TouchableOpacity',
}));

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
}));

import { formatVndAmount } from '../FinancialNumpad';

describe('FinancialNumpad Logic & Formatting Suite', () => {
  it('formats zero and empty amounts properly', () => {
    expect(formatVndAmount(0)).toBe('0');
    expect(formatVndAmount(NaN)).toBe('0');
  });

  it('formats thousands and millions with standard Vietnamese dot separators', () => {
    expect(formatVndAmount(50000)).toBe('50.000');
    expect(formatVndAmount(100000)).toBe('100.000');
    expect(formatVndAmount(500000)).toBe('500.000');
    expect(formatVndAmount(1000000)).toBe('1.000.000');
    expect(formatVndAmount(1500000)).toBe('1.500.000');
    expect(formatVndAmount(25850000)).toBe('25.850.000');
  });

  it('floors fractional decimals when formatting currency', () => {
    expect(formatVndAmount(1500000.85)).toBe('1.500.000');
  });
});
