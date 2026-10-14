import { readFileSync } from "node:fs";
import path from "node:path";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RateCardDeclarationForm } from "@/components/RateCardDeclarationForm";
import { amountFieldLabel, rateTransportLabel, routeOptionLabel } from "@/lib/rate-cards";

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
    await user.type(screen.getByLabelText("Tuyến"), "HAN");
    expect(screen.getByRole("option", { name: "SGN → HAN (VN-MY)" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: /OLD/ })).toBeNull();
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
    await user.type(screen.getByLabelText("Mã quy tắc 1"), "FRT");
    await user.click(screen.getByRole("radio", { name: /Bảng giá nâng cao/ }));
    expect(screen.getByRole("dialog")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Hủy" }));
    expect(screen.queryByRole("dialog")).toBeNull();
    expect((screen.getByLabelText("Mã quy tắc 1") as HTMLInputElement).value).toBe("FRT");
    expect((screen.getByRole("radio", { name: /Bảng giá cơ bản/ }) as HTMLInputElement).checked).toBe(true);
  });

  it("confirm clears the other shape and the payload has one mode", async () => {
    const user = userEvent.setup();
    renderForm();
    await user.type(screen.getByLabelText("Mã"), "RC-UI");
    await user.type(screen.getByLabelText("Tên"), "Bảng giao diện");
    await user.selectOptions(screen.getByLabelText("Tuyến").parentElement!.querySelector("select")!, "route-1");
    await user.type(screen.getByLabelText("Mã quy tắc 1"), "FRT");
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
    expect(body.routeId).toBe("route-1");
  });

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
    await user.selectOptions(screen.getByLabelText("Tuyến").parentElement!.querySelector("select")!, "route-1");
    await user.click(screen.getByRole("button", { name: "Lưu nháp" }));
    expect(screen.getByRole("alert").textContent).toContain("một hình thức khai báo");
    expect(screen.getByRole("alert").textContent).not.toContain("Exception");
    expect(screen.getByRole("alert").textContent).not.toContain("invalid_declaration_mode");
  });
});

describe("rate card detail contract", () => {
  it("is read-only and offers edit only through the draft link", () => {
    const src = readFileSync(path.join(process.cwd(), "app/rate-cards/[id]/page.tsx"), "utf8");
    expect(src).not.toContain("Thêm quy tắc");
    expect(src).not.toContain("AddPricingRuleForm");
    expect(src).not.toContain("ComposeTariffForm");
    expect(src).toContain("isDraftVersion");
    expect(src).toContain("Chỉnh sửa bảng giá");
    expect(src).toContain("rateTransportLabel");
    expect(rateTransportLabel("air")).toBe("Hàng không");
    expect(rateTransportLabel("ROAD_X")).toBe("Phương thức khác");
  });
});
