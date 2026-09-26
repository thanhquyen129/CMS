"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Proposal = {
  matchDetailId: string;
  kind: string;
  costId: string | null;
  revenueId: string | null;
  financialDocumentId: string;
  billId: string | null;
  amount: number;
  currencyCode: string;
  alreadyExists: boolean;
  existingExposureId: string | null;
  skipReason: string | null;
};

type CreateResult = {
  createdCount: number;
  skippedExistingCount: number;
  skippedIneligibleCount: number;
  createdPayableExposureIds: string[];
  createdReceivableExposureIds: string[];
};

type Props = {
  matchId: string;
  documentId: string;
  documentNo?: string | null;
};

export function CreateExposuresFromMatchButton({
  matchId,
  documentId,
  documentNo,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [proposals, setProposals] = useState<Proposal[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function loadProposals() {
    setError(null);
    setInfo(null);
    setBusy(true);
    try {
      const res = await fetch(
        `/bff/document-matches/${matchId}/exposure-proposals`,
        { headers: { Accept: "application/json" }, cache: "no-store" }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Không tải được đề xuất exposure.");
        return;
      }
      setProposals((await res.json()) as Proposal[]);
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  async function createExposures() {
    if (
      !window.confirm(
        "Tạo exposure từ các dòng khớp Cost/Revenue? Không tạo chi phí/doanh thu mới."
      )
    ) {
      return;
    }
    setError(null);
    setInfo(null);
    setBusy(true);
    try {
      const res = await fetch(
        `/bff/document-matches/${matchId}/create-exposures`,
        {
          method: "POST",
          headers: { Accept: "application/json" },
        }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Tạo exposure thất bại.");
        return;
      }
      const result = (await res.json()) as CreateResult;
      setInfo(
        `Đã tạo ${result.createdCount} exposure` +
          (result.skippedExistingCount
            ? `; bỏ qua ${result.skippedExistingCount} đã có`
            : "") +
          (result.skippedIneligibleCount
            ? `; ${result.skippedIneligibleCount} không đủ điều kiện`
            : "") +
          "."
      );
      await loadProposals();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const loading = busy || isPending;
  const creatable =
    proposals?.filter((p) => !p.alreadyExists && (p.kind === "payable" || p.kind === "receivable"))
      .length ?? 0;

  return (
    <div className="stack" style={{ marginTop: "1rem" }}>
      <h2 className="section-title sm">Exposure từ khớp</h2>
      <p className="note">
        Đề xuất / tạo exposure gắn Cost hoặc Revenue đã khớp — không invent chi phí /
        doanh thu (Received ≠ Accepted ≠ Matched ≠ Recognized).
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
      <div className="cta-row">
        <button
          type="button"
          className="btn btn-ghost"
          disabled={loading}
          onClick={loadProposals}
        >
          {loading && !proposals ? "Đang tải…" : "Xem đề xuất"}
        </button>
        <button
          type="button"
          className="btn"
          disabled={loading || creatable === 0}
          onClick={createExposures}
        >
          {loading ? "Đang tạo…" : `Tạo exposure (${creatable})`}
        </button>
        <a className="btn btn-ghost" href="/ap-ar?tab=exposure">
          Mở danh sách exposure
        </a>
      </div>

      {proposals ? (
        proposals.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có chi tiết khớp active. Thêm dòng ↔ Cost/Revenue trước.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Loại</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col">Cost / Revenue</th>
                  <th scope="col">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {proposals.map((p) => (
                  <tr key={p.matchDetailId}>
                    <td>
                      {p.kind === "payable"
                        ? "Phải trả"
                        : p.kind === "receivable"
                          ? "Phải thu"
                          : "Bỏ qua"}
                    </td>
                    <td className="num">
                      {p.currencyCode
                        ? `${p.amount.toLocaleString("vi-VN")} ${p.currencyCode}`
                        : p.amount.toLocaleString("vi-VN")}
                    </td>
                    <td className="muted small">
                      {p.kind === "payable"
                        ? "Chi phí đã khớp"
                        : p.kind === "receivable"
                          ? "Doanh thu đã khớp"
                          : "—"}
                    </td>
                    <td>
                      {p.alreadyExists ? (
                        <a
                          className="row-link"
                          href={`/ap-ar/exposures/${p.existingExposureId}/recognize?kind=${p.kind === "receivable" ? "receivable" : "payable"}`}
                        >
                          Đã có — ghi nhận
                        </a>
                      ) : p.skipReason ? (
                        <span className="muted small">{p.skipReason}</span>
                      ) : (
                        "Sẵn sàng tạo"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      ) : null}
      <p className="muted small">
        Chứng từ neo:{" "}
        <Link href={`/documents/${documentId}`}>{documentNo || documentId}</Link>
      </p>
    </div>
  );
}
