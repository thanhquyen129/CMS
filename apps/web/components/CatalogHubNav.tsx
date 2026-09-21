import Link from "next/link";

type Props = {
  active:
    | "customer"
    | "vendor"
    | "service_type"
    | "cost_type"
    | "revenue_type"
    | "transport_route"
    | "transport_mode"
    | "location"
    | "currency"
    | "other";
};

const TABS: { id: Props["active"]; href: string; title: string; desc: string }[] = [
  { id: "customer", href: "/admin/parties?role=customer", title: "Khách hàng", desc: "Bên mua dịch vụ" },
  { id: "vendor", href: "/admin/parties?role=vendor", title: "Nhà cung cấp", desc: "Bên bán / thầu" },
  { id: "service_type", href: "/admin/catalog?kind=service_type", title: "Dịch vụ", desc: "Loại dịch vụ" },
  { id: "cost_type", href: "/admin/catalog?kind=cost_type", title: "Loại chi phí", desc: "Taxonomy chi phí" },
  { id: "revenue_type", href: "/admin/catalog?kind=revenue_type", title: "Loại doanh thu", desc: "Taxonomy doanh thu" },
  { id: "transport_route", href: "/admin/catalog?kind=transport_route", title: "Tuyến vận chuyển", desc: "Master cho bảng giá" },
  { id: "transport_mode", href: "/admin/catalog?kind=transport_mode", title: "Phương thức vận chuyển", desc: "Đường / biển / hàng không" },
  { id: "location", href: "/admin/catalog?kind=location", title: "Cảng / Sân bay / Cửa khẩu", desc: "Địa điểm tài chính" },
  { id: "currency", href: "/admin/currencies", title: "Tiền tệ & Tỷ giá", desc: "ISO + tỷ giá ngày" },
  { id: "other", href: "/admin/catalog?kind=other", title: "Danh mục khác", desc: "Chứng từ, ĐKTT, tổ chức" },
];

export function CatalogHubNav({ active }: Props) {
  return (
    <div className="hub-module-tabs" role="tablist" aria-label="Danh mục dữ liệu">
      {TABS.map((item) => (
        <Link
          key={item.id}
          href={item.href}
          className={`hub-module-tab${active === item.id ? " is-active" : ""}`}
          role="tab"
          aria-selected={active === item.id}
        >
          <strong>{item.title}</strong>
          <span>{item.desc}</span>
        </Link>
      ))}
    </div>
  );
}
