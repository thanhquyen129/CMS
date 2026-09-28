import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ApArReconciliationWorkspace } from "@/components/ApArReconciliationWorkspace";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

export default async function ApArReconciliationPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");

  return (
    <AppShell terms={terms} active="ap">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/ap-ar">
            {apLabel} / {arLabel}
          </Link>
          <span aria-hidden="true"> / </span>
          <span>Đối soát số dư</span>
        </p>
        <h1>Đối soát số dư công nợ</h1>
        <p className="lede">
          So sánh số dư hiện tại với số dư dựng lại từ sổ công nợ (ghi nhận, điều chỉnh, phân bổ đã
          chốt). Hệ thống không tự sửa dữ liệu lịch sử — chỉ đối soát khi xác định được nguyên nhân, có
          lý do và nhật ký.
        </p>
        <ApArReconciliationWorkspace />
      </section>
    </AppShell>
  );
}
