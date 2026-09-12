import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateExposureForm } from "@/components/CreateExposureForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

type SearchParams = Promise<{
  kind?: string;
  billId?: string;
}>;

export default async function NewExposurePage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { kind: kindRaw, billId } = await searchParams;
  const kind = kindRaw === "receivable" ? "receivable" : "payable";
  const terms = await fetchTerminology();
  const exposureLabel =
    kind === "payable"
      ? term(terms, "PAYABLE_EXPOSURE", "Nghĩa vụ phải trả (exposure)")
      : term(terms, "RECEIVABLE_EXPOSURE", "Quyền thu dự kiến (exposure)");
  const apArLabel = `${term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả")} / ${term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu")}`;

  return (
    <AppShell terms={terms} active="ap-ar">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/ap-ar?tab=exposure">{apArLabel}</Link>
          {" / "}
          Tạo exposure
        </p>
        <h1>Tạo {exposureLabel}</h1>

        <div className="search-bar" role="tablist" aria-label="Loại exposure">
          <Link
            className={kind === "payable" ? "btn" : "btn btn-ghost"}
            href={`/ap-ar/exposures/new?kind=payable${billId ? `&billId=${encodeURIComponent(billId)}` : ""}`}
            role="tab"
            aria-selected={kind === "payable"}
          >
            Phải trả
          </Link>
          <Link
            className={kind === "receivable" ? "btn" : "btn btn-ghost"}
            href={`/ap-ar/exposures/new?kind=receivable${billId ? `&billId=${encodeURIComponent(billId)}` : ""}`}
            role="tab"
            aria-selected={kind === "receivable"}
          >
            Phải thu
          </Link>
        </div>

        <CreateExposureForm
          terms={terms}
          kind={kind}
          defaultBillId={billId}
        />
      </section>
    </AppShell>
  );
}
