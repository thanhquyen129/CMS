import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";
import type {
  FinancialDocument,
  FinancialDocumentListItem,
} from "./documents-shared";
import { unwrapPaged, type PagedResult } from "./paging";

export type {
  FinancialDocument,
  FinancialDocumentLine,
  FinancialDocumentListItem,
  ReceiveDocumentBody,
} from "./documents-shared";
export {
  acceptanceStatusLabel,
  canAcceptDocument,
  canAddDocumentLine,
  canMutateDocumentLine,
  directionLabel,
  documentLineCoverage,
  documentTypeLabel,
  matchingStatusLabel,
  receiptStatusLabel,
  recordStatusLabel,
} from "./documents-shared";

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

export function listFinancialDocuments(opts?: {
  documentType?: string;
  receiptStatus?: string;
  acceptanceStatus?: string;
  matchingStatus?: string;
  billId?: string;
  page?: number;
  pageSize?: number;
}): Promise<ApiResult<PagedResult<FinancialDocumentListItem>>> {
  const params = new URLSearchParams();
  if (opts?.documentType) params.set("documentType", opts.documentType);
  if (opts?.receiptStatus) params.set("receiptStatus", opts.receiptStatus);
  if (opts?.acceptanceStatus)
    params.set("acceptanceStatus", opts.acceptanceStatus);
  if (opts?.matchingStatus) params.set("matchingStatus", opts.matchingStatus);
  if (opts?.billId) params.set("billId", opts.billId);
  if (opts?.page != null) params.set("page", String(opts.page));
  if (opts?.pageSize != null) params.set("pageSize", String(opts.pageSize));
  const qs = params.toString();
  return apiGet<FinancialDocumentListItem[] | PagedResult<FinancialDocumentListItem>>(
    qs ? `/api/financial-documents?${qs}` : "/api/financial-documents"
  ).then((r) => (r.ok ? { ok: true, data: unwrapPaged(r.data) } : r));
}

export function getFinancialDocument(
  id: string
): Promise<ApiResult<FinancialDocument>> {
  return apiGet<FinancialDocument>(`/api/financial-documents/${id}`);
}
