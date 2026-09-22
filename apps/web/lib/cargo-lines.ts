/** Draft package/container lines for create forms → POST /api/operational-import/*. */

export type PackageLineDraft = {
  key: string;
  packageCount: string;
  lengthCm: string;
  widthCm: string;
  heightCm: string;
  weightKg: string;
};

export type ContainerLineDraft = {
  key: string;
  containerType: string;
  containerNo: string;
  quantity: string;
  teu: string;
};

export function emptyPackageLine(): PackageLineDraft {
  return {
    key: crypto.randomUUID(),
    packageCount: "1",
    lengthCm: "",
    widthCm: "",
    heightCm: "",
    weightKg: "",
  };
}

export function emptyContainerLine(): ContainerLineDraft {
  return {
    key: crypto.randomUUID(),
    containerType: "",
    containerNo: "",
    quantity: "1",
    teu: "1",
  };
}

function parsePositiveInt(raw: string): number | null {
  const n = Number(String(raw).trim().replaceAll(",", "."));
  if (!Number.isFinite(n) || n <= 0) return null;
  return Math.round(n);
}

function parseNonNegNum(raw: string): number | null {
  const t = String(raw).trim().replaceAll(",", ".");
  if (!t) return null;
  const n = Number(t);
  if (!Number.isFinite(n) || n < 0) return null;
  return n;
}

function parseOptPositiveNum(raw: string): number | null {
  const t = String(raw).trim().replaceAll(",", ".");
  if (!t) return null;
  const n = Number(t);
  if (!Number.isFinite(n) || n <= 0) return null;
  return n;
}

/** Skip blank rows; validate filled rows. Returns VI error or null. */
export function validateCargoLines(
  packages: PackageLineDraft[],
  containers: ContainerLineDraft[]
): string | null {
  for (let i = 0; i < packages.length; i++) {
    const p = packages[i];
    const hasDim =
      p.lengthCm.trim() || p.widthCm.trim() || p.heightCm.trim() || p.weightKg.trim();
    const countRaw = p.packageCount.trim();
    if (!countRaw && !hasDim) continue;
    if (parsePositiveInt(p.packageCount) == null) {
      return `Dòng kiện ${i + 1}: nhập số kiện lớn hơn 0.`;
    }
    for (const [label, raw] of [
      ["Dài", p.lengthCm],
      ["Rộng", p.widthCm],
      ["Cao", p.heightCm],
      ["Trọng lượng", p.weightKg],
    ] as const) {
      if (raw.trim() && parseOptPositiveNum(raw) == null) {
        return `Dòng kiện ${i + 1}: ${label} không hợp lệ.`;
      }
    }
  }

  for (let i = 0; i < containers.length; i++) {
    const c = containers[i];
    const type = c.containerType.trim();
    const hasOther =
      c.containerNo.trim() || c.quantity.trim() || c.teu.trim();
    if (!type && !hasOther) continue;
    if (!type) {
      return `Dòng container ${i + 1}: nhập loại container.`;
    }
    if (type.length > 16) {
      return `Dòng container ${i + 1}: loại container tối đa 16 ký tự.`;
    }
    if (parsePositiveInt(c.quantity) == null) {
      return `Dòng container ${i + 1}: nhập số lượng lớn hơn 0.`;
    }
    if (parseNonNegNum(c.teu) == null) {
      return `Dòng container ${i + 1}: nhập TEU ≥ 0.`;
    }
  }

  return null;
}

export type CargoParentType = "bill" | "order" | "shipment";

type PostResult = { ok: true } | { ok: false; message: string };

async function postJson(url: string, body: unknown): Promise<PostResult> {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify(body),
  });
  if (res.status === 401) {
    window.location.href = "/login";
    return { ok: false, message: "Phiên đăng nhập đã hết." };
  }
  if (!res.ok) {
    const payload = (await res.json().catch(() => ({}))) as { message?: string };
    return {
      ok: false,
      message:
        payload.message ||
        (res.status === 403
          ? "Bạn không có quyền ghi kiện/container."
          : "Không lưu được kiện/container."),
    };
  }
  return { ok: true };
}

/** POST package/container lines after parent create. Empty/blank rows skipped. */
export async function submitCargoLines(
  objectType: CargoParentType,
  objectId: string,
  packages: PackageLineDraft[],
  containers: ContainerLineDraft[]
): Promise<PostResult> {
  let seq = 0;
  for (const p of packages) {
    const packageCount = parsePositiveInt(p.packageCount);
    const hasDim =
      p.lengthCm.trim() || p.widthCm.trim() || p.heightCm.trim() || p.weightKg.trim();
    if (packageCount == null && !hasDim) continue;
    if (packageCount == null) {
      return { ok: false, message: "Số kiện không hợp lệ." };
    }
    seq += 1;
    const result = await postJson("/bff/operational-import/packages", {
      objectType,
      objectId,
      sequenceNo: seq,
      packageCount,
      lengthCm: parseOptPositiveNum(p.lengthCm),
      widthCm: parseOptPositiveNum(p.widthCm),
      heightCm: parseOptPositiveNum(p.heightCm),
      weightKg: parseOptPositiveNum(p.weightKg),
    });
    if (!result.ok) return result;
  }

  seq = 0;
  for (const c of containers) {
    const type = c.containerType.trim();
    const hasOther =
      c.containerNo.trim() || c.quantity.trim() || c.teu.trim();
    if (!type && !hasOther) continue;
    const quantity = parsePositiveInt(c.quantity);
    const teu = parseNonNegNum(c.teu);
    if (!type || quantity == null || teu == null) {
      return { ok: false, message: "Dòng container thiếu loại, số lượng hoặc TEU." };
    }
    seq += 1;
    const result = await postJson("/bff/operational-import/containers", {
      objectType,
      objectId,
      sequenceNo: seq,
      containerType: type,
      quantity,
      teu,
      containerNo: c.containerNo.trim() || null,
    });
    if (!result.ok) return result;
  }

  return { ok: true };
}
