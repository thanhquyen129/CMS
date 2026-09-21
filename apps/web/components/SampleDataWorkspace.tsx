"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { SAMPLE_DATA_LABELS, type SampleDataStatus } from "@/lib/tenant-admin-model";

type Props = { status: SampleDataStatus };

export function SampleDataWorkspace({ status }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function seed() {
    setError(null);
    setInfo(null);
    setBusy(true);
    try {
      const res = await fetch("/bff/sample-data", {
        method: "POST",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không tạo được dữ liệu mẫu.");
        return;
      }
      const body = (await res.json()) as { summary?: string };
      setInfo(body.summary || "Đã bổ sung dữ liệu mẫu.");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const entries = Object.entries(status.counts);
  const short = status.complete
    ? `Đủ ≥${status.targetCount} bản ghi mỗi loại.`
    : `Một số loại chưa đủ ${status.targetCount} bản ghi. Bấm tạo để bổ sung.`;

  return (
    <div className="stack">
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? (
        <div className="alert alert-success" role="status">
          {info}
        </div>
      ) : null}
      <p className="lede">{short}</p>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Loại</th>
              <th className="num">Số lượng mẫu (VOL)</th>
              <th className="num">Mục tiêu</th>
            </tr>
          </thead>
          <tbody>
            {entries.map(([key, count]) => (
              <tr key={key}>
                <td>{SAMPLE_DATA_LABELS[key] ?? key}</td>
                <td className="num">{count}</td>
                <td className="num">{status.targetCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="toolbar-row">
        <button className="btn" type="button" onClick={() => void seed()} disabled={busy || isPending}>
          {busy || isPending ? "Đang tạo…" : status.complete ? "Kiểm tra lại / bổ sung thiếu" : "Tạo dữ liệu mẫu"}
        </button>
      </div>
    </div>
  );
}
