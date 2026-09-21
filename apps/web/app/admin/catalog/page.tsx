import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CatalogHubNav } from "@/components/CatalogHubNav";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { UpsertCatalogItemForm } from "@/components/UpsertCatalogItemForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import {
  CATALOG_KINDS,
  catalogKindLabel,
  locationClassLabel,
  listCatalog,
} from "@/lib/catalog";

const HUB_KINDS = new Set([
  "service_type",
  "cost_type",
  "revenue_type",
  "transport_route",
  "transport_mode",
  "location",
  "other",
]);

function parseAttributes(raw: string | null): { class?: string; origin?: string; destination?: string } {
  if (!raw) return {};
  try {
    return JSON.parse(raw) as { class?: string; origin?: string; destination?: string };
  } catch {
    return {};
  }
}

export default async function AdminCatalogPage({
  searchParams,
}: {
  searchParams: Promise<{ kind?: string }>;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const kindRaw = (sp.kind ?? "cost_type").trim();
  const hubActive = HUB_KINDS.has(kindRaw) ? kindRaw : "other";
  const otherKinds = CATALOG_KINDS.filter((k) => k.group === "other").map((k) => k.id);
  const filterKind = kindRaw === "other" ? undefined : kindRaw;
  const terms = await fetchTerminology();
  const result = await listCatalog(filterKind);
  const rows =
    result.ok && kindRaw === "other"
      ? result.data.filter((i) => otherKinds.includes(i.kind))
      : result.ok
        ? result.data
        : [];
  const title =
    kindRaw === "other"
      ? "Danh mục khác"
      : catalogKindLabel(filterKind ?? "cost_type");

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/admin", label: "Danh mục" },
            { label: title },
          ]}
          title={title}
          lede="Danh mục theo thuê bao dùng cho chi phí, doanh thu, bảng giá. Không xóa — ngưng dùng bằng cập nhật. Tuyến / phương thức / cảng là master tính giá, không phải TMS."
        />
        <CatalogHubNav
          active={
            hubActive as
              | "service_type"
              | "cost_type"
              | "revenue_type"
              | "transport_route"
              | "transport_mode"
              | "location"
              | "other"
          }
        />

        {kindRaw === "other" ? (
          <p className="note">
            Tổ chức (phạm vi dữ liệu) nằm tại{" "}
            <Link href="/admin/organizations">Đơn vị / Tổ chức</Link>.
          </p>
        ) : null}

        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">
                {result.message}
              </div>
            ) : rows.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có mã danh mục.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Nhóm</th>
                      <th scope="col">Mã</th>
                      <th scope="col">Tên</th>
                      <th scope="col">Chi tiết</th>
                      <th scope="col">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((i) => {
                      const attrs = parseAttributes(i.attributesJson);
                      const detail =
                        i.kind === "location"
                          ? locationClassLabel(attrs.class)
                          : i.kind === "transport_route" && (attrs.origin || attrs.destination)
                            ? `${attrs.origin ?? "—"} → ${attrs.destination ?? "—"}`
                            : i.description ?? "—";
                      return (
                        <tr key={i.id}>
                          <td>{catalogKindLabel(i.kind)}</td>
                          <td className="mono-id">{i.code}</td>
                          <td>{i.name}</td>
                          <td>{detail}</td>
                          <td>{i.isActive ? "Đang dùng" : "Ngừng"}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </fieldset>
          <fieldset className="group-box">
            <legend>Thêm / cập nhật</legend>
            <UpsertCatalogItemForm
              defaultKind={kindRaw === "other" ? "document_type" : filterKind ?? "cost_type"}
            />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
