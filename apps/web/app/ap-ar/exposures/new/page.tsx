import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateExposureForm } from "@/components/CreateExposureForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listCostsByBill,
  listRevenuesByBill,
} from "@/lib/costs-revenues-server";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{
  kind?: string;
  billId?: string;
  costId?: string;
  revenueId?: string;
  amount?: string;
  currency?: string;
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

  const {
    kind: kindRaw,
    billId,
    costId,
    revenueId,
    amount: amountRaw,
    currency,
  } = await searchParams;
  const kind = kindRaw === "receivable" ? "receivable" : "payable";
  const terms = await fetchTerminology();
  const exposureLabel =
    kind === "payable"
      ? term(terms, "PAYABLE_EXPOSURE", "Nghĩa vụ phải trả (exposure)")
      : term(terms, "RECEIVABLE_EXPOSURE", "Quyền thu dự kiến (exposure)");
  const apArLabel = `${term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả")} / ${term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu")}`;

  const parsedAmount = amountRaw != null ? Number(amountRaw) : undefined;
  const defaultAmount =
    parsedAmount != null && Number.isFinite(parsedAmount)
      ? parsedAmount
      : undefined;

  const qsBase = (k: string) => {
    const p = new URLSearchParams({ kind: k });
    if (billId) p.set("billId", billId);
    if (costId && k === "payable") p.set("costId", costId);
    if (revenueId && k === "receivable") p.set("revenueId", revenueId);
    if (amountRaw) p.set("amount", amountRaw);
    if (currency) p.set("currency", currency);
    return `/ap-ar/exposures/new?${p.toString()}`;
  };

  const [costsRes, revsRes] = billId
    ? await Promise.all([listCostsByBill(billId), listRevenuesByBill(billId)])
    : [null, null];

  const costOptions =
    costsRes?.ok
      ? costsRes.data
          .filter((c) => c.recordStatus === "active")
          .map((c) => ({
            id: c.id,
            label: `${c.costTypeCode || "Cost"} · ${c.financialMaturity}`,
            amount: c.amount,
            currencyCode: c.currencyCode,
          }))
      : [];
  const revenueOptions =
    revsRes?.ok
      ? revsRes.data
          .filter((r) => r.recordStatus === "active")
          .map((r) => ({
            id: r.id,
            label: `${r.revenueTypeCode || "Revenue"} · ${r.financialMaturity}`,
            amount: r.amount,
            currencyCode: r.currencyCode,
          }))
      : [];

  return (
    <AppShell terms={terms} active="ap-ar">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/ap-ar?tab=exposure">{apArLabel}</Link>
          {" / "}
          Tạo exposure
        </p>
        <h1>Tạo {exposureLabel}</h1>
        {billId && defaultAmount != null ? (
          <p className="lede">
            Prefill từ Bill · {formatMoney(defaultAmount, currency || "VND")}
          </p>
        ) : null}

        <div className="search-bar" role="tablist" aria-label="Loại exposure">
          <Link
            className={kind === "payable" ? "btn" : "btn btn-ghost"}
            href={qsBase("payable")}
            role="tab"
            aria-selected={kind === "payable"}
          >
            Phải trả
          </Link>
          <Link
            className={kind === "receivable" ? "btn" : "btn btn-ghost"}
            href={qsBase("receivable")}
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
          defaultCurrency={currency || "VND"}
          defaultAmount={defaultAmount}
          defaultCostId={costId}
          defaultRevenueId={revenueId}
          costOptions={costOptions}
          revenueOptions={revenueOptions}
        />
      </section>
    </AppShell>
  );
}
