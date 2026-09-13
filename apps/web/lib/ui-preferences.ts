export const UI_PREFS_COOKIE = "lcms_ui";
export const UI_PREFS_STORAGE_KEY = "lcms_ui";

export type UiThemeId = "soft-purple" | "invoika" | "classic";
export type UiLayoutId = "vertical" | "horizontal";
export type UiDensityId = "comfortable" | "compact";

/** Allowlisted post-login landing paths only. */
export type UiHomePath =
  | "/dashboard"
  | "/bills"
  | "/documents"
  | "/ap-ar"
  | "/settlements"
  | "/bank-feed"
  | "/queues/exceptions"
  | "/queues/variances"
  | "/queues/approvals"
  | "/queues/reconciliations"
  | "/settings";

export type UiPreferences = {
  theme: UiThemeId;
  layout: UiLayoutId;
  density: UiDensityId;
  homePath: UiHomePath;
  tableZebra: boolean;
  stickyNav: boolean;
  reduceMotion: boolean;
  showNavLabels: boolean;
  showQueues: boolean;
};

export const DEFAULT_UI_PREFERENCES: UiPreferences = {
  theme: "invoika",
  layout: "horizontal",
  density: "comfortable",
  homePath: "/dashboard",
  tableZebra: true,
  stickyNav: true,
  reduceMotion: false,
  showNavLabels: false,
  showQueues: true,
};

export const UI_HOME_OPTIONS: ReadonlyArray<{ id: UiHomePath; label: string }> = [
  { id: "/dashboard", label: "Bảng điều khiển" },
  { id: "/bills", label: "Bill" },
  { id: "/documents", label: "Chứng từ tài chính" },
  { id: "/ap-ar", label: "Phải trả / Phải thu" },
  { id: "/settlements", label: "Thanh toán / Thu tiền" },
  { id: "/bank-feed", label: "Sao kê ngân hàng" },
  { id: "/queues/exceptions", label: "Hàng đợi ngoại lệ" },
  { id: "/queues/variances", label: "Hàng đợi chênh lệch" },
  { id: "/queues/approvals", label: "Hàng đợi phê duyệt" },
  { id: "/queues/reconciliations", label: "Hàng đợi đối soát" },
  { id: "/settings", label: "Cài đặt" },
];

export const UI_THEME_OPTIONS: ReadonlyArray<{
  id: UiThemeId;
  label: string;
  description: string;
  swatches: readonly [string, string, string];
}> = [
  {
    id: "invoika",
    label: "Invoika Soft",
    description: "Teal admin, bố cục ngang — phong cách dashboard hóa đơn.",
    swatches: ["#0ab39c", "#405189", "#f3f6f9"],
  },
  {
    id: "soft-purple",
    label: "Soft Purple",
    description: "Tím soft-UI, sidebar dọc — phù hợp điều khiển tài chính.",
    swatches: ["#5d5fef", "#efeffd", "#f8f9fb"],
  },
  {
    id: "classic",
    label: "Classic CMS",
    description: "Navy / teal gốc CMS — trung tính, tương phản cao.",
    swatches: ["#0b5f4b", "#123047", "#f3f5f7"],
  },
];

export function isUiThemeId(v: unknown): v is UiThemeId {
  return v === "soft-purple" || v === "invoika" || v === "classic";
}

export function isUiLayoutId(v: unknown): v is UiLayoutId {
  return v === "vertical" || v === "horizontal";
}

export function isUiDensityId(v: unknown): v is UiDensityId {
  return v === "comfortable" || v === "compact";
}

export function isUiHomePath(v: unknown): v is UiHomePath {
  return UI_HOME_OPTIONS.some((o) => o.id === v);
}

function asBool(v: unknown, fallback: boolean): boolean {
  if (typeof v === "boolean") return v;
  if (v === "true" || v === 1 || v === "1") return true;
  if (v === "false" || v === 0 || v === "0") return false;
  return fallback;
}

