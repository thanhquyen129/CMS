/**
 * Financial Numpad for LCMS Mobile (Field Operations / Truckers)
 * 
 * Specially engineered for rapid on-the-road expense & cash handling:
 * - Oversized touch targets (64x64 dp) for gloved hands under extreme outdoor conditions.
 * - 4 one-tap quick-add denomination buttons: [+50k], [+100k], [+500k], [+1M].
 * - Instant Vietnamese currency formatting with thousand separators (e.g., 1.500.000 ₫).
 * - High-contrast palette compatible with Outdoor Mode (#000000 base, #FACC15 text).
 */

import React, { useState, useCallback } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Vibration,
  Platform,
} from 'react-native';
import { HIGH_CONTRAST_THEME, STANDARD_DARK_THEME } from '../theme/HighContrastTheme';

export interface FinancialNumpadProps {
  initialAmount?: number;
  maxAmount?: number; // Default cap: 500,000,000 VND
  onAmountChange?: (amount: number) => void;
  onConfirm?: (amount: number) => void;
  onCancel?: () => void;
  title?: string;
  currency?: string;
  outdoorMode?: boolean;
}

export function formatVndAmount(value: number): string {
  if (!value || isNaN(value)) return '0';
  return Math.floor(value)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, '.');
}

export const FinancialNumpad: React.FC<FinancialNumpadProps> = ({
  initialAmount = 0,
  maxAmount = 500_000_000,
  onAmountChange,
  onConfirm,
  onCancel,
  title = 'Nhập số tiền chi phí',
  currency = '₫',
  outdoorMode = true,
}) => {
  const [amountStr, setAmountStr] = useState<string>(
    initialAmount > 0 ? Math.floor(initialAmount).toString() : '0'
  );

  const theme = outdoorMode ? HIGH_CONTRAST_THEME : STANDARD_DARK_THEME;
  const currentAmount = parseInt(amountStr, 10) || 0;

  const triggerHaptic = () => {
    if (Platform.OS !== 'web') {
      try {
        Vibration.vibrate(10);
      } catch {
        // Ignore if unsupported
      }
    }
  };

  const updateAmount = useCallback(
    (newStr: string) => {
      // Remove leading zeros unless it's just '0'
      let sanitized = newStr.replace(/^0+/, '');
      if (sanitized === '') sanitized = '0';

      const numVal = parseInt(sanitized, 10) || 0;
      if (numVal > maxAmount) {
        return; // Exceeds cap
      }

      setAmountStr(sanitized);
      triggerHaptic();
      if (onAmountChange) {
        onAmountChange(numVal);
      }
    },
    [maxAmount, onAmountChange]
  );

  const handleDigitPress = (digit: string) => {
    if (amountStr === '0') {
      updateAmount(digit);
    } else {
      updateAmount(amountStr + digit);
    }
  };

  const handleTripleZero = () => {
    if (amountStr === '0') return;
    updateAmount(amountStr + '000');
  };

  const handleBackspace = () => {
    if (amountStr.length <= 1 || amountStr === '0') {
      updateAmount('0');
    } else {
      updateAmount(amountStr.slice(0, -1));
    }
  };

  const handleClear = () => {
    updateAmount('0');
  };

  const handleQuickAdd = (addValue: number) => {
    const nextVal = currentAmount + addValue;
    if (nextVal <= maxAmount) {
      updateAmount(nextVal.toString());
    }
  };

  const handleConfirmPress = () => {
    triggerHaptic();
    if (onConfirm) {
      onConfirm(currentAmount);
    }
  };

  return (
    <View style={[styles.container, { backgroundColor: theme.colors.background }]}>
      {/* Title & Display Area */}
      <View style={[styles.displayCard, { borderColor: theme.colors.border }]}>
        <Text style={[styles.displayTitle, { color: theme.colors.textMuted }]}>
          {title.toUpperCase()}
        </Text>
        <View style={styles.amountRow}>
          <Text
            style={[styles.amountValue, { color: theme.colors.textPrimary }]}
            numberOfLines={1}
            adjustsFontSizeToFit
          >
            {formatVndAmount(currentAmount)}
          </Text>
          <Text style={[styles.currencyLabel, { color: theme.colors.textPrimary }]}>
            {currency}
          </Text>
        </View>
      </View>

      {/* Quick Add Row: [+50k], [+100k], [+500k], [+1M] */}
      <View style={styles.quickAddRow}>
        <TouchableOpacity
          style={[styles.quickAddBtn, { borderColor: theme.colors.border }]}
          onPress={() => handleQuickAdd(50_000)}
          activeOpacity={0.7}
        >
          <Text style={[styles.quickAddText, { color: theme.colors.textPrimary }]}>+50k</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.quickAddBtn, { borderColor: theme.colors.border }]}
          onPress={() => handleQuickAdd(100_000)}
          activeOpacity={0.7}
        >
          <Text style={[styles.quickAddText, { color: theme.colors.textPrimary }]}>+100k</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.quickAddBtn, { borderColor: theme.colors.border }]}
          onPress={() => handleQuickAdd(500_000)}
          activeOpacity={0.7}
        >
          <Text style={[styles.quickAddText, { color: theme.colors.textPrimary }]}>+500k</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.quickAddBtn, { borderColor: theme.colors.border }]}
          onPress={() => handleQuickAdd(1_000_000)}
          activeOpacity={0.7}
        >
          <Text style={[styles.quickAddText, { color: theme.colors.textPrimary }]}>+1M</Text>
        </TouchableOpacity>
      </View>

      {/* 4x3 Oversized Keypad Grid (Buttons 64x64 dp min) */}
      <View style={styles.grid}>
        {/* Row 1 */}
        <View style={styles.gridRow}>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('1')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>1</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('2')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>2</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('3')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>3</Text>
          </TouchableOpacity>
        </View>

        {/* Row 2 */}
        <View style={styles.gridRow}>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('4')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>4</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('5')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>5</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('6')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>6</Text>
          </TouchableOpacity>
        </View>

        {/* Row 3 */}
        <View style={styles.gridRow}>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('7')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>7</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('8')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>8</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('9')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>9</Text>
          </TouchableOpacity>
        </View>

        {/* Row 4: 000, 0, Backspace */}
        <View style={styles.gridRow}>
          <TouchableOpacity
            style={[styles.numBtn, styles.specialBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={handleTripleZero}
          >
            <Text style={[styles.specialText, { color: theme.colors.textPrimary }]}>000</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={() => handleDigitPress('0')}
          >
            <Text style={[styles.numText, { color: theme.colors.textSecondary }]}>0</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.numBtn, styles.specialBtn, { borderColor: theme.colors.borderMuted }]}
            onPress={handleBackspace}
            onLongPress={handleClear}
          >
            <Text style={[styles.backspaceText, { color: theme.colors.danger }]}>⌫</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Action Row: Cancel / Confirm */}
      <View style={styles.actionRow}>
        {onCancel && (
          <TouchableOpacity
            style={[
              styles.actionBtn,
              styles.cancelBtn,
              { borderColor: theme.colors.borderMuted },
            ]}
            onPress={onCancel}
          >
            <Text style={[styles.cancelBtnText, { color: theme.colors.textMuted }]}>
              HỦY BỎ
            </Text>
          </TouchableOpacity>
        )}
        <TouchableOpacity
          style={[
            styles.actionBtn,
            styles.confirmBtn,
            { backgroundColor: theme.colors.actionPrimary },
          ]}
          onPress={handleConfirmPress}
        >
          <Text style={[styles.confirmBtnText, { color: theme.colors.actionPrimaryText }]}>
            XÁC NHẬN SỐ TIỀN
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    padding: 16,
    width: '100%',
  },
  displayCard: {
    backgroundColor: '#0a0a0a',
    borderRadius: 12,
    borderWidth: 2.5,
    padding: 16,
    alignItems: 'center',
    marginBottom: 16,
  },
  displayTitle: {
    fontSize: 13,
    fontWeight: '700',
    letterSpacing: 1,
    marginBottom: 6,
  },
  amountRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'center',
  },
  amountValue: {
    fontSize: 34,
    fontWeight: '900',
    letterSpacing: 1.5,
  },
  currencyLabel: {
    fontSize: 22,
    fontWeight: '800',
    marginLeft: 6,
  },
  quickAddRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 14,
    gap: 8,
  },
  quickAddBtn: {
    flex: 1,
    height: 48, // >= 48dp touch target
    backgroundColor: '#121212',
    borderWidth: 2,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
  },
  quickAddText: {
    fontSize: 16,
    fontWeight: '800',
  },
  grid: {
    gap: 10,
    marginBottom: 16,
  },
  gridRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 10,
  },
  numBtn: {
    flex: 1,
    minHeight: 64, // >= 64x64 dp requirement
    backgroundColor: '#171717',
    borderWidth: 2,
    borderRadius: 10,
    justifyContent: 'center',
    alignItems: 'center',
  },
  specialBtn: {
    backgroundColor: '#262626',
  },
  numText: {
    fontSize: 26,
    fontWeight: '800',
  },
  specialText: {
    fontSize: 19,
    fontWeight: '800',
  },
  backspaceText: {
    fontSize: 24,
    fontWeight: '900',
  },
  actionRow: {
    flexDirection: 'row',
    gap: 12,
  },
  actionBtn: {
    minHeight: 56, // >= 48dp standard
    borderRadius: 10,
    justifyContent: 'center',
    alignItems: 'center',
  },
  cancelBtn: {
    flex: 1,
    backgroundColor: '#171717',
    borderWidth: 2,
  },
  cancelBtnText: {
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  confirmBtn: {
    flex: 2,
  },
  confirmBtnText: {
    fontSize: 16,
    fontWeight: '900',
    letterSpacing: 1,
  },
});
