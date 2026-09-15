"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { PartyBankAccount } from "@/lib/party";
import { findVnBank } from "@/lib/vn-banks";
import { VnBankAutocomplete } from "@/components/VnBankAutocomplete";
import { VnBankBranchField } from "@/components/VnBankBranchField";

export function PartyBankAccountsPanel({
  partyId,
  accounts,
}: {
  partyId: string;
  accounts: PartyBankAccount[];
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [bankShortName, setBankShortName] = useState<string | null>(null);
  const [formKey, setFormKey] = useState(0);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const rawBank = String(fd.get("bankName") ?? "").trim();
    const matched = findVnBank(rawBank);
    const body = {
      bankName: matched?.shortName ?? rawBank,
      bankBranch: String(fd.get("bankBranch") ?? "").trim() || null,
      accountNumber: String(fd.get("accountNumber") ?? "").trim(),
      accountName: String(fd.get("accountName") ?? "").trim() || null,
      currencyCode:
        String(fd.get("currencyCode") ?? "VND").trim().toUpperCase() || "VND",
      isDefault: fd.get("isDefault") === "on",
      isActive: true,
      note: null as string | null,
    };
    if (!body.bankName || !body.accountNumber) {
      setError("Chọn ngân hàng và nhập số tài khoản.");
      setSubmitting(false);
      return;
    }
    try {
      const res = await fetch(`/bff/admin/parties/${partyId}/bank-accounts`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Thêm tài khoản thất bại.");
        return;
      }
      setBankShortName(null);
      setFormKey((k) => k + 1);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  async function onDelete(id: string) {
    if (!window.confirm("Xóa mềm tài khoản ngân hàng này?")) return;
    setError(null);
    try {
      const res = await fetch(
        `/bff/admin/parties/${partyId}/bank-accounts/${id}`,
        { method: "DELETE", headers: { Accept: "application/json" } }
      );
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Xóa tài khoản thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    }
  }

  const busy = submitting || isPending;

  return (
    <fieldset className="group-box">
      <legend>Tài khoản ngân hàng</legend>
      {accounts.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có tài khoản ngân hàng.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Ngân hàng</th>
                <th>Số TK</th>
                <th>Tiền tệ</th>
                <th>Mặc định</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {accounts.map((a) => {
                const bank = findVnBank(a.bankName);
                return (
                  <tr key={a.id}>
                    <td>
                      <span className="vn-bank-row">
                        {bank ? (
                          // eslint-disable-next-line @next/next/no-img-element
                          <img src={bank.logo} alt="" width={22} height={22} />
                        ) : null}
                        <span>
                          <strong>{bank?.shortName ?? a.bankName}</strong>
                          {a.bankBranch ? (
                            <span className="muted"> · {a.bankBranch}</span>
                          ) : null}
                        </span>
                      </span>
                    </td>
                    <td className="mono-id">{a.accountNumber}</td>
                    <td>{a.currencyCode}</td>
                    <td>{a.isDefault ? "Có" : "—"}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        disabled={busy}
                        onClick={() => onDelete(a.id)}
                      >
                        Xóa
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <form
        key={formKey}
        className="receive-form"
        onSubmit={onSubmit}
        style={{ marginTop: "0.75rem" }}
      >
        <div className="form-grid">
          <div className="field field-span">
            <label>Ngân hàng</label>
            <VnBankAutocomplete
              required
              disabled={busy}
              onChange={(bank, display) =>
                setBankShortName(bank?.shortName ?? (display || null))
              }
            />
          </div>
          <VnBankBranchField
            disabled={busy}
            bankShortName={bankShortName}
          />
          <div className="field">
            <label htmlFor="accountNumber">Số tài khoản</label>
            <input
              id="accountNumber"
              name="accountNumber"
              required
              disabled={busy}
            />
          </div>
          <div className="field">
            <label htmlFor="accountName">Tên chủ TK</label>
            <input id="accountName" name="accountName" disabled={busy} />
          </div>
          <div className="field">
            <label htmlFor="bankCur">Tiền tệ</label>
            <input
              id="bankCur"
              name="currencyCode"
              maxLength={3}
              defaultValue="VND"
              disabled={busy}
            />
          </div>
          <label className="field checkbox-field">
            <input
              type="checkbox"
              name="isDefault"
              defaultChecked
              disabled={busy}
            />{" "}
            Mặc định thanh toán
          </label>
        </div>
        {error ? (
          <div className="alert alert-error" role="alert">
            {error}
          </div>
        ) : null}
        <div className="cta-row">
          <button type="submit" className="btn" disabled={busy}>
            {busy ? "Đang lưu…" : "Thêm tài khoản"}
          </button>
        </div>
      </form>
    </fieldset>
  );
}
