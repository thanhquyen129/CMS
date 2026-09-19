import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateBillForm } from "@/components/CreateBillForm";
import { WaybillCaptureForm } from "@/components/WaybillCaptureForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

export default async function NewBillPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/bills">Danh sách {billLabel}</Link>
          {" / "}
          Tạo mới
        </p>
        <h1>Tạo vận đơn ({billLabel})</h1>
        <p className="lede muted">
          Nhập như vận đơn giấy: người gửi, người nhận, kiện, cước. Hệ thống tạo {billLabel} neo tài chính — không phải điều vận TMS.
        </p>
        <WaybillCaptureForm terms={terms} />
        <details className="waybill-simple-create">
          <summary>Chỉ tạo số {billLabel} (không nhập vận đơn giấy)</summary>
          <CreateBillForm terms={terms} />
        </details>
      </section>
    </AppShell>
  );
}
