"use client";

import { useEffect, useState } from "react";

type TenantSettings = {
  tenantId: string;
  uiJson: string | null;
  financialJson: string | null;
  financial: {
    maxWriteOffAmount?: number | null;
    confirmApprovalThresholdBase?: number | null;
    recognitionPolicyMode?: string | null;
    recognitionPolicyVersion?: string | null;
    revenueMaturityModel?: string | null;
    revenueMaturityPolicyVersion?: string | null;
    revenueMaturityEffectiveFrom?: string | null;
  };
};

export function TenantFinancialSettingsForm() {
  const [data, setData] = useState<TenantSettings | null>(null);
  const [maxWriteOff, setMaxWriteOff] = useState("");
  const [confirmThreshold, setConfirmThreshold] = useState("");
  const [policyMode, setPolicyMode] = useState("manual");
  const [policyVersion, setPolicyVersion] = useState("tenant-default-v1");
  const [maturityModel, setMaturityModel] = useState("three");
  const [maturityVersion, setMaturityVersion] = useState("v1");
  const [maturityFrom, setMaturityFrom] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        const res = await fetch("/bff/tenant-settings", {
          headers: { Accept: "application/json" },
          cache: "no-store",
        });
        if (res.status === 401) {
          window.location.href = "/login";
          return;
        }
        if (!res.ok) {
          setError("Không tải được cài đặt thuê bao.");
          return;
        }
        const json = (await res.json()) as TenantSettings;
        setData(json);
        setMaxWriteOff(
          json.financial?.maxWriteOffAmount != null
            ? String(json.financial.maxWriteOffAmount)
            : ""
        );
        setConfirmThreshold(
          json.financial?.confirmApprovalThresholdBase != null
            ? String(json.financial.confirmApprovalThresholdBase)
            : ""
        );
        setPolicyMode(json.financial?.recognitionPolicyMode || "manual");
        setPolicyVersion(
          json.financial?.recognitionPolicyVersion || "tenant-default-v1"
        );
        setMaturityModel(json.financial?.revenueMaturityModel === "two" ? "two" : "three");
        setMaturityVersion(json.financial?.revenueMaturityPolicyVersion || "v1");
        setMaturityFrom(json.financial?.revenueMaturityEffectiveFrom || "");
      } catch {
        setError("Không kết nối được máy chủ.");
      }
    })();
  }, []);

  async function save() {
    setBusy(true);
    setError(null);
    setInfo(null);
    let existing: Record<string, unknown> = {};
    try {
      existing = data?.financialJson ? (JSON.parse(data.financialJson) as Record<string, unknown>) : {};
    } catch {
      existing = {};
    }
    const financial = {
      ...existing,
      maxWriteOffAmount: maxWriteOff.trim()
        ? Number(maxWriteOff.replace(",", "."))
        : null,
      confirmApprovalThresholdBase: confirmThreshold.trim()
        ? Number(confirmThreshold.replace(",", "."))
        : null,
      recognitionPolicyMode: policyMode,
      recognitionPolicyVersion: policyVersion.trim() || "tenant-default-v1",
      revenueMaturityModel: maturityModel === "two" ? "two" : "three",
      revenueMaturityPolicyVersion: maturityVersion.trim() || "v1",
      revenueMaturityEffectiveFrom: maturityFrom.trim() || null,
    };
    try {
      const res = await fetch("/bff/tenant-settings", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          uiJson: data?.uiJson ?? null,
          financialJson: JSON.stringify(financial),
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(body.message || "Lưu cài đặt thất bại.");
        return;
      }
      setData((await res.json()) as TenantSettings);
      setInfo("Đã lưu cài đặt tài chính thuê bao.");
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="stack" style={{ marginTop: "1.5rem" }}>
      <fieldset className="group-box">
        <legend>Cài đặt tài chính thuê bao</legend>
        <p className="note" style={{ marginTop: 0 }}>
          Ghi đè ngưỡng node (P19/P20). Để trống = dùng mặc định hệ thống.
        </p>
        {error ? (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        ) : null}
        {info ? (
          <div className="alert alert-info" role="status">
            {info}
          </div>
        ) : null}
        <div className="form-sections cols-2">
          <fieldset className="group-box">
            <legend>Ngưỡng</legend>
            <div className="form-grid">
              <div className="field">
                <label htmlFor="max-wo">Trần xóa nợ áp dụng ngay</label>
                <input
                  id="max-wo"
                  type="text"
                  value={maxWriteOff}
                  onChange={(e) => setMaxWriteOff(e.target.value)}
                  placeholder="VD: 1000"
                  disabled={busy}
                />
              </div>
              <div className="field">
                <label htmlFor="confirm-th">Ngưỡng phê duyệt confirm</label>
                <input
                  id="confirm-th"
                  type="text"
                  value={confirmThreshold}
                  onChange={(e) => setConfirmThreshold(e.target.value)}
                  placeholder="Để trống = tắt"
                  disabled={busy}
                />
              </div>
            </div>
          </fieldset>
          <fieldset className="group-box">
            <legend>Chính sách ghi nhận</legend>
            <div className="form-grid">
              <div className="field">
                <label htmlFor="pol-mode">Chế độ AP/AR</label>
                <select
                  id="pol-mode"
                  value={policyMode}
                  onChange={(e) => setPolicyMode(e.target.value)}
                  disabled={busy}
                >
                  <option value="manual">manual — ghi nhận tay</option>
                  <option value="require_document_link">
                    require_document_link — cần chứng từ gắn exposure
                  </option>
                </select>
              </div>
              <div className="field">
                <label htmlFor="rev-maturity">Mô hình doanh thu</label>
                <select id="rev-maturity" value={maturityModel} onChange={(e) => setMaturityModel(e.target.value)} disabled={busy}>
                  <option value="three">3 lớp — Expected → Confirmed → Actual (mặc định)</option>
                  <option value="two">2 lớp — Expected → Actual (không tạo Confirmed)</option>
                </select>
                <p className="note">Đổi mô hình không sửa doanh thu lịch sử. Mặc định giữ 3 lớp cho đến khi PO chọn 2 lớp cho thuê bao.</p>
              </div>
              <div className="field">
                <label htmlFor="rev-maturity-ver">Phiên bản mô hình</label>
                <input id="rev-maturity-ver" value={maturityVersion} onChange={(e) => setMaturityVersion(e.target.value)} disabled={busy} maxLength={64} />
              </div>
              <div className="field">
                <label htmlFor="rev-maturity-from">Ngày hiệu lực mô hình</label>
                <input id="rev-maturity-from" placeholder="YYYY-MM-DD" value={maturityFrom} onChange={(e) => setMaturityFrom(e.target.value)} disabled={busy} />
              </div>
              <div className="field">
                <label htmlFor="pol-ver">Phiên bản chính sách</label>
                <input
                  id="pol-ver"
                  type="text"
                  value={policyVersion}
                  onChange={(e) => setPolicyVersion(e.target.value)}
                  maxLength={64}
                  disabled={busy}
                />
              </div>
            </div>
          </fieldset>
        </div>
        <div className="cta-row">
          <button type="button" className="btn" disabled={busy} onClick={save}>
            {busy ? "Đang lưu…" : "Lưu cài đặt thuê bao"}
          </button>
        </div>
      </fieldset>
    </div>
  );
}
