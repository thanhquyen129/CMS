/**
 * High-Contrast Outdoor Theme for LCMS Mobile (Field Operations / Truckers)
 * 
 * Specially engineered for extreme sunlight visibility (80,000 - 100,000 lux):
 * - Deep OLED True Black (#000000) base to minimize solar reflection glare.
 * - High-contrast Cyber Lemon Yellow (#FACC15) & Solid White (#FFFFFF) text.
 * - Thick, high-contrast borders (2.5dp - 3.5dp).
 * - Minimum touch target >= 48x48 dp (up to 64x64 dp) for thick working gloves.
 */

import React, { createContext, useContext, useState, useEffect } from 'react';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

export interface ThemeColors {
  background: string;
  surface: string;
  surfaceElevated: string;
  textPrimary: string;
  textSecondary: string;
  textMuted: string;
  border: string;
  borderMuted: string;
  actionPrimary: string;
  actionPrimaryText: string;
  actionSecondary: string;
  actionSecondaryText: string;
  success: string;
  warning: string;
  danger: string;
  accent: string;
}

export interface ThemeMetrics {
  minTouchTarget: number;
  largeTouchTarget: number;
  borderWidthThick: number;
  borderWidthExtra: number;
  borderRadius: number;
}

export interface AppTheme {
  isOutdoorMode: boolean;
  colors: ThemeColors;
  metrics: ThemeMetrics;
}

export const HIGH_CONTRAST_THEME: AppTheme = {
  isOutdoorMode: true,
  colors: {
    background: '#000000',
    surface: '#0A0A0A',
    surfaceElevated: '#171717',
    textPrimary: '#FACC15', // High-visibility Lemon Yellow (peak human eye sensitivity at daylight)
    textSecondary: '#FFFFFF', // Solid Pure White
    textMuted: '#E4E4E7', // Crisp off-white
    border: '#FACC15', // Thick Yellow Border
    borderMuted: '#71717A',
    actionPrimary: '#FACC15',
    actionPrimaryText: '#000000',
    actionSecondary: '#27272A',
    actionSecondaryText: '#FACC15',
    success: '#4ADE80', // High-contrast Neon Green
    warning: '#FB923C', // High-contrast Neon Orange
    danger: '#F87171', // High-contrast Bright Red
    accent: '#38BDF8', // Cyan Accent
  },
  metrics: {
    minTouchTarget: 48,
    largeTouchTarget: 64,
    borderWidthThick: 2.5,
    borderWidthExtra: 3.5,
    borderRadius: 8,
  },
};

export const STANDARD_DARK_THEME: AppTheme = {
  isOutdoorMode: false,
  colors: {
    background: '#0F172A',
    surface: '#1E293B',
    surfaceElevated: '#334155',
    textPrimary: '#F8FAFC',
    textSecondary: '#94A3B8',
    textMuted: '#64748B',
    border: '#334155',
    borderMuted: '#1E293B',
    actionPrimary: '#0284C7',
    actionPrimaryText: '#FFFFFF',
    actionSecondary: '#334155',
    actionSecondaryText: '#F8FAFC',
    success: '#10B981',
    warning: '#F59E0B',
    danger: '#EF4444',
    accent: '#38BDF8',
  },
  metrics: {
    minTouchTarget: 48,
    largeTouchTarget: 56,
    borderWidthThick: 1,
    borderWidthExtra: 2,
    borderRadius: 10,
  },
};

interface OutdoorThemeContextValue {
  theme: AppTheme;
  isOutdoorMode: boolean;
  toggleOutdoorMode: () => void;
  setOutdoorMode: (enabled: boolean) => void;
}

const OutdoorThemeContext = createContext<OutdoorThemeContextValue>({
  theme: HIGH_CONTRAST_THEME,
  isOutdoorMode: true,
  toggleOutdoorMode: () => {},
  setOutdoorMode: () => {},
});

const THEME_STORAGE_KEY = 'lcms_mobile_outdoor_mode';

export const OutdoorThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [isOutdoorMode, setIsOutdoorMode] = useState<boolean>(true);

  useEffect(() => {
    (async () => {
      try {
        if (Platform.OS !== 'web') {
          const stored = await SecureStore.getItemAsync(THEME_STORAGE_KEY);
          if (stored !== null) {
            setIsOutdoorMode(stored === 'true');
          }
        }
      } catch {
        // Fallback to outdoor mode
      }
    })();
  }, []);

  const handleSetMode = (enabled: boolean) => {
    setIsOutdoorMode(enabled);
    if (Platform.OS !== 'web') {
      SecureStore.setItemAsync(THEME_STORAGE_KEY, enabled.toString()).catch(() => {});
    }
  };

  const handleToggle = () => {
    handleSetMode(!isOutdoorMode);
  };

  const theme = isOutdoorMode ? HIGH_CONTRAST_THEME : STANDARD_DARK_THEME;

  return React.createElement(
    OutdoorThemeContext.Provider,
    {
      value: {
        theme,
        isOutdoorMode,
        toggleOutdoorMode: handleToggle,
        setOutdoorMode: handleSetMode,
      },
    },
    children
  );
};

export function useOutdoorTheme(): OutdoorThemeContextValue {
  return useContext(OutdoorThemeContext);
}
