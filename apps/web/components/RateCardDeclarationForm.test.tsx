import { readFileSync } from "node:fs";
import path from "node:path";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RateCardDeclarationForm } from "@/components/RateCardDeclarationForm";
import { amountFieldLabel, rateTransportLabel, routeOptionLabel, weightBandLabel } from "@/lib/rate-cards";

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => <a href={href}>{children}</a>,
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: () => undefined, refresh: () => undefined }),
}));

vi.mock("@/components/PartyTypeahead", () => ({
  PartyTypeahead: () => <input aria-label="Đối tác" />,
}));

const routes = [
  {
    id: "route-1",
    code: "VN-MY",
    name: "Sài Gòn đi Hà Nội",
    originCode: "SGN",
    destinationCode: "HAN",
    originLocationId: "o",
    destinationLocationId: "d",
    isActive: true,
    stops: [],
  },
  {
    id: "route-old",
    code: "OLD",
    name: "Tuyến cũ",
    originCode: "DAD",
    destinationCode: "PQC",
    originLocationId: "o2",
    destinationLocationId: "d2",
    isActive: false,
    stops: [],
  },
];

const currencies = [
  { code: "VND", name: "Đồng", isActive: true },
  { code: "USD", name: "Đô la Mỹ", isActive: true },
];

function renderForm(extra: Record<string, unknown> = {}) {
  return render(
    <RateCardDeclarationForm
      defaultCurrency="VND"
      currencies={currencies}
      routes={routes}
      {...extra}
    />
  );
}

describe("rate card declaration form", () => {
  afterEach(() => cleanup());

  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: true,
        status: 201,
        json: async () => ({ id: "card-1", rateCardId: "card-1" }),
      }))
    );
  });

  it("shows the route as origin, destination, and code", async () => {
    const user = userEvent.setup();
    renderForm();
    expect(routeOptionLabel({ code: "VN-MY", originCode: "SGN", destinationCode: "HAN" })).toBe("SGN → HAN (VN-MY)");
    expect(weightBandLabel(0, 5)).toBe("< 5");
    expect(weightBandLabel(5, 11)).toBe("5 – < 11");
    expect(weightBandLabel(10.999, 11)).toBe("10.999 – < 11");
    expect(weightBandLabel(21, null)).toBe("≥ 21 kg");
    await user.type(screen.getByLabelText("Tuyến 1"), "HAN");
    expect(screen.getByRole("option", { name: "SGN → HAN (VN-MY)" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: /OLD/ })).toBeNull();
    expect(screen.queryByLabelText("Hãng vận chuyển")).toBeNull();
  });

  it("does not invent a route when none are active", () => {
    renderForm({ routes: [] });
    expect(screen.getByText(/Không tạo tuyến giả/)).toBeTruthy();
  });

  it("warns when the saved route is inactive", () => {
    renderForm({ routeId: "route-old", routeCode: "OLD" });
    expect(screen.getByText(/Tuyến đã lưu ngừng hiệu lực/)).toBeTruthy();
  });

  it("offers ISO currencies and keeps the stored currency on edit", () => {
    renderForm({ cardId: "card-1", code: "RC-1", currencyCode: "EUR", name: "Giữ tiền" });
    const select = screen.getByLabelText("Tiền tệ") as HTMLSelectElement;
    expect(Array.from(select.options).map((o) => o.value)).toEqual(expect.arrayContaining(["USD", "EUR"]));
    expect(select.value).toBe("EUR");
  });

  it("switches an empty form without a warning", async () => {
    const user = userEvent.setup();
    renderForm();
    await user.click(screen.getByRole("radio", { name: /Bảng giá nâng cao/ }));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect((screen.getByRole("radio", { name: /Bảng giá nâng cao/ }) as HTMLInputElement).checked).toBe(true);
  });

  it("opens an advanced draft on the advanced tab", () => {
    renderForm({ cardId: "card-1", code: "RC-1", name: "Nâng cao", declarationMode: "advanced" });
    expect((screen.getByRole("radio", { name: /Bảng giá nâng cao/ }) as HTMLInputElement).checked).toBe(true);
    expect(screen.getByRole("button", { name: "Khung hàng không" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "+ Thêm quy tắc giá" })).toBeNull();
  });

  it("asks before dropping basic rules, and cancel keeps them", async () => {
    const user = userEvent.setup();
    renderForm();
    await user.type(screen.getByLabelText("Tên quy tắc 1"), "FRT");
    await user.click(screen.getByRole("radio", { name: /Bảng giá nâng cao/ }));
    expect(screen.getByRole("dialog")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Hủy" }));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect((screen.getByLabelText("Tên quy tắc 1") as HTMLInputElement).value).toBe("FRT");
    expect((screen.getByRole("radio", { name: /Bảng giá cơ bản/ }) as HTMLInputElement).checked).toBe(true);
  });

  it("confirm clears the other shape and the payload has one mode", async () => {
    const user = userEvent.setup();
    renderForm();
    await user.type(screen.getByLabelText("Mã"), "RC-UI");
    await user.type(screen.getByLabelText("Tên"), "Bảng giao diện");
    await user.selectOptions(screen.getByRole("combobox", { name: "Chọn tuyến 1" }), "route-1");
    await user.type(screen.getByLabelText("Tên quy tắc 1"), "Cước");
    await user.type(screen.getByLabelText("Số tiền cố định 1"), "10");
    await user.click(screen.getByRole("radio", { name: /Bảng giá nâng cao/ }));
    await user.click(screen.getByRole("button", { name: "Đồng ý" }));
    expect(screen.queryByLabelText("Mã quy tắc 1")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    const fetchMock = fetch as unknown as { mock: { calls: unknown[][] } };
    const init = fetchMock.mock.calls.at(-1)?.[1] as RequestInit;
    const body = JSON.parse(String(init.body));
    expect(body.declarationMode).toBe("advanced");
    expect(body.rules).toEqual([]);
    expect(body.columns.length).toBeGreaterThan(0);
    expect(body.bands.length).toBeGreaterThan(0);
    expect(body.routeId).toBeNull();
    expect(body.groups[0].routeId).toBe("route-1");
  }, 20000);

  it("labels a fixed amount and a unit rate differently", async () => {
    const user = userEvent.setup();
    renderForm();
    expect(amountFieldLabel("fixed")).toBe("Số tiền cố định");
    expect(screen.getByLabelText("Số tiền cố định 1")).toBeTruthy();
    await user.selectOptions(screen.getByLabelText("Cách tính 1"), "unit_rate");
    expect(amountFieldLabel("unit_rate")).toBe("Đơn giá (theo số lượng cước)");
    expect(screen.getByLabelText("Đơn giá (theo số lượng cước) 1")).toBeTruthy();
  });

  it("shows the API safe message and hides technical faults", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: false,
        status: 409,
        json: async () => ({
          code: "invalid_declaration_mode",
          message: "Một bảng giá chỉ dùng một hình thức khai báo. Không gửi đồng thời quy tắc giá và ma trận khung giá.",
          correlationId: "corr-ui-01",
        }),
      }))
    );
    const user = userEvent.setup();
    renderForm();
    await user.type(screen.getByLabelText("Mã"), "RC-ERR");
    await user.type(screen.getByLabelText("Tên"), "Lỗi");
    await user.selectOptions(screen.getByRole("combobox", { name: "Chọn tuyến 1" }), "route-1");
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    expect(screen.getByRole("alert").textContent).toContain("một hình thức khai báo");
    expect(screen.getByRole("alert").textContent).not.toContain("Exception");
    expect(screen.getByRole("alert").textContent).not.toContain("invalid_declaration_mode");
  });
});

