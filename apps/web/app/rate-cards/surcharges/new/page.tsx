import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SurchargeForm } from "@/components/SurchargeForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listRateCards } from "@/lib/rate-cards-server";
import { unwrapPaged } from "@/lib/paging";

export default async function NewSurchargePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const cards = await listRateCards({ active: true, pageSize: 100 });
  const options = cards.ok
    ? unwrapPaged(cards.data).items.map((card) => ({ id: card.id, label: `${card.code} — ${card.name}` }))
    : [];

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href="/rate-cards/surcharges">← Quản lý phụ phí</Link>
        </p>
        <h1>Tạo phụ phí</h1>
        <p className="muted">Không cần chọn bảng giá. Bỏ trống các điều kiện nếu phụ phí áp dụng chung.</p>
        <SurchargeForm cards={options} />
      </section>
    </AppShell>
  );
}
