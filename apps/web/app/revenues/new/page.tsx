import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { BillPickTable } from "@/components/BillPickTable";
import { CreateRevenueForm } from "@/components/CreateRevenueForm";
import { FilterBar, ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getBill, listBills } from "@/lib/bills";
import {
  billTypeLabel,
  operationalStatusLabel,
  operationalStatusPillClass,
  transportModeLabel,
} from "@/lib/bills-shared";
import { formatDateVi } from "@/lib/money";

type SearchParams = Promise<{ billId?: string; q?: string }>;

export default async function NewRevenuePage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { billId, q } = await searchParams;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const costLabel = term(terms, "COST", "Chi phí");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");

  const bills = await listBills(q?.trim() || undefined, {
    page: 1,
    pageSize: 50,
  });

  const selectedRes = billId ? await getBill(billId) : null;
  const selected = selectedRes?.ok ? selectedRes.data : null;

  return (
    <AppShell terms={terms} active="revenues">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/revenues", label: "Danh sách doanh thu" },
            { label: "Tạo doanh thu" },
          ]}
          title="Tạo doanh thu"
          lede={`Doanh thu kinh tế gắn một ${billLabel}. Chọn ${billLabel} như danh sách vận đơn, rồi nhập số tiền. Chia nhiều ${billLabel} sau khi ghi.`}
        />

        {!bills.ok ? (
          <div className="alert alert-error" role="alert">
            {bills.message}
          </div>
        ) : null}

        {billId && selected ? (
          <fieldset className="group-box">
            <legend>{billLabel} đã chọn</legend>
            <div className="detail-head" style={{ display: "flex", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}>
              <div>
                <p style={{ margin: 0 }}>
                  <Link className="row-link" href={`/bills/${selected.id}`}>
                    <strong>{selected.billNo}</strong>
                  </Link>{" "}
                  <span className={operationalStatusPillClass(selected.operationalStatus)}>
                    {operationalStatusLabel(selected.operationalStatus)}
                  </span>
                </p>
                <p className="meta-line" style={{ marginTop: "0.35rem" }}>
                  {selected.customerName ?? "—"}
                  {" · "}
                  {selected.routeCode ?? "—"}
                  {" · "}
                  {selected.transportMode
                    ? transportModeLabel(selected.transportMode)
                    : billTypeLabel(selected.billType)}
                  {" · "}
                  Ngày tạo: {formatDateVi(selected.createdAt)}
                </p>
              </div>
              <div className="toolbar-row">
                <Link className="btn btn-secondary" href="/revenues/new">
                  Đổi {billLabel}
                </Link>
                <Link className="btn btn-ghost" href={`/bills/${selected.id}`}>
                  Hồ sơ {billLabel}
                </Link>
              </div>
            </div>
          </fieldset>
        ) : null}

        {billId && !selected ? (
          <div className="alert alert-error" role="alert">
            Không tìm thấy {billLabel}. Chọn lại từ danh sách.
          </div>
        ) : null}

        {billId && selected ? (
          <CreateRevenueForm terms={terms} billId={billId} />
        ) : (
          <>
            <FilterBar
              action="/revenues/new"
              submitLabel="Tìm"
              resetHref={q ? "/revenues/new" : undefined}
              fields={[
                {
                  kind: "search",
                  name: "q",
                  label: `Tìm ${billLabel}`,
                  placeholder: `Tìm theo số ${billLabel}, khách hàng, tuyến…`,
                  defaultValue: q ?? "",
                },
              ]}
            />
            {bills.ok ? (
              <BillPickTable
                bills={bills.data.items}
                totalCount={bills.data.totalCount}
                billLabel={billLabel}
                revenueLabel={revenueLabel}
                costLabel={costLabel}
                profitLabel={profitLabel}
                selectedId={billId}
                pickHrefBase="/revenues/new"
                emptyMessage={
                  q
                    ? `Không có ${billLabel} khớp bộ lọc.`
                    : `Chưa có ${billLabel}. Tạo ${billLabel} trước khi ghi doanh thu.`
                }
              />
            ) : null}
            <p className="note">Chọn một dòng {billLabel} để mở form tạo doanh thu.</p>
          </>
        )}
      </section>
    </AppShell>
  );
}
