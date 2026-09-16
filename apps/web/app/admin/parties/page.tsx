import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdminPartyListWorkspace } from "@/components/AdminPartyListWorkspace";
import { CreateBusinessPartyForm } from "@/components/CreateBusinessPartyForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listAdminParties } from "@/lib/master-data";
import { PARTY_ROLE_OPTIONS } from "@/lib/party";

type Search = {
  q?: string;
  role?: string;
  status?: string;
};

export default async function AdminPartiesPage({
  searchParams,
}: {
  searchParams: Promise<Search>;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const result = await listAdminParties({
    search: sp.q,
    roleCode: sp.role,
    isActive:
      sp.status === "active"
        ? "true"
        : sp.status === "inactive"
          ? "false"
          : undefined,
  });

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          <Link href="/admin">Danh mục</Link>
          {" / "}
          Đối tác
        </p>
        <h1>Đối tác kinh doanh</h1>
        <p className="lede">
          Hồ sơ đối tác chuẩn tài chính: MST, vai trò, điều khoản thanh toán,
          hạn mức công nợ, tài khoản ngân hàng và người liên hệ.
        </p>

        <div className="filter-tabs" role="tablist" aria-label="Danh mục dữ liệu">
          <Link className="active" href="/admin/parties" role="tab" aria-selected="true">
            Đối tác
          </Link>
          <Link href="/admin/access" role="tab" aria-selected="false">
            Phân quyền
          </Link>
          <Link href="/admin/organizations" role="tab" aria-selected="false">
            Đơn vị / Tổ chức
          </Link>
          <Link href="/admin/currencies" role="tab" aria-selected="false">
            Tiền tệ
          </Link>
        </div>

        <form className="filter-bar" method="get">
          <div className="form-grid">
            <div className="field">
              <label htmlFor="q">Tìm kiếm</label>
              <input
                id="q"
                name="q"
                type="search"
                defaultValue={sp.q ?? ""}
                placeholder="Mã, tên, MST…"
              />
            </div>
            <div className="field">
              <label htmlFor="role">Vai trò</label>
              <select id="role" name="role" defaultValue={sp.role ?? ""}>
                <option value="">Tất cả</option>
                {PARTY_ROLE_OPTIONS.map((r) => (
                  <option key={r.code} value={r.code}>
                    {r.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="status">Trạng thái</label>
              <select id="status" name="status" defaultValue={sp.status ?? ""}>
                <option value="">Tất cả</option>
                <option value="active">Đang dùng</option>
                <option value="inactive">Ngừng</option>
              </select>
            </div>
          </div>
          <div className="cta-row">
            <button type="submit" className="btn">
              Lọc
            </button>
            <Link href="/admin/parties" className="btn btn-ghost">
              Xóa lọc
            </Link>
          </div>
        </form>

        <div className="layout-cols-2" style={{ marginTop: "1rem" }}>
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">
                {result.message}
              </div>
            ) : result.data.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có đối tác khớp bộ lọc.
              </div>
            ) : (
              <AdminPartyListWorkspace parties={result.data} />
            )}
          </fieldset>

          <fieldset className="group-box">
            <legend>Thêm đối tác</legend>
            <CreateBusinessPartyForm />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
