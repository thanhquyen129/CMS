import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ComposeTariffForm } from "@/components/ComposeTariffForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function NewRateCardPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href="/rate-cards">
            ← Bảng giá
          </Link>
        </p>
        <h1>Tạo bảng giá</h1>
        <ComposeTariffForm />
      </section>
    </AppShell>
  );
}
