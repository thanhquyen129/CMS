import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { RecognizeExposureForm } from "@/components/RecognizeExposureForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  exposureStatusLabel,
  getPayableExposure,
  getReceivableExposure,
} from "@/lib/ap-ar";
import { formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;
type SearchParams = Promise<{ kind?: string }>;

export default async function RecognizeExposurePage({
  params,
  searchParams,
}: {
  params: Params;
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const { kind: kindRaw } = await searchParams;
  const kind = kindRaw === "receivable" ? "receivable" : "payable";
  const terms = await fetchTerminology();

  const exposureRes =
    kind === "payable"
      ? await getPayableExposure(id)
      : await getReceivableExposure(id);

  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const targetLabel = kind === "payable" ? apLabel : arLabel;
  const exposureLabel =
    kind === "payable"
      ? term(terms, "PAYABLE_EXPOSURE", "Nghĩa vụ phải trả (exposure)")
      : term(terms, "RECEIVABLE_EXPOSURE", "Quyền thu dự kiến (exposure)");

  if (!exposureRes.ok) {
    return (
      <AppShell terms={terms} active="ap">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">
            {exposureRes.message}
          </div>
          <p className="cta-row">
            <Link className="btn btn-ghost" href="/ap-ar?tab=exposure">
              Quay lại exposure
            </Link>
          </p>
        </section>
      </AppShell>
    );
  }

  const exp = exposureRes.data;

  return (
    <AppShell terms={terms} active="ap">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/ap-ar?tab=exposure">Exposure</Link>
          {" / "}
          Ghi nhận
        </p>
        <h1>
          Ghi nhận → {targetLabel}
        </h1>
        <p className="lede">
          {exposureLabel}: {formatMoney(exp.amount, exp.currencyCode)} · còn mở{" "}
          {formatMoney(exp.openAmount, exp.currencyCode)} ·{" "}
          {exposureStatusLabel(exp.status)}
          {exp.billId ? (
            <>
              {" · "}
              <Link className="row-link" href={`/bills/${exp.billId}`}>
                Mở Bill
              </Link>
            </>
          ) : null}
        </p>
        <RecognizeExposureForm
          terms={terms}
          kind={kind}
          exposureId={id}
          openAmount={exp.openAmount}
          currencyCode={exp.currencyCode}
          billId={exp.billId}
          defaultDueDate={exp.dueDate}
          rowVersion={exp.rowVersion}
        />
      </section>
    </AppShell>
  );
}
