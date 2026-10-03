"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useEffect, useId, useState, useTransition } from "react";
import { BillTypeahead } from "@/components/BillTypeahead";
import { CurrencySelect } from "@/components/CurrencySelect";
import { PartyTypeahead } from "@/components/PartyTypeahead";
import { formatApiErrorMessage } from "@/lib/api-error";
import { withIdempotency } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import type { PartyLookupItem } from "@/lib/parties-client";
import { term, type TerminologyMap } from "@/lib/terminology";
import { useIdempotency } from "@/lib/use-idempotency";
import type { EligibleSourceLine, SourceLineSelection } from "@/lib/documents";

type Props = {
  terms: TerminologyMap;
  defaultBillId?: string;
};

export function ReceiveDocumentForm({ terms, defaultBillId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const idem = useIdempotency("doc-receive");

  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const billLabel = term(terms, "BILL", "Bill");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  // Step 1: Direction (payable | receivable)
  const [direction, setDirection] = useState<"payable" | "receivable">("payable");

  // Step 2: Mode (lcms_generated | external_received)
  const [mode, setMode] = useState<"lcms_generated" | "external_received">("lcms_generated");

  // Step 3 & 4: Filters and common fields
  const [counterpartyId, setCounterpartyId] = useState<string | null>(null);
  const [billId, setBillId] = useState<string | null>(defaultBillId || null);
  const [currencyCode, setCurrencyCode] = useState<string>("VND");
  const [documentType, setDocumentType] = useState<string>("invoice");
  const [documentNo, setDocumentNo] = useState<string>("");
  const [documentDate, setDocumentDate] = useState<string>(
    new Date().toISOString().slice(0, 10)
  );
  const [notes, setNotes] = useState<string>("");

  // Step 5: Eligible source lines
  const [eligibleLines, setEligibleLines] = useState<EligibleSourceLine[]>([]);
  const [loadingLines, setLoadingLines] = useState(false);
  const [linesError, setLinesError] = useState<string | null>(null);

  // Selected source lines: Map<sourceId, { amount: number, line: EligibleSourceLine }>
  const [selectedLinesMap, setSelectedLinesMap] = useState<
    Map<string, { amount: number; line: EligibleSourceLine }>
  >(new Map());

  // Step 6: Mode A manual total amount
  const [manualTotalAmount, setManualTotalAmount] = useState<string>("");

  // Fetch eligible source lines when direction, counterparty, bill, or currency changes
  useEffect(() => {
    let active = true;
    async function fetchLines() {
      setLoadingLines(true);
      setLinesError(null);

      const params = new URLSearchParams({ direction });
      if (counterpartyId) params.set("counterpartyId", counterpartyId);
      if (billId) params.set("billId", billId);
      if (currencyCode) params.set("currencyCode", currencyCode);

      try {
        const res = await fetch(
          `/bff/financial-documents/eligible-source-lines?${params.toString()}`,
          { cache: "no-store" }
        );
        if (!active) return;
        if (!res.ok) {
          setLinesError("Không tải được danh sách khoản tài chính liên quan.");
          setEligibleLines([]);
          return;
        }
        const data = (await res.json()) as EligibleSourceLine[];
        if (active) {
          setEligibleLines(Array.isArray(data) ? data : []);
        }
      } catch {
        if (active) {
          setLinesError("Lỗi kết nối khi tải danh sách dòng tài chính.");
          setEligibleLines([]);
        }
      } finally {
        if (active) {
          setLoadingLines(false);
        }
      }
    }

    void fetchLines();

    return () => {
      active = false;
    };
  }, [direction, counterpartyId, billId, currencyCode]);

  // When direction changes, clear selected lines
  function handleDirectionChange(newDir: "payable" | "receivable") {
    setDirection(newDir);
    setSelectedLinesMap(new Map());
  }

  // Toggle selection for a line
  function toggleLine(line: EligibleSourceLine) {
    setSelectedLinesMap((prev) => {
      const next = new Map(prev);
      if (next.has(line.sourceId)) {
        next.delete(line.sourceId);
      } else {
        next.set(line.sourceId, {
          amount: line.remainingEligibleAmount,
          line,
        });
      }
      return next;
    });
  }

  // Update amount to match for a selected line (Partial matching: AC-FD-005)
  function updateLineAmount(line: EligibleSourceLine, newAmount: number) {
    setSelectedLinesMap((prev) => {
      const next = new Map(prev);
      const clamped = Math.max(
        0,
        Math.min(newAmount, line.remainingEligibleAmount)
      );
      next.set(line.sourceId, {
        amount: clamped,
        line,
      });
      return next;
    });
  }

  // Calculate sum of selected source lines
  const selectedLinesSum = Array.from(selectedLinesMap.values()).reduce(
    (sum, item) => sum + item.amount,
    0
  );

  // Derived or user-entered total
  const finalTotalAmount =
    mode === "lcms_generated"
      ? selectedLinesSum
      : Number(manualTotalAmount.replace(",", ".")) || 0;

  const variance = finalTotalAmount - selectedLinesSum;

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const idemKey = idem.acquire();
    if (!idemKey) return;
    setError(null);
    setSubmitting(true);
    let succeeded = false;

    if (!documentNo.trim()) {
      setError("Số chứng từ không được để trống.");
      setSubmitting(false);
      idem.release(false);
      return;
    }

    if (mode === "lcms_generated" && selectedLinesMap.size === 0) {
      setError("Chế độ tạo từ LCMS bắt buộc chọn ít nhất một dòng nguồn (FD-R02).");
      setSubmitting(false);
      idem.release(false);
      return;
    }

    if (finalTotalAmount < 0 || !Number.isFinite(finalTotalAmount)) {
      setError("Tổng tiền chứng từ không hợp lệ.");
      setSubmitting(false);
      idem.release(false);
      return;
    }

    const selectedSourceLines: SourceLineSelection[] = Array.from(
      selectedLinesMap.values()
    ).map(({ amount, line }) => ({
      sourceId: line.sourceId,
      sourceType: line.sourceType,
      amount,
      billId: line.billId || undefined,
    }));

    const body = {
      mode,
      documentType,
      documentNo: documentNo.trim(),
      direction,
      totalAmount: finalTotalAmount,
      currencyCode: currencyCode.trim().toUpperCase(),
      documentDate: documentDate || null,
      billId: billId || null,
      counterpartyId: counterpartyId || null,
      notes: notes.trim() || null,
      selectedSourceLines,
    };

    try {
      const res = await fetch("/bff/financial-documents", {
        method: "POST",
        headers: withIdempotency(
          { "Content-Type": "application/json" },
          idemKey
        ),
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
          errors?: Record<string, string[]>;
          correlationId?: string;
        };
        setError(
          formatApiErrorMessage(
            payload,
            res.status === 403
              ? "Bạn không có quyền nhận chứng từ."
              : res.status === 409
                ? "Chứng từ trùng hoặc xung đột. Kiểm tra số chứng từ / đối tác."
                : "Nhận chứng từ thất bại."
          )
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      succeeded = true;
      if (created.id) {
        startTransition(() => router.push(`/documents/${created.id}`));
      } else {
        startTransition(() => router.push("/documents"));
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      idem.release(succeeded);
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Nhận chỉ đặt trạng thái <strong>{receivedLabel}</strong>. Chấp nhận và
        khớp là bước riêng. Không tự tạo thêm {costLabel} hay {paymentLabel}{" "}
        (FD-R01 / Single Economic Fact).
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {/* BƯỚC 1 & BƯỚC 2: Chọn Chiều và Chế độ */}
      <fieldset className="group-box" style={{ marginBottom: "1.25rem" }}>
        <legend>Bước 1 &amp; 2: Chiều &amp; Chế độ chứng từ</legend>
        <div className="form-grid cols-2">
          <div className="field">
            <label>Chiều chứng từ</label>
            <div style={{ display: "flex", gap: "1.5rem", marginTop: "0.25rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", cursor: "pointer" }}>
                <input
                  type="radio"
                  name="direction_radio"
                  value="payable"
                  checked={direction === "payable"}
                  disabled={busy}
                  onChange={() => handleDirectionChange("payable")}
                />
                <strong>Phải trả (AP-side / Nhà cung cấp)</strong>
              </label>
              <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", cursor: "pointer" }}>
                <input
                  type="radio"
                  name="direction_radio"
                  value="receivable"
                  checked={direction === "receivable"}
                  disabled={busy}
                  onChange={() => handleDirectionChange("receivable")}
                />
                <strong>Phải thu (AR-side / Khách hàng)</strong>
              </label>
            </div>
          </div>

          <div className="field">
            <label>Chế độ chứng từ (PO 02/10/2026)</label>
            <div style={{ display: "flex", gap: "1.5rem", marginTop: "0.25rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", cursor: "pointer" }}>
                <input
                  type="radio"
                  name="mode_radio"
                  value="lcms_generated"
                  checked={mode === "lcms_generated"}
                  disabled={busy}
                  onChange={() => setMode("lcms_generated")}
                />
                <strong>Tạo từ LCMS (Mode B)</strong>
              </label>
              <label style={{ display: "flex", alignItems: "center", gap: "0.4rem", cursor: "pointer" }}>
                <input
                  type="radio"
                  name="mode_radio"
                  value="external_received"
                  checked={mode === "external_received"}
                  disabled={busy}
                  onChange={() => setMode("external_received")}
                />
                <strong>Nhận chứng từ bên ngoài (Mode A)</strong>
              </label>
            </div>
          </div>
        </div>
      </fieldset>

      {/* BƯỚC 3 & BƯỚC 4: Thông tin chứng từ & Bộ lọc */}
      <div className="form-sections cols-2" style={{ marginBottom: "1.25rem" }}>
        <fieldset className="group-box">
          <legend>Bước 3: Thông tin chứng từ</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="documentType">Loại {docLabel.toLowerCase()}</label>
              <select
                id="documentType"
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value)}
                disabled={busy}
              >
                <option value="invoice">Hóa đơn</option>
                <option value="dn">DN</option>
                <option value="credit_note">Credit note</option>
                <option value="debit_note">Debit note</option>
                <option value="other">Khác</option>
              </select>
            </div>

            <div className="field">
              <label htmlFor="documentNo">Số chứng từ</label>
              <input
                id="documentNo"
                value={documentNo}
                onChange={(e) => setDocumentNo(e.target.value)}
                required
                maxLength={128}
                disabled={busy}
                autoComplete="off"
                placeholder="VD: INV-2026-001"
              />
            </div>

            <div className="field">
              <label htmlFor="documentDate">Ngày chứng từ</label>
              <input
                id="documentDate"
                type="date"
                value={documentDate}
                onChange={(e) => setDocumentDate(e.target.value)}
                disabled={busy}
              />
            </div>

            <CurrencySelect
              id="currencySelect"
              label="Nguyên tệ chứng từ"
              disabled={busy}
              value={currencyCode}
              onChange={setCurrencyCode}
            />

            <div className="field field-span">
              <label htmlFor="notes">Ghi chú</label>
              <input
                id="notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                maxLength={2048}
                disabled={busy}
                placeholder="Ghi chú nghiệp vụ / tham chiếu nội bộ…"
              />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>Bước 4: Đối tác &amp; Bill lọc (Phạm vi tìm kiếm)</legend>
          <div className="form-grid">
            <div className="field field-span">
              <PartyTypeahead
                name="counterpartyId"
                label={
                  direction === "payable"
                    ? "Đối tác (Nhà cung cấp / Bên nhận tiền)"
                    : "Đối tác (Khách hàng / Bên trả tiền)"
                }
                roleCode={direction === "payable" ? "vendor" : "customer"}
                disabled={busy}
                onSelect={(party) => setCounterpartyId(party?.id || null)}
                hint={
                  direction === "payable"
                    ? "Lọc các dòng chi phí của nhà cung cấp này (AC-FD-019)."
                    : "Lọc các dòng doanh thu của khách hàng này (AC-FD-019)."
                }
              />
            </div>

            <div className="field field-span">
              <BillTypeahead
                label={`${billLabel} lọc (tuỳ chọn)`}
                disabled={busy}
                defaultId={defaultBillId}
                onSelect={(b) => setBillId(b?.id || null)}
              />
              <p className="note" style={{ margin: "0.25rem 0 0" }}>
                Bill chỉ là phạm vi tìm kiếm. Không tự lấy toàn bộ số tiền của Bill
                vào chứng từ (FD-R06, AC-FD-004).
              </p>
            </div>
          </div>
        </fieldset>
      </div>

      {/* BƯỚC 5: Bảng Khoản tài chính liên quan */}
      <fieldset className="group-box" style={{ marginBottom: "1.25rem" }}>
        <legend>
          Bước 5: Khoản tài chính liên quan ({direction === "payable" ? costLabel : revenueLabel})
        </legend>

        {linesError ? (
          <div className="alert alert-error" style={{ margin: "0.5rem 0" }}>
            {linesError}
          </div>
        ) : null}

        {loadingLines ? (
          <p className="muted" style={{ padding: "1rem" }}>
            Đang tải các dòng tài chính khả dụng…
          </p>
        ) : eligibleLines.length === 0 ? (
          <p className="muted" style={{ padding: "1rem 0" }}>
            Không tìm thấy khoản {direction === "payable" ? "chi phí" : "doanh thu"}{" "}
            nào có số dư khả dụng phù hợp với bộ lọc hiện tại.
          </p>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table className="data-table" style={{ width: "100%", marginTop: "0.5rem" }}>
              <thead>
                <tr>
                  <th style={{ width: "3rem", textAlign: "center" }}>Chọn</th>
                  <th>Loại</th>
                  <th>Mã dòng / Diễn giải</th>
                  <th>Bill</th>
                  <th>Đối tác</th>
                  <th>Tiền tệ</th>
                  <th style={{ textAlign: "right" }}>Giá trị gốc</th>
                  <th style={{ textAlign: "right" }}>Đã gắn CT</th>
                  <th style={{ textAlign: "right" }}>Còn lại</th>
                  <th style={{ width: "10rem", textAlign: "right" }}>Gán đợt này</th>
                </tr>
              </thead>
              <tbody>
                {eligibleLines.map((line) => {
                  const isChecked = selectedLinesMap.has(line.sourceId);
                  const selectedAmount =
                    selectedLinesMap.get(line.sourceId)?.amount ?? 0;

                  return (
                    <tr
                      key={line.sourceId}
                      style={{
                        backgroundColor: isChecked ? "var(--accent-soft)" : undefined,
                      }}
                    >
                      <td style={{ textAlign: "center" }}>
                        <input
                          type="checkbox"
                          checked={isChecked}
                          disabled={busy}
                          onChange={() => toggleLine(line)}
                        />
                      </td>
                      <td>
                        <span className="badge">
                          {line.sourceType === "cost" ? "Chi phí" : "Doanh thu"}
                        </span>
                      </td>
                      <td>
                        <strong>{line.lineCode}</strong>
                        {line.description && line.description !== line.lineCode ? (
                          <div className="muted small">{line.description}</div>
                        ) : null}
                      </td>
                      <td>{line.billNo || "—"}</td>
                      <td>{line.counterpartyName || "—"}</td>
                      <td>{line.currencyCode}</td>
                      <td style={{ textAlign: "right" }}>
                        {formatMoney(line.originalAmount, line.currencyCode)}
                      </td>
                      <td style={{ textAlign: "right" }}>
                        {formatMoney(line.alreadyDocumentedAmount, line.currencyCode)}
                      </td>
                      <td style={{ textAlign: "right", fontWeight: 600 }}>
                        {formatMoney(line.remainingEligibleAmount, line.currencyCode)}
                      </td>
                      <td style={{ textAlign: "right" }}>
                        <input
                          type="number"
                          step="any"
                          min={0}
                          max={line.remainingEligibleAmount}
                          value={isChecked ? selectedAmount : ""}
                          placeholder={isChecked ? String(line.remainingEligibleAmount) : "0"}
                          disabled={!isChecked || busy}
                          style={{
                            width: "100%",
                            textAlign: "right",
                            padding: "0.25rem 0.5rem",
                          }}
                          onChange={(e) => {
                            const val = Number(e.target.value);
                            updateLineAmount(line, isNaN(val) ? 0 : val);
                          }}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </fieldset>

      {/* BƯỚC 6: Tổng tiền & Đối soát (Matching Summary) */}
      <fieldset className="group-box" style={{ marginBottom: "1.5rem" }}>
        <legend>Bước 6: Tổng tiền &amp; Đối soát chênh lệch</legend>
        <div className="form-grid cols-2">
          <div className="field">
            <label htmlFor="totalAmountInput">
              Tổng tiền chứng từ ({currencyCode})
              {mode === "lcms_generated" ? (
                <span className="badge" style={{ marginLeft: "0.5rem" }}>
                  Tự động tính từ dòng chọn (Khóa nhập)
                </span>
              ) : null}
            </label>
            {mode === "lcms_generated" ? (
              <input
                id="totalAmountInput"
                type="text"
                readOnly
                value={formatMoney(selectedLinesSum, currencyCode)}
                disabled={busy}
                style={{
                  fontWeight: "bold",
                  backgroundColor: "var(--line)",
                  cursor: "not-allowed",
                }}
              />
            ) : (
              <input
                id="totalAmountInput"
                type="number"
                step="any"
                min={0}
                required
                value={manualTotalAmount}
                disabled={busy}
                placeholder="Nhập tổng tiền theo hóa đơn thực tế…"
                onChange={(e) => setManualTotalAmount(e.target.value)}
              />
            )}
            <p className="note" style={{ margin: "0.25rem 0 0" }}>
              {mode === "lcms_generated"
                ? "Mode B: Tổng tiền = tổng các khoản tài chính được chọn (FD-R02, AC-FD-001, AC-FD-002)."
                : "Mode A: Nhập theo hóa đơn ngoài; cho phép đối soát chênh lệch với Cost/Revenue (FD-R03, AC-FD-003)."}
            </p>
          </div>

          <div
            className="field"
            style={{
              padding: "0.75rem 1rem",
              background: "var(--accent-soft)",
              borderRadius: "var(--radius)",
            }}
          >
            <h4 style={{ margin: "0 0 0.5rem" }}>Tóm tắt đối soát (Matching Summary)</h4>
            <div style={{ display: "grid", gridTemplateColumns: "1fr auto", rowGap: "0.25rem" }}>
              <span>Số dòng đã chọn:</span>
              <strong>{selectedLinesMap.size} dòng</strong>

              <span>Tổng tiền dòng nguồn:</span>
              <strong>{formatMoney(selectedLinesSum, currencyCode)}</strong>

              <span>Tổng tiền chứng từ:</span>
              <strong>{formatMoney(finalTotalAmount, currencyCode)}</strong>

              {mode === "external_received" ? (
                <>
                  <span style={{ borderTop: "1px solid var(--line)", paddingTop: "0.25rem" }}>
                    Chênh lệch (Variance):
                  </span>
                  <strong
                    style={{
                      borderTop: "1px solid var(--line)",
                      paddingTop: "0.25rem",
                      color: variance === 0 ? "var(--success)" : "var(--warning)",
                    }}
                  >
                    {variance > 0 ? `+${variance}` : variance} {currencyCode}
                  </strong>
                </>
              ) : null}
            </div>
          </div>
        </div>
      </fieldset>

      {/* BƯỚC 7: Nút lưu */}
      <div className="cta-row">
        <button
          type="button"
          className="btn btn-ghost"
          disabled={busy}
          onClick={() => router.back()}
        >
          Hủy bỏ
        </button>
        <button type="submit" className="btn" disabled={busy}>
          {busy
            ? "Đang lưu…"
            : mode === "lcms_generated"
              ? `Tạo chứng từ từ LCMS (${selectedLinesMap.size} dòng)`
              : `Nhận ${docLabel.toLowerCase()} & Khớp`}
        </button>
      </div>
    </form>
  );
}
