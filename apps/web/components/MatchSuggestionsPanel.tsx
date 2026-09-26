"use client";

import { useState } from "react";

type Suggestion = {
  sourceLineId: string;
  sourceLineNo: number;
  sourceOpenAmount: number;
  currencyCode: string;
  targetKind: string;
  targetId: string;
  targetLabel: string;
  targetAmount: number;
  suggestedMatchedAmount: number;
  amountDelta: number;
  effectiveTolerance: number;
};

type Props = {
  matchId: string;
  draft: boolean;
};

export function MatchSuggestionsPanel({ matchId, draft }: Props) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [items, setItems] = useState<Suggestion[] | null>(null);

  async function load() {
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(`/bff/document-matches/${matchId}/suggestions`, {
        headers: { Accept: "application/json" },
        cache: "no-store",
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(body.message || "Không tải được đề xuất khớp.");
        return;
      }
      setItems((await res.json()) as Suggestion[]);
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  function kindLabel(kind: string) {
    switch (kind) {
      case "cost":
        return "Chi phí";
      case "revenue":
        return "Doanh thu";
      case "line":
        return "Dòng CT";
      default:
        return kind;
    }
  }

  return (
    <div className="stack" style={{ marginTop: "1rem" }}>
      <h2 className="section-title sm">Đề xuất trong dung sai</h2>
      <p className="note">
        Gợi ý ứng viên |Δ| ≤ dung sai phiên — chỉ xem; thêm chi tiết thủ công bên
        dưới{draft ? "" : " (phiên đã khóa thêm mới)"}.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="cta-row">
        <button
          type="button"
          className="btn btn-ghost"
          disabled={busy}
          onClick={load}
        >
          {busy ? "Đang tải…" : "Xem đề xuất"}
        </button>
      </div>
      {items ? (
        items.length === 0 ? (
          <div className="empty-state" role="status">
            Không có ứng viên trong dung sai. Kiểm tra Bill / tiền tệ / số mở.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Dòng nguồn</th>
                  <th scope="col">Đích gợi ý</th>
                  <th scope="col" className="num">
                    Đề xuất khớp
                  </th>
                  <th scope="col" className="num">
                    |Δ|
                  </th>
                </tr>
              </thead>
              <tbody>
                {items.map((s) => (
                  <tr key={`${s.sourceLineId}-${s.targetKind}-${s.targetId}`}>
                    <td>
                      #{s.sourceLineNo} · mở{" "}
                      {s.sourceOpenAmount.toLocaleString("vi-VN")}{" "}
                      {s.currencyCode}
                    </td>
                    <td>
                      <span className="muted small">{kindLabel(s.targetKind)}</span>{" "}
                      {s.targetLabel}
                    </td>
                    <td className="num">
                      {s.suggestedMatchedAmount.toLocaleString("vi-VN")}{" "}
                      {s.currencyCode}
                    </td>
                    <td className="num">
                      {s.amountDelta.toLocaleString("vi-VN")}
                      <div className="muted small">
                        tol {s.effectiveTolerance.toLocaleString("vi-VN")}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      ) : null}
    </div>
  );
}
