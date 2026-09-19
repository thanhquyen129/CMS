import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

/** UI-15 — Navigation & Workflow Map (training/navigation only; no domain state). */
export default async function WorkflowMapPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");

  const steps = [
    {
      no: "01",
      title: billLabel,
      desc: "Neo tài chính — hồ sơ chi phí / doanh thu / chứng từ",
      href: "/bills",
    },
    {
      no: "01b",
      title: "Tham chiếu vận hành",
      desc: "Đơn hàng / lô / chặng / chuyến — neo Bill, không TMS",
      href: "/operations",
    },
    {
      no: "02",
      title: "Tính giá",
      desc: "Rate card tạo kỳ vọng tài chính, không tạo Thực tế",
      href: "/rate-cards",
    },
    {
      no: "03",
      title: costLabel,
      desc: "Dự kiến → Đã xác nhận → Thực tế; phân bổ bảo toàn tổng",
      href: "/costs",
    },
    {
      no: "04",
      title: revenueLabel,
      desc: "Doanh thu ≠ hóa đơn / AR / thu tiền",
      href: "/revenues",
    },
    {
      no: "05",
      title: docLabel,
      desc: "Nhận ≠ Chấp nhận ≠ Khớp ≠ Ghi nhận ≠ Tất toán",
      href: "/documents",
    },
    {
      no: "06",
      title: "Exposure",
      desc: "Nghĩa vụ / quyền dự kiến — trước khi ghi nhận AP/AR",
      href: "/ap-ar?tab=exposure",
    },
    {
      no: "07",
      title: `${apLabel} / ${arLabel}`,
      desc: "Ghi nhận công nợ không tạo Cost/Revenue mới",
      href: "/ap-ar",
    },
    {
      no: "08",
      title: `${paymentLabel} & ${collectionLabel}`,
      desc: "Tất toán khi allocation hợp lệ",
      href: "/settlements",
    },
    {
      no: "09",
      title: term(terms, "BANK_FEED", "Sao kê ngân hàng"),
      desc: "Nhập dòng sao kê — khớp / bỏ qua trước đối soát",
      href: "/bank-feed",
    },
    {
      no: "10",
      title: term(terms, "RECONCILIATION", "Đối soát"),
      desc: "Đối soát ngân hàng với thanh toán / thu tiền",
      href: "/reconciliations",
    },
    {
      no: "11",
      title: "Kiểm soát",
      desc: "Chênh lệch ≠ Ngoại lệ; phê duyệt độc lập",
      href: "/control",
    },
    {
      no: "12",
      title: closeLabel,
      desc: "Snapshot bất biến; reopen / reclose theo policy",
      href: "/financial-closes",
    },
    {
      no: "13",
      title: "Báo cáo",
      desc: "Read model — truy ngược Bill & giao dịch nguồn",
      href: "/reports",
    },
  ];

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Tổng thể hệ thống" },
          ]}
          title="Bản đồ điều hướng & luồng nghiệp vụ"
          lede={`Bản đồ đào tạo / điều hướng end-to-end. Không tạo trạng thái domain hay workflow song song. ${billLabel} là Financial Anchor của CMS.`}
        />

        <div className="workflow-strip">
          {steps.map((s) => (
            <Link key={s.no} href={s.href} className="workflow-step">
              <span className="workflow-step-no">{s.no}</span>
              <strong>{s.title}</strong>
              <span>{s.desc}</span>
            </Link>
          ))}
        </div>

        <div className="hub-module-tabs">
          <div className="hub-module-tab">
            <strong>Vận hành / tài chính</strong>
            <span>
              Ghi nhận trên {billLabel} → chứng từ → AP/AR → thanh toán/thu → kiểm soát →
              chốt.
            </span>
          </div>
          <Link href="/admin" className="hub-module-tab">
            <strong>Quản trị</strong>
            <span>Danh mục dữ liệu · Hệ thống & Cài đặt</span>
          </Link>
          <div className="hub-module-tab">
            <strong>Lưu ý kiến trúc</strong>
            <span>
              CMS là lớp kiểm soát tài chính (H-002). Đơn hàng/Shipment vận hành vẫn thuộc
              hệ thống SoT vận hành — không dựng TMS trong CMS.
            </span>
          </div>
        </div>
      </section>
    </AppShell>
  );
}
