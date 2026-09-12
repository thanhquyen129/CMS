import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { StartDocumentMatchForm } from "@/components/StartDocumentMatchForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  canStartMatch,
  defaultMatchMethod,
} from "@/lib/document-matches";
import { getFinancialDocument } from "@/lib/documents";

type Params = Promise<{ id: string }>;

export default async function StartDocumentMatchPage({
  params,
}: {
  params: Params;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const matchLabel = term(terms, "MATCHED", "Khớp");

  const result = await getFinancialDocument(id);

  if (!result.ok) {
    return (
      <AppShell terms={terms} active="documents">
        <section className="panel">
          <h1>
            {result.status === 404
              ? `Không tìm thấy ${docLabel.toLowerCase()}`
              : matchLabel}
          </h1>
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
          <Link className="btn btn-ghost" href={`/documents/${id}`}>
            Quay lại chứng từ
          </Link>
        </section>
      </AppShell>
    );
  }

  const doc = result.data;
  const allowed = canStartMatch(doc);

  return (
    <AppShell
      terms={terms}
      active="documents"
      topbarRight={
        <Link className="btn btn-ghost btn-sm" href={`/documents/${id}`}>
          ← {doc.documentNo}
        </Link>
      }
    >
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/documents">{docLabel}</Link>
          <span aria-hidden="true"> / </span>
          <Link href={`/documents/${id}`}>{doc.documentNo}</Link>
          <span aria-hidden="true"> / </span>
          <span>Mở phiên {matchLabel.toLowerCase()}</span>
        </p>
        <h1>
          Mở phiên {matchLabel.toLowerCase()} — {doc.documentNo}
        </h1>
        <p className="lede">
          Hành động chính: tạo phiên nháp. Thêm chi tiết khớp ở bước sau. Nhận ≠
          Chấp nhận ≠ Khớp.
        </p>

        {!allowed ? (
          <div className="alert alert-error" role="alert">
            Chỉ mở khớp khi chứng từ đã nhận, đã chấp nhận, còn hiệu lực và còn
            số mở trên dòng. Kiểm tra ba chiều trạng thái trên chứng từ.
          </div>
        ) : (
          <StartDocumentMatchForm
            terms={terms}
            documentId={doc.id}
            documentNo={doc.documentNo}
            defaultMethod={defaultMatchMethod(doc.direction)}
          />
        )}
      </section>
    </AppShell>
  );
}
