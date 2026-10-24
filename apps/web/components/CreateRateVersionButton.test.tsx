import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CreateRateVersionButton } from "@/components/CreateRateVersionButton";

const pushMock = vi.fn();
const refreshMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock, refresh: refreshMock }),
}));

describe("CreateRateVersionButton", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: true,
        status: 201,
        json: async () => ({ id: "v2-id" }),
      }))
    );
  });

  it("renders with default label and handles click with cloneFromVersionId", async () => {
    const user = userEvent.setup();
    render(
      <CreateRateVersionButton
        rateCardId="card-123"
        sourceVersionId="v1-id"
        sourceVersionNo={1}
      />
    );

    const btn = screen.getByRole("button", { name: "Tạo phiên bản mới" });
    expect(btn).toBeTruthy();

    await user.click(btn);

    expect(fetch).toHaveBeenCalledWith("/bff/rate-cards/card-123/versions", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({ cloneFromVersionId: "v1-id" }),
    });

    expect(pushMock).toHaveBeenCalledWith("/rate-cards/card-123/edit");
  });

  it("prevents double-clicks while submitting", async () => {
    let resolveFetch: (value: unknown) => void = () => undefined;
    vi.stubGlobal(
      "fetch",
      vi.fn(
        () =>
          new Promise((resolve) => {
            resolveFetch = resolve;
          })
      )
    );

    const user = userEvent.setup();
    render(
      <CreateRateVersionButton
        rateCardId="card-123"
        sourceVersionId="v1-id"
      />
    );

    const btn = screen.getByRole("button", { name: "Tạo phiên bản mới" });
    await user.click(btn);

    expect((btn as HTMLButtonElement).disabled).toBe(true);
    expect(btn.textContent).toContain("Đang tạo phiên bản…");

    // Clicking again while busy should not call fetch again
    await user.click(btn);
    expect(fetch).toHaveBeenCalledTimes(1);

    resolveFetch({
      ok: true,
      status: 201,
      json: async () => ({ id: "v2-id" }),
    });
  });

  it("displays error message on 403 Forbidden without navigation", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: false,
        status: 403,
        json: async () => ({ message: "Bạn không có quyền tạo phiên bản bảng giá này." }),
      }))
    );

    const user = userEvent.setup();
    render(
      <CreateRateVersionButton
        rateCardId="card-123"
      />
    );

    const btn = screen.getByRole("button", { name: "Tạo phiên bản mới" });
    await user.click(btn);

    expect(screen.getByRole("alert").textContent).toContain("Bạn không có quyền tạo phiên bản bảng giá này.");
    expect(pushMock).not.toHaveBeenCalled();
  });
});
