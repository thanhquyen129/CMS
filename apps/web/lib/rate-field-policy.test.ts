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
    expect(storedMode("AIR")).toBe("air");
    expect(storedMode("sea")).toBe("sea");
    expect(storedMode("ROAD")).toBe("road");
    expect(storedMode("RAIL")).toBe("rail");
    expect(storedMode("WATER")).toBe("water");
    expect(storedMode("EXPRESS")).toBe("express");
    expect(storedMode("WATER")).not.toBe("sea");
    expect(storedMode("EXPRESS")).not.toBe("air");
    const modes = selectableModes([
      { code: "AIR", name: "Hàng không" },
      { code: "WATER", name: "WATER" },
      { code: "EXPRESS", name: "EXPRESS" },
    ]);
    expect(modes.map((item) => item.value)).toEqual(["air"]);
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
