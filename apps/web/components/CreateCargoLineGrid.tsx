"use client";

import {
  emptyContainerLine,
  emptyPackageLine,
  type ContainerLineDraft,
  type PackageLineDraft,
} from "@/lib/cargo-lines";

type Props = {
  disabled: boolean;
  packages: PackageLineDraft[];
  containers: ContainerLineDraft[];
  onPackagesChange: (rows: PackageLineDraft[]) => void;
  onContainersChange: (rows: ContainerLineDraft[]) => void;
};

/** Editable kiện / container rows — fields match AddCargoPackage/ContainerCommand only. */
export function CreateCargoLineGrid({
  disabled,
  packages,
  containers,
  onPackagesChange,
  onContainersChange,
}: Props) {
  function patchPackage(key: string, patch: Partial<PackageLineDraft>) {
    onPackagesChange(packages.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  }

  function patchContainer(key: string, patch: Partial<ContainerLineDraft>) {
    onContainersChange(containers.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  }

  return (
    <div className="create-cargo-lines">
      <div className="cw-field s12">
        <label>Kiện</label>
        <p className="cw-hint">
          Dòng kiện gắn sau khi lưu chứng từ. Để trống nếu chưa có chi tiết kiện.
        </p>
        {packages.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có dòng kiện.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>STT</th>
                  <th>Số kiện</th>
                  <th>Dài (cm)</th>
                  <th>Rộng (cm)</th>
                  <th>Cao (cm)</th>
                  <th>Trọng lượng (kg)</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {packages.map((row, idx) => (
                  <tr key={row.key}>
                    <td className="num">{idx + 1}</td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="numeric"
                          value={row.packageCount}
                          disabled={disabled}
                          aria-label={`Số kiện dòng ${idx + 1}`}
                          onChange={(e) => patchPackage(row.key, { packageCount: e.target.value })}
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="decimal"
                          value={row.lengthCm}
                          disabled={disabled}
                          aria-label={`Dài cm dòng ${idx + 1}`}
                          onChange={(e) => patchPackage(row.key, { lengthCm: e.target.value })}
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="decimal"
                          value={row.widthCm}
                          disabled={disabled}
                          aria-label={`Rộng cm dòng ${idx + 1}`}
                          onChange={(e) => patchPackage(row.key, { widthCm: e.target.value })}
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="decimal"
                          value={row.heightCm}
                          disabled={disabled}
                          aria-label={`Cao cm dòng ${idx + 1}`}
                          onChange={(e) => patchPackage(row.key, { heightCm: e.target.value })}
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="decimal"
                          value={row.weightKg}
                          disabled={disabled}
                          aria-label={`Trọng lượng kg dòng ${idx + 1}`}
                          onChange={(e) => patchPackage(row.key, { weightKg: e.target.value })}
                        />
                      </div>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        disabled={disabled}
                        onClick={() => onPackagesChange(packages.filter((r) => r.key !== row.key))}
                      >
                        Xóa
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="cta-row" style={{ marginTop: 8 }}>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={disabled}
            onClick={() => onPackagesChange([...packages, emptyPackageLine()])}
          >
            + Thêm kiện
          </button>
        </div>
      </div>

      <div className="cw-field s12" style={{ marginTop: 12 }}>
        <label>Container</label>
        <p className="cw-hint">
          Loại container (vd 20GP, 40HC), số lượng và TEU — không phải thiết bị điều phối.
        </p>
        {containers.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có dòng container.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>STT</th>
                  <th>Loại container</th>
                  <th>Số container</th>
                  <th>Số lượng</th>
                  <th>TEU</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {containers.map((row, idx) => (
                  <tr key={row.key}>
                    <td className="num">{idx + 1}</td>
                    <td>
                      <div className="field">
                        <input
                          maxLength={16}
                          value={row.containerType}
                          disabled={disabled}
                          placeholder="VD: 20GP"
                          aria-label={`Loại container dòng ${idx + 1}`}
                          onChange={(e) =>
                            patchContainer(row.key, { containerType: e.target.value })
                          }
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          maxLength={64}
                          value={row.containerNo}
                          disabled={disabled}
                          placeholder="Tuỳ chọn"
                          aria-label={`Số container dòng ${idx + 1}`}
                          onChange={(e) =>
                            patchContainer(row.key, { containerNo: e.target.value })
                          }
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="numeric"
                          value={row.quantity}
                          disabled={disabled}
                          aria-label={`Số lượng container dòng ${idx + 1}`}
                          onChange={(e) =>
                            patchContainer(row.key, { quantity: e.target.value })
                          }
                        />
                      </div>
                    </td>
                    <td>
                      <div className="field">
                        <input
                          inputMode="decimal"
                          value={row.teu}
                          disabled={disabled}
                          aria-label={`TEU dòng ${idx + 1}`}
                          onChange={(e) => patchContainer(row.key, { teu: e.target.value })}
                        />
                      </div>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        disabled={disabled}
                        onClick={() =>
                          onContainersChange(containers.filter((r) => r.key !== row.key))
                        }
                      >
                        Xóa
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="cta-row" style={{ marginTop: 8 }}>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={disabled}
            onClick={() => onContainersChange([...containers, emptyContainerLine()])}
          >
            + Thêm container
          </button>
        </div>
      </div>
    </div>
  );
}
