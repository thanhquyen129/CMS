import { describe, expect, it } from "vitest";
import {
  modeBlock,
  routeModeConflictMessage,
  selectableModes,
  selectableServices,
  serviceBlock,
  storedMode,
  suggestRuleName,
} from "./rate-field-policy";

describe("rate field policy", () => {
  it("hides AIR from new service choices and keeps a stored value readable", () => {
    const choices = selectableServices([
      { code: "FREIGHT", name: "Vận tải" },
      { code: "AIR", name: "AIR" },
    ]);
    expect(choices.map((item) => item.code)).toEqual(["FREIGHT"]);
    expect(selectableServices([{ code: "AIR", name: "AIR" }], "AIR").map((item) => item.code)).toEqual(["AIR"]);
    expect(serviceBlock("AIR", false)).toContain("AIR");
    expect(serviceBlock("AIR", true)).toBeNull();
  });

  it("maps mode codes both ways and blocks WATER and EXPRESS for new rules", () => {
    expect(storedMode("AIR")).toBe("AIR");
    expect(storedMode("sea")).toBe("SEA");
    expect(storedMode("ROAD")).toBe("ROAD");
    expect(storedMode("RAIL")).toBe("RAIL");
    expect(storedMode("WATER")).toBe("WATER");
    expect(storedMode("EXPRESS")).toBe("EXPRESS");
    expect(storedMode("WATER")).not.toBe("SEA");
    expect(storedMode("EXPRESS")).not.toBe("AIR");
    const modes = selectableModes([
      { code: "AIR", name: "Hàng không" },
      { code: "WATER", name: "WATER" },
      { code: "EXPRESS", name: "EXPRESS" },
    ]);
    expect(modes.map((item) => item.value)).toEqual(["AIR"]);
    expect(modes[0].label).toBe("Hàng không");
    expect(modeBlock("water")).toContain("WATER");
    expect(modeBlock("express")).toContain("EXPRESS");
  });

  it("blocks a route mode that disagrees and allows every route", () => {
    expect(routeModeConflictMessage("air", "ROAD", false)).toContain("không khớp");
    expect(routeModeConflictMessage("air", "AIR", false)).toBeNull();
    expect(routeModeConflictMessage("air", "ROAD", true)).toBeNull();
  });

  it("suggests a name without using it as a surcharge classifier", () => {
    expect(suggestRuleName("Cước chính", "SGN-FRA", "0", "10")).toBe("Cước chính · SGN-FRA · 0 – < 10");
  });
});
