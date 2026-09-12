import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ReceiveDocumentForm } from "@/components/ReceiveDocumentForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listBusinessParties } from "@/lib/parties";

type SearchParams = Promise<{ billId?: string }>;

export default async function ReceiveDocumentPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { billId } = await searchParams;
  const terms = await fetchTerminology();
  const partiesResult = await listBusinessParties();
  const parties = partiesResult.ok ? partiesResult.data : [];
  const partiesError = partiesResult.ok ? null : partiesResult.message;
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");

  return (
    <AppShell
      terms={terms}
      active="documents"
      topbarRight={
        <Link className="btn btn-ghost btn-sm" href="/documents">
          ← Danh sách
        </Link>
      }
    >
      <section className="panel">
        <p className="breadcrumb">
          <Link href="/documents">{docLabel}</Link>
          {" / "}
          Nhận
        </p>
        <h1>Nhận {docLabel.toLowerCase()}</h1>
        <p className="lede">
          Hành động chính: đặt trạng thái {receivedLabel}. Chấp nhận và khớp là
          bước riêng sau này. Thêm dòng đủ tổng trước khi chấp nhận.
        </p>
        <ReceiveDocumentForm
          terms={terms}
          defaultBillId={billId}
          parties={parties}
          partiesError={partiesError}
        />
      </section>
    </AppShell>
  );
}
