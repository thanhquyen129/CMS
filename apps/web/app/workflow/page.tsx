import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
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
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Tổng thể hệ thống
        </p>
        <h1>Bản đồ điều hướng &amp; luồng nghiệp vụ</h1>
        <p className="lede">
          Bản đồ đào tạo / điều hướng end-to-end. Không tạo trạng thái domain hay workflow
          song song. {billLabel} là Financial Anchor của CMS.
        </p>

        <div className="workflow-strip">
          {steps.map((s) => (
            <Link key={s.no} href={s.href} className="workflow-step">
              <span className="workflow-step-no">{s.no}</span>
              <strong>{s.title}</strong>
              <span>{s.desc}</span>
            </Link>
          ))}
        </div>

        <div className="hub-links">
          <div className="panel">
            <h2 className="section-title">Vận hành / tài chính</h2>
            <p className="muted">
              Ghi nhận trên {billLabel} → chứng từ → AP/AR → thanh toán/thu → kiểm soát →
              chốt.
            </p>
          </div>
          <div className="panel">
            <h2 className="section-title">Quản trị</h2>
            <p className="muted">
              <Link className="row-link" href="/admin">
                Danh mục dữ liệu
              </Link>
              {" · "}
              <Link className="row-link" href="/settings">
                Hệ thống &amp; Cài đặt
              </Link>
            </p>
          </div>
          <div className="panel">
            <h2 className="section-title">Lưu ý kiến trúc</h2>
            <p className="muted">
              CMS là lớp kiểm soát tài chính (H-002). Đơn hàng/Shipment vận hành vẫn thuộc
              hệ thống SoT vận hành — không dựng TMS trong CMS.
            </p>
          </div>
        </div>
      </section>
    </AppShell>
  );
}