export function parseUiPreferences(raw: string | null | undefined): UiPreferences {
  if (!raw) return { ...DEFAULT_UI_PREFERENCES };
  try {
    const parsed = JSON.parse(raw) as Partial<UiPreferences>;
    return {
      theme: isUiThemeId(parsed.theme) ? parsed.theme : DEFAULT_UI_PREFERENCES.theme,
      layout: isUiLayoutId(parsed.layout) ? parsed.layout : DEFAULT_UI_PREFERENCES.layout,
      density: isUiDensityId(parsed.density) ? parsed.density : DEFAULT_UI_PREFERENCES.density,
      homePath: isUiHomePath(parsed.homePath) ? parsed.homePath : DEFAULT_UI_PREFERENCES.homePath,
      tableZebra: asBool(parsed.tableZebra, DEFAULT_UI_PREFERENCES.tableZebra),
      stickyNav: asBool(parsed.stickyNav, DEFAULT_UI_PREFERENCES.stickyNav),
      reduceMotion: asBool(parsed.reduceMotion, DEFAULT_UI_PREFERENCES.reduceMotion),
      showNavLabels: asBool(parsed.showNavLabels, DEFAULT_UI_PREFERENCES.showNavLabels),
      showQueues: asBool(parsed.showQueues, DEFAULT_UI_PREFERENCES.showQueues),
    };
  } catch {
    return { ...DEFAULT_UI_PREFERENCES };
  }
}

export function applyUiPreferencesToDocument(prefs: UiPreferences): void {
  if (typeof document === "undefined") return;
  const root = document.documentElement;
  root.setAttribute("data-theme", prefs.theme);
  root.setAttribute("data-layout", prefs.layout);
  root.setAttribute("data-density", prefs.density);
  root.setAttribute("data-zebra", prefs.tableZebra ? "1" : "0");
  root.setAttribute("data-sticky-nav", prefs.stickyNav ? "1" : "0");
  root.setAttribute("data-reduce-motion", prefs.reduceMotion ? "1" : "0");
  root.setAttribute("data-nav-labels", prefs.showNavLabels ? "1" : "0");
  root.setAttribute("data-show-queues", prefs.showQueues ? "1" : "0");
}

export function persistUiPreferences(prefs: UiPreferences): void {
  const payload = JSON.stringify(prefs);
  try {
    localStorage.setItem(UI_PREFS_STORAGE_KEY, payload);
  } catch {
    /* ignore quota / private mode */
  }
  const maxAge = 60 * 60 * 24 * 365;
  document.cookie = `${UI_PREFS_COOKIE}=${encodeURIComponent(payload)}; Path=/; Max-Age=${maxAge}; SameSite=Lax`;
  applyUiPreferencesToDocument(prefs);
}

export function clearUiPreferences(): void {
  try {
    localStorage.removeItem(UI_PREFS_STORAGE_KEY);
  } catch {
    /* ignore */
  }
  document.cookie = `${UI_PREFS_COOKIE}=; Path=/; Max-Age=0; SameSite=Lax`;
  persistUiPreferences({ ...DEFAULT_UI_PREFERENCES });
}

export function readUiPreferencesClient(): UiPreferences {
  try {
    const fromStore = localStorage.getItem(UI_PREFS_STORAGE_KEY);
    if (fromStore) return parseUiPreferences(fromStore);
  } catch {
    /* ignore */
  }
  if (typeof document !== "undefined") {
    const match = document.cookie.match(new RegExp(`(?:^|; )${UI_PREFS_COOKIE}=([^;]*)`));
    if (match?.[1]) return parseUiPreferences(decodeURIComponent(match[1]));
  }
  return { ...DEFAULT_UI_PREFERENCES };
}

/** Inline boot script — prevent FOUC before React hydrates. */
export function uiPreferencesBootScript(): string {
  const cookie = UI_PREFS_COOKIE;
  const storage = UI_PREFS_STORAGE_KEY;
  const def = JSON.stringify(DEFAULT_UI_PREFERENCES);
  return `(function(){try{var d=${def},p=null;try{p=localStorage.getItem(${JSON.stringify(storage)});}catch(e){}if(!p){var m=document.cookie.match(/(?:^|; )${cookie}=([^;]*)/);if(m)p=decodeURIComponent(m[1]);}if(p){try{var o=JSON.parse(p);for(var k in o){if(Object.prototype.hasOwnProperty.call(d,k))d[k]=o[k];}}catch(e){}}var r=document.documentElement;r.setAttribute("data-theme",d.theme);r.setAttribute("data-layout",d.layout);r.setAttribute("data-density",d.density);r.setAttribute("data-zebra",d.tableZebra?"1":"0");r.setAttribute("data-sticky-nav",d.stickyNav?"1":"0");r.setAttribute("data-reduce-motion",d.reduceMotion?"1":"0");r.setAttribute("data-nav-labels",d.showNavLabels?"1":"0");r.setAttribute("data-show-queues",d.showQueues?"1":"0");}catch(e){}})();`;
}
