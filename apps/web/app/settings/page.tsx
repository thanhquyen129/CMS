import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SettingsForm } from "@/components/SettingsForm";
import { TenantFinancialSettingsForm } from "@/components/TenantFinancialSettingsForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function SettingsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="settings">
      <div className="panel panel-wide">
        <p className="breadcrumb">
          <a href="/dashboard">Trang chủ</a>
          {" / "}
          Hệ thống &amp; Cài đặt
        </p>
        <h1>Hệ thống &amp; Cài đặt</h1>
        <p className="lede">
          Giao diện (cookie), cấu hình nghiệp vụ thuê bao, và liên kết quản trị.
          Ngưỡng ghi đè node khi đã lưu.
        </p>

        <div className="hub-links">
          <Link href="/admin/access" className="panel">
            <h2 className="section-title">🔐 Phân quyền</h2>
            <p className="muted">Vai trò, gán thành viên, quyền hành động × phạm vi.</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
          <Link href="/admin" className="panel">
            <h2 className="section-title">📋 Danh mục dữ liệu</h2>
            <p className="muted">Đối tác, đơn vị tổ chức, tiền tệ.</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
          <Link href="/integration-errors" className="panel">
            <h2 className="section-title">🔌 Lỗi tích hợp</h2>
            <p className="muted">Hàng đợi lỗi đồng bộ / dead-letter cần xử lý.</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
          <Link href="/workflow" className="panel">
            <h2 className="section-title">🗺️ Bản đồ luồng hệ thống</h2>
            <p className="muted">Điều hướng end-to-end theo 11 bước nghiệp vụ.</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
        </div>

        <SettingsForm />
        <TenantFinancialSettingsForm />
      </div>
    </AppShell>
  );
}