describe("UAT RATE-UI-05..07", () => {
  afterEach(() => cleanup());

  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({ ok: true, status: 201, json: async () => ({ id: "card-1", rateCardId: "card-1" }) }))
    );
  });

  const chargeItems = [
    { id: "ct-freight", code: "FREIGHT", name: "Cước vận tải", isActive: true },
    { id: "ct-pickup", code: "PICKUP", name: "Phí lấy hàng", isActive: true },
  ];

  async function fillHeader(user: ReturnType<typeof userEvent.setup>, code: string) {
    await user.type(screen.getByLabelText("Mã"), code);
    await user.type(screen.getByLabelText("Tên"), "Bảng UAT");
    await user.selectOptions(screen.getByRole("combobox", { name: "Chọn tuyến 1" }), "route-1");
    await user.type(screen.getByLabelText("Tên quy tắc 1"), "Cước");
    await user.type(screen.getByLabelText("Số tiền cố định 1"), "10");
  }

  function lastBody() {
    const fetchMock = fetch as unknown as { mock: { calls: unknown[][] } };
    const init = fetchMock.mock.calls.at(-1)?.[1] as RequestInit;
    return JSON.parse(String(init.body));
  }

  it("UAT-01/05: no rule-level price direction in basic or advanced; payload inherits the header", async () => {
    const user = userEvent.setup();
    renderForm({ chargeItems, partyType: "customer" });
    expect(screen.queryByLabelText(/Lọc chiều giá/)).toBeNull();
    expect(screen.queryByText("Mọi chiều giá")).toBeNull();
    await fillHeader(user, "RC-U01");
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    const body = lastBody();
    expect(body.partyType).toBe("customer");
    expect(body.rules[0]).not.toHaveProperty("partyTypeCode");
    cleanup();
    renderForm({ chargeItems, declarationMode: "advanced", cardId: "card-1", code: "RC-A", name: "Nâng cao" });
    expect(screen.queryByLabelText(/Lọc chiều giá/)).toBeNull();
    expect(screen.queryByText("Mọi chiều giá")).toBeNull();
  }, 20000);

  it("UAT-06: Không lọc nấc disables and clears Từ/Đến with a notice, payload sends three nulls", async () => {
    const user = userEvent.setup();
    renderForm({ chargeItems });
    const basis = screen.getByLabelText("Điều kiện trọng lượng 1") as HTMLSelectElement;
    expect(Array.from(basis.options).map((o) => o.textContent)).toEqual([
      "Không lọc nấc",
      "Trọng lượng thực (Gross)",
      "Trọng lượng tính cước (Chargeable)",
    ]);
    const from = screen.getByLabelText("Trọng lượng từ 1") as HTMLInputElement;
    const to = screen.getByLabelText("Trọng lượng đến 1") as HTMLInputElement;
    expect(from.disabled).toBe(true);
    expect(to.disabled).toBe(true);
    await user.selectOptions(basis, "gross");
    expect(from.disabled).toBe(false);
    await user.type(from, "0");
    await user.type(to, "5");
    await user.selectOptions(basis, "");
    expect(from.value).toBe("");
    expect(to.value).toBe("");
    expect(from.disabled).toBe(true);
    expect(screen.getByText(/giá trị Từ – Đến đã nhập được xóa/)).toBeTruthy();
    await fillHeader(user, "RC-U06");
    await user.selectOptions(screen.getByLabelText("Khoản giá 1"), "ct-freight");
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    const rule = lastBody().rules[0];
    expect(rule.weightBasis).toBeNull();
    expect(rule.weightMin).toBeNull();
    expect(rule.weightMax).toBeNull();
  }, 20000);

  it("UAT-10: gross needs both bounds and Đến > Từ before any request", async () => {
    const user = userEvent.setup();
    renderForm({ chargeItems });
    await fillHeader(user, "RC-U10");
    await user.selectOptions(screen.getByLabelText("Điều kiện trọng lượng 1"), "gross");
    await user.type(screen.getByLabelText("Trọng lượng từ 1"), "0");
    const fetchMock = fetch as unknown as { mock: { calls: unknown[][] } };
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    expect(screen.getByRole("alert").textContent).toContain("nhập đủ Từ và Đến");
    await user.clear(screen.getByLabelText("Trọng lượng từ 1"));
    await user.type(screen.getByLabelText("Trọng lượng từ 1"), "10");
    await user.type(screen.getByLabelText("Trọng lượng đến 1"), "5");
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    expect(screen.getByRole("alert").textContent).toContain("Đến phải lớn hơn Từ");
    expect(fetchMock.mock.calls.length).toBe(0);
  }, 20000);

  it("UAT-13/16: charge item comes from master data; no free charge code; draft without it warns", async () => {
    const user = userEvent.setup();
    renderForm({ chargeItems });
    expect(screen.queryByLabelText(/Nhóm cạnh tranh/)).toBeNull();
    const charge = screen.getByLabelText("Khoản giá 1") as HTMLSelectElement;
    expect(charge.tagName).toBe("SELECT");
    expect(Array.from(charge.options).map((o) => o.textContent)).toEqual(["Chọn khoản giá", "Cước vận tải", "Phí lấy hàng"]);
    await fillHeader(user, "RC-U16");
    expect(screen.getByText(/Có thể lưu nháp; chưa phát hành được/)).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    let rule = lastBody().rules[0];
    expect(rule.economicChargeTypeId).toBeNull();
    expect(rule).not.toHaveProperty("chargeCode");
    await user.selectOptions(charge, "ct-freight");
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    rule = lastBody().rules[0];
    expect(rule.economicChargeTypeId).toBe("ct-freight");
    expect(rule).not.toHaveProperty("chargeCode");
  }, 20000);

  it("UAT-16: an empty charge catalog says so instead of inventing a code", () => {
    renderForm({ chargeItems: [] });
    const charge = screen.getByLabelText("Khoản giá 1") as HTMLSelectElement;
    expect(Array.from(charge.options).map((o) => o.textContent)).toEqual(["Chưa có danh mục khoản giá"]);
  });
});

describe("rate card detail contract", () => {
  it("is read-only and offers edit only through the draft link", () => {
    const src = readFileSync(path.join(process.cwd(), "app/rate-cards/[id]/page.tsx"), "utf8");
    expect(src).not.toContain("Thêm quy tắc");
    expect(src).not.toContain("AddPricingRuleForm");
    expect(src).not.toContain("ComposeTariffForm");
    expect(src).toContain("isDraftVersion");
    expect(src).toContain("draftVersion ? (");
    expect(src).toContain("Chỉnh sửa bảng giá");
    expect(src).toContain("Phiên bản đã phát hành không sửa được");
    expect(src).toContain("rateTransportLabel");
    expect(rateTransportLabel("air")).toBe("Hàng không");
    expect(rateTransportLabel("ROAD_X")).toBe("Phương thức khác");
  });
});
