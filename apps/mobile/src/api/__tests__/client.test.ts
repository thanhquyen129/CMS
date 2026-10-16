import { getApiBaseUrl, setApiBaseUrl } from "../client";

const mockStore: Record<string, string> = {};

jest.mock("expo-secure-store", () => ({
  getItemAsync: jest.fn(async (key: string) => mockStore[key] ?? null),
  setItemAsync: jest.fn(async (key: string, value: string) => {
    mockStore[key] = value;
  }),
  deleteItemAsync: jest.fn(async (key: string) => {
    delete mockStore[key];
  }),
}));

jest.mock("react-native", () => ({
  Platform: { OS: "android" },
}));

describe("API Base URL Management", () => {
  const originalEnv = process.env;

  beforeEach(() => {
    jest.resetModules();
    for (const k of Object.keys(mockStore)) {
      delete mockStore[k];
    }
    process.env = { ...originalEnv };
  });

  afterAll(() => {
    process.env = originalEnv;
  });

  it("returns default URL when no custom URL is configured", async () => {
    delete process.env.EXPO_PUBLIC_API_URL;
    process.env.NODE_ENV = "development";
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS = "true";

    const url = await getApiBaseUrl();
    expect(url).toBe("https://cms.lsslogistics.vn");
  });

  it("allows setting and getting custom URL in development mode", async () => {
    process.env.NODE_ENV = "development";
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS = "true";

    await setApiBaseUrl("http://192.168.1.50:5000/");
    const url = await getApiBaseUrl();
    expect(url).toBe("http://192.168.1.50:5000");
  });

  it("permits setting canonical production URL in production store mode without throwing", async () => {
    process.env.NODE_ENV = "production";
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS = "false";
    process.env.EXPO_PUBLIC_API_URL = "https://cms.lsslogistics.vn";

    await expect(
      setApiBaseUrl("https://cms.lsslogistics.vn")
    ).resolves.toBeUndefined();
  });

  it("rejects tampering with API Base URL in production store mode", async () => {
    process.env.NODE_ENV = "production";
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS = "false";
    process.env.EXPO_PUBLIC_API_URL = "https://cms.lsslogistics.vn";

    await expect(setApiBaseUrl("https://attacker-proxy.com")).rejects.toThrow(
      "Không được phép thay đổi API Base URL trong môi trường Store Production thương mại."
    );
  });

  it("forces default production URL in production store mode even if storage has legacy custom URL", async () => {
    mockStore["lcms.config.api_base_url"] = "http://dev-server.internal";
    process.env.NODE_ENV = "production";
    process.env.EXPO_PUBLIC_SHOW_DEMO_PERSONAS = "false";
    process.env.EXPO_PUBLIC_API_URL = "https://cms.lsslogistics.vn";

    const url = await getApiBaseUrl();
    expect(url).toBe("https://cms.lsslogistics.vn");
  });
});
