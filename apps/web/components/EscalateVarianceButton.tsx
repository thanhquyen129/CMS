"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useState, useTransition } from "react";
import { formatMoney } from "@/lib/money";
import { severityLabel } from "@/lib/severity";
import { term, type TerminologyMap } from "@/lib/terminology";

export type EscalateVariance = {
  id: string;
  reconciliationId: string | null;
  amount: number;
  currencyCode: string;
  status: string;
  severity: string;
  explanation: string | null;
  exceptionId: string | null;
};

type Props = {
  terms: TerminologyMap;
  variance: EscalateVariance;
};

export function EscalateVarianceButton({ terms, variance }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const exceptionLabel = term(terms, "EXCEPTION", "Ngoại lệ");
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");
  const exceptionQueueLabel = term(
    terms,
    "EXCEPTION_QUEUE",
    "Hàng đợi ngoại lệ"
  );

  const escalate = useCallback(async () => {
    setSubmitting(true);
    setError(null);
    setInfo(null);

    const title =
      variance.explanation?.trim() ||
      `Leo thang ${varianceLabel.toLowerCase()} ${formatMoney(variance.amount, variance.currencyCode)}`;

    try {
      const res = await fetch("/bff/exceptions", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          ruleCode: "variance.manual_escalate",
          severity: variance.severity,
          title,
          description:
            variance.explanation ||
            `${varianceLabel}: ${formatMoney(variance.amount, variance.currencyCode)} (${severityLabel(variance.severity)})`,
          reconciliationId: variance.reconciliationId,
          varianceId: variance.id,
          objectType: "variance",
          objectId: variance.id,
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
        setError(
          body.message ||
            (res.status === 409
              ? "Không mở ngoại lệ được (đã leo thang / trạng thái lệch). Tải lại trang."
              : `Mở ${exceptionLabel.toLowerCase()} thất bại.`)
        );
        return;
      }

      setInfo(`Đã mở ${exceptionLabel.toLowerCase()} từ ${varianceLabel.toLowerCase()}.`);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [exceptionLabel, router, variance, varianceLabel]);

  if (variance.exceptionId) {
    return (
      <Link className="row-link" href="/queues/exceptions">
        Đã leo thang → {exceptionQueueLabel}
      </Link>
    );
  }

  if (variance.status.toLowerCase() !== "open") {
    return <span className="muted">—</span>;
  }

  return (
    <>
      <button
        type="button"
        className="btn btn-sm"
        onClick={escalate}
        disabled={submitting || isPending}
      >
        {submitting ? "Đang gửi…" : `Mở ${exceptionLabel.toLowerCase()}`}
      </button>
      {error ? (
        <div
          className="alert alert-error"
          role="alert"
          style={{ marginTop: "0.35rem" }}
        >
          {error}
        </div>
      ) : null}
      {info ? (
        <div
          className="alert alert-info"
          role="status"
          style={{ marginTop: "0.35rem" }}
        >
          {info}{" "}
          <Link className="row-link" href="/queues/exceptions">
            Mở {exceptionQueueLabel.toLowerCase()}
          </Link>
        </div>
      ) : null}
    </>
  );
}
