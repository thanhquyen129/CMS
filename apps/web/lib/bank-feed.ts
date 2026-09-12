import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type { TerminologyMap } from "./terminology";
import { term } from "./terminology";

export type BankFeedLineItem = {
  id: string;
  valueDate: string;
  amount: number;
  currencyCode: string;
  direction: string;
  bankReference: string | null;
  counterpartyName: string | null;
  description: string | null;
  status: string;
  matchedReconciliationDetailId: string | null;
  matchedAt: string | null;
  ignoredAt: string | null;
  ignoreReason: string | null;
};

async function apiGet<T>(path: string): Promise<ApiResult<T>> {
  const token = await getSessionToken();
  if (!token) {
    redirect("/login");
  }

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (res.status === 401) {
      redirect("/login");
    }

    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message:
          body.message ||
          (res.status === 403
            ? "Bạn không có quyền xem dữ liệu này."
            : "Không tải được dữ liệu từ máy chủ."),
      };
    }

    return { ok: true, data: (await res.json()) as T };
  } catch {
    return {
      ok: false,
      status: 0,
      message: "Không kết nối được máy chủ API. Thử lại sau.",
    };
  }
}

export function listBankFeedLines(opts?: {
  status?: string;
}): Promise<ApiResult<BankFeedLineItem[]>> {
  const qs = opts?.status ? `?status=${encodeURIComponent(opts.status)}` : "";
  return apiGet<BankFeedLineItem[]>(`/api/bank-feed/lines${qs}`);
}

export function bankFeedStatusLabel(
  terms: TerminologyMap,
  status: string
): string {
  switch (status.toLowerCase()) {
    case "unmatched":
      return term(terms, "BANK_FEED_UNMATCHED", "Chưa đối soát");
    case "matched":
      return term(terms, "BANK_FEED_MATCHED", "Đã đối soát");
    case "ignored":
      return term(terms, "BANK_FEED_IGNORED", "Đã bỏ qua");
    default:
      return status;
  }
}

export function bankDirectionLabel(
  terms: TerminologyMap,
  direction: string
): string {
  switch (direction.toLowerCase()) {
    case "credit":
      return term(terms, "BANK_CREDIT", "Thu vào");
    case "debit":
      return term(terms, "BANK_DEBIT", "Chi ra");
    default:
      return direction;
  }
}
