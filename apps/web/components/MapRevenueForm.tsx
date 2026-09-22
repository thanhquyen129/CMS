"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type BillOption = { id: string; billNo: string };

export function MapRevenueForm({
  revenueId,
  amount,
  currencyCode,
  bills,
}: {
  revenueId: string;
  amount: number;
  currencyCode: string;
  bills: BillOption[];
}) {
  const router = useRouter();
  const [selected, setSelected] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();

  function toggle(id: string) {
    setSelected((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id]
    );
  }

  async function submit() {
    setError(null);
    if (selected.length < 2) {
      setError("Chọn ít nhất 2 Bill. Không gán mọi Bill khác.");
      return;
    }
    setBusy(true);
    try {
      const created = await fetch(`/bff/revenues/${revenueId}/mappings`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          allocationBasis: "equal",
          details: selected.map((billId) => ({ billId })),
        }),
      });
      const payload = (await created.json().catch(() => ({}))) as { id?: string; message?: string };
      if (!created.ok || !payload.id) {
        setError(payload.message || "Không tạo được phiên chia.");
        return;
      }
      const fin = await fetch(`/bff/revenue-mappings/${payload.id}/finalize`, { method: "POST" });
      if (!fin.ok) {
        const body = (await fin.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Không chốt được phiên chia.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2>Chia doanh thu cho nhiều Bill</h2>
      <p className="note">
        Một doanh thu {amount} {currencyCode} chia đều rồi chốt. Tổng dòng bằng số gốc. Chia lại thì phiên cũ bị thay.
      </p>
      <ul className="inline-list">
        {bills.map((bill) => (
          <li key={bill.id}>
            <label>
              <input
                type="checkbox"
                checked={selected.includes(bill.id)}
                onChange={() => toggle(bill.id)}
              />{" "}
              {bill.billNo}
            </label>
          </li>
        ))}
      </ul>
      <button className="btn btn-sm" type="button" disabled={busy || pending} onClick={() => void submit()}>
        Chốt chia đều
      </button>
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
    </section>
  );
}
